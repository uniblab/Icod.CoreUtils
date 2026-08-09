namespace Icod.ProcPs.Watch.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Processes;
using Icod.CoreUtils.Shared.Terminal;
using Icod.CoreUtils.Shared.Time;
using Icod.ProcPs.Shared;
using Xunit;
/// <summary>Exercises Batch 66 procps-ng 4.0.6 <c>watch</c> compatibility and lifecycle behavior.</summary>
public sealed class WatchCommandTests {
	/// <summary>Verifies direct execution preserves argument boundaries and restores the terminal.</summary>
	[Fact]
	public async Task ExecModePreservesArgumentBoundariesAndRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 50, 6 ) );
		var executor = new FakeExecutor(
			Execution.Success( "alpha\n" ),
			Execution.Success( "alpha\n" )
		);
		var result = await RunAsync(
			new[] { "--exec", "--equexit=1", "tool", "two words", "three" },
			terminal,
			executor
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 2, executor.Options.Count );
		Assert.Equal( "tool", executor.Options[ 0 ].FileName );
		Assert.Equal( new[] { "two words", "three" }, executor.Options[ 0 ].Arguments.ToArray() );
		Assert.Contains( "alpha", terminal.Frames[ 0 ], StringComparison.Ordinal );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
		Assert.Equal( string.Empty, result.Stderr );
	}
	/// <summary>Verifies default mode runs the reconstructed command through the host shell.</summary>
	[Fact]
	public async Task DefaultModeUsesHostShell() {
		var executor = new FakeExecutor(
			Execution.Success( "ok" ),
			Execution.Success( "ok" )
		);
		var result = await RunAsync(
			new[] { "--equexit", "1", "echo", "hello" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			executor
		);
		Assert.Equal( 0, result.ExitCode );
		var options = executor.Options[ 0 ];
		if ( OperatingSystem.IsWindows() ) {
			Assert.Equal( "cmd.exe", options.FileName );
			Assert.Equal( "/D", options.Arguments[ 0 ] );
			Assert.Equal( "/S", options.Arguments[ 1 ] );
			Assert.Equal( "/C", options.Arguments[ 2 ] );
			Assert.Equal( "echo hello", options.Arguments[ 3 ] );
		} else {
			Assert.Equal( "/bin/sh", options.FileName );
			Assert.Equal( new[] { "-c", "echo hello" }, options.Arguments.ToArray() );
		}
	}
	/// <summary>Verifies ordinary intervals are measured after command completion.</summary>
	[Fact]
	public async Task FixedDelayWaitsFullIntervalAfterCommand() {
		var clock = new FakeClock();
		var executor = new FakeExecutor(
			clock,
			TimeSpan.FromMilliseconds( 100 ),
			Execution.Success( "same" ),
			Execution.Success( "same" )
		);
		var result = await RunAsync(
			new[] { "--exec", "--interval", "0.25", "--equexit=1", "tool" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			executor,
			clock: clock
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Single( clock.Delays );
		Assert.Equal( TimeSpan.FromMilliseconds( 250 ), clock.Delays[ 0 ] );
	}
	/// <summary>Verifies precise mode counts child running time toward the requested cadence.</summary>
	[Fact]
	public async Task PreciseIntervalIncludesCommandRunningTime() {
		var clock = new FakeClock();
		var executor = new FakeExecutor(
			clock,
			TimeSpan.FromMilliseconds( 100 ),
			Execution.Success( "same" ),
			Execution.Success( "same" )
		);
		var result = await RunAsync(
			new[] { "--exec", "--precise", "--interval=0.25", "--equexit=1", "tool" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			executor,
			clock: clock
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Single( clock.Delays );
		Assert.Equal( TimeSpan.FromMilliseconds( 150 ), clock.Delays[ 0 ] );
	}
	/// <summary>Verifies WATCH_INTERVAL is parsed through an injected environment rather than ambient test state.</summary>
	[Fact]
	public async Task WatchIntervalEnvironmentControlsCadence() {
		var clock = new FakeClock();
		var executor = new FakeExecutor( Execution.Success( "same" ), Execution.Success( "same" ) );
		var result = await RunAsync(
			new[] { "--exec", "--equexit=1", "tool" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			executor,
			clock: clock,
			environmentProvider: name => {
				if ( "WATCH_INTERVAL" == name ) {
					return "0,3";
				}
				return null;
			}
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromMilliseconds( 300 ), clock.Delays[ 0 ] );
	}
	/// <summary>Verifies difference mode highlights changed visible cells.</summary>
	[Fact]
	public async Task DifferencesHighlightChangedVisibleCells() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 30, 4 ) );
		var result = await RunAsync(
			new[] { "--exec", "--no-title", "--differences", "--chgexit", "tool" },
			terminal,
			new FakeExecutor( Execution.Success( "alpha" ), Execution.Success( "alpHa" ) )
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 2, terminal.Frames.Count );
		Assert.Contains( "\u001b[7mH", terminal.Frames[ 1 ], StringComparison.Ordinal );
	}
	/// <summary>Verifies ANSI SGR sequences are retained only when color interpretation is enabled.</summary>
	[Fact]
	public async Task ColorOptionControlsAnsiStylePreservation() {
		var colored = new FakeTerminal( true, new TerminalDimensions( 30, 4 ) );
		var coloredResult = await RunAsync(
			new[] { "--exec", "--no-title", "--color", "--equexit=1", "tool" },
			colored,
			new FakeExecutor( Execution.Success( "\u001b[31mred\u001b[0m" ), Execution.Success( "\u001b[31mred\u001b[0m" ) )
		);
		Assert.Equal( 0, coloredResult.ExitCode );
		Assert.Contains( "\u001b[31m", colored.Frames[ 0 ], StringComparison.Ordinal );
		var plain = new FakeTerminal( true, new TerminalDimensions( 30, 4 ) );
		var plainResult = await RunAsync(
			new[] { "--exec", "--no-title", "--no-color", "--equexit=1", "tool" },
			plain,
			new FakeExecutor( Execution.Success( "\u001b[31mred\u001b[0m" ), Execution.Success( "\u001b[31mred\u001b[0m" ) )
		);
		Assert.Equal( 0, plainResult.ExitCode );
		Assert.DoesNotContain( "\u001b[31m", plain.Frames[ 0 ], StringComparison.Ordinal );
	}
	/// <summary>Verifies non-zero child status can beep and propagate through errexit.</summary>
	[Fact]
	public async Task BeepAndErrorExitPropagateChildStatus() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 40, 5 ) );
		var result = await RunAsync(
			new[] { "--exec", "--beep", "--errexit", "tool" },
			terminal,
			new FakeExecutor( Execution.Exit( 7, "failed" ) )
		);
		Assert.Equal( 7, result.ExitCode );
		Assert.Single( terminal.Frames );
		Assert.StartsWith( "\a", terminal.Frames[ 0 ], StringComparison.Ordinal );
	}
	/// <summary>Verifies equexit stops after the requested count of unchanged visible updates.</summary>
	[Fact]
	public async Task EqualExitCountsUnchangedVisibleCycles() {
		var executor = new FakeExecutor(
			Execution.Success( "same" ),
			Execution.Success( "same" ),
			Execution.Success( "same" )
		);
		var result = await RunAsync(
			new[] { "--exec", "--equexit=2", "tool" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			executor
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 3, executor.Options.Count );
	}
	/// <summary>Verifies chgexit ignores differences that occur outside the visible no-wrap area.</summary>
	[Fact]
	public async Task ChangeExitUsesVisibleOutputOnly() {
		var executor = new FakeExecutor(
			Execution.Success( "abcdeX" ),
			Execution.Success( "abcdeY" ),
			Execution.Success( "abcdZZ" )
		);
		var result = await RunAsync(
			new[] { "--exec", "--no-title", "--no-wrap", "--chgexit", "tool" },
			new FakeTerminal( true, new TerminalDimensions( 5, 3 ) ),
			executor
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 3, executor.Options.Count );
	}
	/// <summary>Verifies resize-driven geometry changes do not create a false chgexit event.</summary>
	[Fact]
	public async Task ResizeDoesNotCountAsVisibleChange() {
		var terminal = new FakeTerminal(
			true,
			new TerminalDimensions( 5, 3 ),
			new TerminalDimensions( 5, 3 ),
			new TerminalDimensions( 6, 3 ),
			new TerminalDimensions( 6, 3 )
		);
		var executor = new FakeExecutor(
			Execution.Success( "abcdeX" ),
			Execution.Success( "abcdeY" ),
			Execution.Success( "abcdZZ" )
		);
		var result = await RunAsync(
			new[] { "--exec", "--no-title", "--no-wrap", "--chgexit", "tool" },
			terminal,
			executor
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 3, executor.Options.Count );
	}
	/// <summary>Verifies no-rerun redraws the previous result after resize without launching a child.</summary>
	[Fact]
	public async Task NoRerunRedrawsPreviousOutputOnResize() {
		var terminal = new FakeTerminal(
			true,
			new TerminalDimensions( 10, 4 ),
			new TerminalDimensions( 10, 4 ),
			new TerminalDimensions( 12, 4 ),
			new TerminalDimensions( 12, 4 )
		);
		var executor = new FakeExecutor( Execution.Success( "same" ), Execution.Success( "same" ) );
		var result = await RunAsync(
			new[] { "--exec", "--no-rerun", "--equexit=1", "tool" },
			terminal,
			executor
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 2, executor.Options.Count );
		Assert.Equal( 3, terminal.Frames.Count );
	}
	/// <summary>Verifies child stdout and stderr share one display stream.</summary>
	[Fact]
	public async Task ChildStandardOutputAndErrorAreBothDisplayed() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 40, 5 ) );
		var result = await RunAsync(
			new[] { "--exec", "--errexit", "tool" },
			terminal,
			new FakeExecutor( new Execution( "out", "err", 3, TimeSpan.Zero ) )
		);
		Assert.Equal( 3, result.ExitCode );
		Assert.Contains( "out", terminal.Frames[ 0 ], StringComparison.Ordinal );
		Assert.Contains( "err", terminal.Frames[ 0 ], StringComparison.Ordinal );
	}
	/// <summary>Verifies the normal header and no-title mode.</summary>
	[Fact]
	public async Task HeaderCanBeSuppressed() {
		var titled = new FakeTerminal( true, new TerminalDimensions( 50, 5 ) );
		var titledResult = await RunAsync(
			new[] { "--exec", "--equexit=1", "tool" },
			titled,
			new FakeExecutor( Execution.Success( "same" ), Execution.Success( "same" ) )
		);
		Assert.Equal( 0, titledResult.ExitCode );
		Assert.Contains( "Every 2s: tool", titled.Frames[ 0 ], StringComparison.Ordinal );
		Assert.Contains( "host.test", titled.Frames[ 0 ], StringComparison.Ordinal );
		var untitled = new FakeTerminal( true, new TerminalDimensions( 50, 5 ) );
		var untitledResult = await RunAsync(
			new[] { "--exec", "--no-title", "--equexit=1", "tool" },
			untitled,
			new FakeExecutor( Execution.Success( "same" ), Execution.Success( "same" ) )
		);
		Assert.Equal( 0, untitledResult.ExitCode );
		Assert.DoesNotContain( "Every", untitled.Frames[ 0 ], StringComparison.Ordinal );
	}
	/// <summary>Verifies redirected output is rejected before a child is launched.</summary>
	[Fact]
	public async Task RedirectedOutputIsRejected() {
		var executor = new FakeExecutor( Execution.Success( "unused" ) );
		var result = await RunAsync(
			new[] { "--exec", "tool" },
			new FakeTerminal( false, new TerminalDimensions( 40, 5 ) ),
			executor
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Empty( executor.Options );
		Assert.Contains( "not a terminal", result.Stderr, StringComparison.Ordinal );
	}
	/// <summary>Verifies cancellation and suspend/resume still restore presentation state.</summary>
	[Fact]
	public async Task CancellationSuspendAndResumeRestoreTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 40, 5 ) );
		var signals = new FakeSignalSourceFactory( resumeOnce: true, suspendOnCreate: true );
		var result = await RunAsync(
			new[] { "--exec", "tool" },
			terminal,
			new FakeExecutor( Execution.Success( "first" ) ),
			clock: new CancelOnDelayClock(),
			signalFactory: signals
		);
		Assert.Equal( 130, result.ExitCode );
		Assert.Equal( 2, terminal.BeginCount );
		Assert.Equal( 1, terminal.SuspendRestoreCount );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
	}
	/// <summary>Verifies help/version and incompatible follow combinations without launching a child.</summary>
	[Fact]
	public async Task HelpVersionAndOptionErrorsAreControlled() {
		var help = await RunAsync(
			new[] { "--help" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			new FakeExecutor()
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "watch [options] command", help.Stdout, StringComparison.Ordinal );
		var version = await RunAsync(
			new[] { "--version" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			new FakeExecutor()
		);
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "4.0.6", version.Stdout, StringComparison.Ordinal );
		var invalid = await RunAsync(
			new[] { "--follow", "--chgexit", "tool" },
			new FakeTerminal( true, new TerminalDimensions( 40, 5 ) ),
			new FakeExecutor()
		);
		Assert.Equal( 1, invalid.ExitCode );
		Assert.Contains( "conflicts", invalid.Stderr, StringComparison.Ordinal );
	}
	private static async Task<RunResult> RunAsync(
		IReadOnlyList<string> args,
		FakeTerminal terminal,
		FakeExecutor executor,
		IMonotonicClock? clock = null,
		IProcFullScreenSignalSourceFactory? signalFactory = null,
		Func<string, string?>? environmentProvider = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( executor );
		using var stdout = new MemoryStream();
		using var stderr = new MemoryStream();
		var exitCode = await Command.RunAsync(
			args,
			stdout,
			stderr,
			processExecutor: executor,
			terminalFactory: new FakeTerminalFactory( terminal ),
			signalSourceFactory: signalFactory ?? new FakeSignalSourceFactory(),
			clock: clock ?? new FakeClock(),
			environmentVariableProvider: environmentProvider ?? EmptyEnvironment,
			wallClockProvider: static () => new DateTimeOffset( 2026, 8, 9, 4, 0, 0, TimeSpan.Zero ),
			hostNameProvider: static () => "host.test"
		);
		return new RunResult(
			exitCode,
			Encoding.UTF8.GetString( stdout.ToArray() ),
			Encoding.UTF8.GetString( stderr.ToArray() )
		);
	}
	private static string? EmptyEnvironment( string name ) {
		ArgumentNullException.ThrowIfNull( name );
		return null;
	}
	private sealed record RunResult( int ExitCode, string Stdout, string Stderr );
	private sealed record Execution( string Output, string Error, int Status, TimeSpan Elapsed ) {
		public static Execution Success( string output ) {
			ArgumentNullException.ThrowIfNull( output );
			return new Execution( output, string.Empty, 0, TimeSpan.Zero );
		}
		public static Execution Exit( int status, string output ) {
			ArgumentNullException.ThrowIfNull( output );
			return new Execution( output, string.Empty, status, TimeSpan.Zero );
		}
	}
	private sealed class FakeExecutor : IProcessExecutor {
		private readonly Queue<Execution> _executions;
		private readonly FakeClock? _clock;
		private readonly TimeSpan _advancePerRun;
		public List<ProcessRunOptions> Options { get; } = new();
		public FakeExecutor( params Execution[] executions ) {
			ArgumentNullException.ThrowIfNull( executions );
			this._executions = new Queue<Execution>( executions );
		}
		public FakeExecutor( FakeClock clock, TimeSpan advancePerRun, params Execution[] executions ) {
			ArgumentNullException.ThrowIfNull( clock );
			ArgumentNullException.ThrowIfNull( executions );
			this._clock = clock;
			this._advancePerRun = advancePerRun;
			this._executions = new Queue<Execution>( executions );
		}
		public async Task<ProcessResult> RunAsync( ProcessRunOptions options, CancellationToken cancellationToken = default ) {
			ArgumentNullException.ThrowIfNull( options );
			cancellationToken.ThrowIfCancellationRequested();
			this.Options.Add( options );
			if ( 0 == this._executions.Count ) {
				throw new InvalidOperationException( "No fake process execution remains." );
			}
			var execution = this._executions.Dequeue();
			if ( null != this._clock ) {
				this._clock.Advance( this._advancePerRun );
			}
			if ( null != options.StandardOutput && 0 < execution.Output.Length ) {
				var bytes = Encoding.UTF8.GetBytes( execution.Output );
				await options.StandardOutput.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
			}
			if ( null != options.StandardError && 0 < execution.Error.Length ) {
				var bytes = Encoding.UTF8.GetBytes( execution.Error );
				await options.StandardError.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
			}
			return ProcessResult.FromTermination(
				ProcessTermination.Exited( execution.Status ),
				elapsed: execution.Elapsed
			);
		}
	}
	private sealed class FakeTerminalFactory : IProcFullScreenTerminalFactory {
		private readonly FakeTerminal _terminal;
		public FakeTerminalFactory( FakeTerminal terminal ) {
			ArgumentNullException.ThrowIfNull( terminal );
			this._terminal = terminal;
		}
		public ValueTask<IProcFullScreenTerminal> OpenAsync(
			string? terminalPath,
			Stream? standardOutput,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Assert.Null( terminalPath );
			Assert.NotNull( standardOutput );
			return ValueTask.FromResult<IProcFullScreenTerminal>( this._terminal );
		}
	}
	private sealed class FakeTerminal : IProcFullScreenTerminal {
		private readonly Queue<TerminalDimensions> _dimensions;
		private TerminalDimensions _lastDimensions;
		public string DisplayName => "fake terminal";
		public bool IsInteractive { get; }
		public int BeginCount { get; private set; }
		public int RestoreCount { get; private set; }
		public int SuspendRestoreCount { get; private set; }
		public bool Disposed { get; private set; }
		public List<string> Frames { get; } = new();
		public FakeTerminal( bool interactive, params TerminalDimensions[] dimensions ) {
			ArgumentNullException.ThrowIfNull( dimensions );
			if ( 0 == dimensions.Length ) {
				throw new ArgumentException( "At least one terminal dimension is required.", nameof( dimensions ) );
			}
			this.IsInteractive = interactive;
			this._dimensions = new Queue<TerminalDimensions>( dimensions );
			this._lastDimensions = dimensions[ ^1 ];
		}
		public TerminalDimensions GetDimensions() {
			if ( 0 < this._dimensions.Count ) {
				this._lastDimensions = this._dimensions.Dequeue();
			}
			return this._lastDimensions;
		}
		public ValueTask BeginAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			this.BeginCount++;
			return ValueTask.CompletedTask;
		}
		public ValueTask WriteFrameAsync( string frame, CancellationToken cancellationToken = default ) {
			ArgumentNullException.ThrowIfNull( frame );
			cancellationToken.ThrowIfCancellationRequested();
			this.Frames.Add( frame );
			return ValueTask.CompletedTask;
		}
		public ValueTask RestoreAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			this.RestoreCount++;
			return ValueTask.CompletedTask;
		}
		public void RestoreForSuspend() {
			this.SuspendRestoreCount++;
		}
		public ValueTask DisposeAsync() {
			this.Disposed = true;
			return ValueTask.CompletedTask;
		}
	}
	private sealed class FakeSignalSourceFactory : IProcFullScreenSignalSourceFactory {
		private readonly bool _resumeOnce;
		private readonly bool _suspendOnCreate;
		public FakeSignalSourceFactory( bool resumeOnce = false, bool suspendOnCreate = false ) {
			this._resumeOnce = resumeOnce;
			this._suspendOnCreate = suspendOnCreate;
		}
		public IProcFullScreenSignalSource Create( Action restoreForSuspend ) {
			ArgumentNullException.ThrowIfNull( restoreForSuspend );
			if ( this._suspendOnCreate ) {
				restoreForSuspend();
			}
			return new FakeSignalSource( this._resumeOnce );
		}
	}
	private sealed class FakeSignalSource : IProcFullScreenSignalSource {
		private bool _resume;
		public CancellationToken TerminationToken => CancellationToken.None;
		public FakeSignalSource( bool resume ) {
			this._resume = resume;
		}
		public bool ConsumeResize() => false;
		public bool ConsumeResume() {
			var result = this._resume;
			this._resume = false;
			return result;
		}
		public void Dispose() {
		}
	}
	private class FakeClock : IMonotonicClock {
		private long _ticks;
		public List<TimeSpan> Delays { get; } = new();
		public long GetTimestamp() => this._ticks;
		public TimeSpan GetElapsedTime( long startingTimestamp, long endingTimestamp ) => TimeSpan.FromTicks( endingTimestamp - startingTimestamp );
		public virtual ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Delays.Add( delay );
			this.Advance( delay );
			return ValueTask.CompletedTask;
		}
		public void Advance( TimeSpan elapsed ) {
			if ( TimeSpan.Zero > elapsed ) {
				throw new ArgumentOutOfRangeException( nameof( elapsed ) );
			}
			this._ticks = checked( this._ticks + elapsed.Ticks );
		}
	}
	private sealed class CancelOnDelayClock : FakeClock {
		public override ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) {
			throw new OperationCanceledException( cancellationToken );
		}
	}
}
