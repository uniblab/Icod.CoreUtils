namespace Icod.ProcPs.Tload.Tests;

using System.Runtime.CompilerServices;
using System.Text;
using Icod.CommandFramework.Host;
using Icod.CommandFramework.Terminal;
using Icod.CommandFramework.Time;
using Icod.ProcPs.Shared;
using Xunit;

/// <summary>Exercises the Batch 65 procps-ng 4.0.6 <c>tload</c> compatibility and lifecycle behavior.</summary>
public sealed class TloadCommandTests {
	/// <summary>Verifies default rendering, cadence, and terminal restoration.</summary>
	[Fact]
	public async Task DefaultReportRendersLoadGraphAndRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 40, 10 ) );
		var scheduler = new FiniteScheduler( 1 );
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			scheduler,
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) )
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromSeconds( 5d ), scheduler.LastInterval );
		Assert.Single( terminal.Frames );
		Assert.Contains( " 0.50, 0.25, 0.10", terminal.Frames[ 0 ], StringComparison.Ordinal );
		Assert.Contains( '*', terminal.Frames[ 0 ] );
		Assert.Equal( 1, terminal.BeginCount );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
		Assert.Equal( string.Empty, result.Stderr );
	}

	/// <summary>Verifies delay and vertical-scale options affect sampling and rendering.</summary>
	[Fact]
	public async Task DelayAndScaleControlsAreApplied() {
		var dimensions = new TerminalDimensions( 40, 10 );
		var terminal = new FakeTerminal( true, dimensions );
		var scheduler = new FiniteScheduler( 1 );
		var result = await RunAsync(
			new[] { "--delay", "2", "--scale=2" },
			terminal,
			scheduler,
			new FakeMetricsProvider( Available( 1d, 0.5d, 0.25d ) )
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromSeconds( 2d ), scheduler.LastInterval );
		var frame = terminal.Frames[ 0 ];
		Assert.Equal( '*', frame[ ( dimensions.Height - 1 ) * dimensions.Width ] );
		Assert.Equal( '=', frame[ ( dimensions.Height - 2 ) * dimensions.Width ] );
	}

	/// <summary>Verifies an explicit terminal operand is routed to the terminal factory.</summary>
	[Fact]
	public async Task SelectedTerminalOperandIsPassedToFactory() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 40, 10 ) );
		var factory = new FakeTerminalFactory( terminal );
		var result = await RunAsync(
			new[] { "/dev/pts/7" },
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) ),
			factory
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( "/dev/pts/7", factory.TerminalPath );
	}

	/// <summary>Verifies geometry changes reset the scrolling graph and use the new dimensions.</summary>
	[Fact]
	public async Task ResizeClearsAndRendersAtNewGeometry() {
		var terminal = new FakeTerminal(
			true,
			new TerminalDimensions( 10, 4 ),
			new TerminalDimensions( 10, 4 ),
			new TerminalDimensions( 12, 5 )
		);
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			new FiniteScheduler( 2 ),
			new FakeMetricsProvider(
				Available( 0.5d, 0.25d, 0.1d ),
				Available( 0.75d, 0.4d, 0.2d )
			)
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 2, terminal.Frames.Count );
		Assert.Equal( 39, terminal.Frames[ 0 ].Length );
		Assert.Equal( 59, terminal.Frames[ 1 ].Length );
	}

	/// <summary>Verifies redirected standard output is rejected without an explicit terminal.</summary>
	[Fact]
	public async Task RedirectedStandardOutputIsRejected() {
		var terminal = new FakeTerminal( false, new TerminalDimensions( 80, 25 ) );
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) )
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "standard output is not a terminal", result.Stderr, StringComparison.Ordinal );
		Assert.Empty( terminal.Frames );
	}

	/// <summary>Verifies unavailable load averages produce a controlled diagnostic.</summary>
	[Fact]
	public async Task MissingLoadAverageIsControlledFailure() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 25 ) );
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider(
				ProcObservedValue<ProcLoadAverages>.Missing(
					ProcObservationAvailability.Unsupported,
					"load averages are unsupported"
				)
			)
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "load averages are unsupported", result.Stderr, StringComparison.Ordinal );
		Assert.Equal( 1, terminal.RestoreCount );
	}

	/// <summary>Verifies cancellation returns the command cancellation status and restores the terminal.</summary>
	[Fact]
	public async Task CancellationRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 25 ) );
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			new FiniteScheduler( 1, cancelAfterTicks: true ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) )
		);
		Assert.Equal( 130, result.ExitCode );
		Assert.Single( terminal.Frames );
		Assert.Equal( 1, terminal.RestoreCount );
	}


	/// <summary>Verifies frame-write failures remain controlled and still restore the terminal.</summary>
	[Fact]
	public async Task WriteFailureRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 25 ) ) {
			WriteFailure = new IOException( "simulated write failure" )
		};
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) )
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "simulated write failure", result.Stderr, StringComparison.Ordinal );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
	}

	/// <summary>Verifies suspension restoration and resume re-entry are coordinated through the signal source.</summary>
	[Fact]
	public async Task SuspendAndResumeLifecycleRestoresAndReentersPresentation() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 25 ) );
		var signalFactory = new FakeSignalSourceFactory( suspendOnCreate: true, resumeOnce: true );
		var result = await RunAsync(
			Array.Empty<string>(),
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) ),
			signalFactory: signalFactory
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 1, terminal.SuspendRestoreCount );
		Assert.Equal( 2, terminal.BeginCount );
	}

	/// <summary>Verifies invalid option values produce deterministic failures.</summary>
	/// <param name="option">Option token under test.</param>
	/// <param name="value">Optional option value.</param>
	/// <param name="expected">Expected diagnostic fragment.</param>
	[Theory]
	[InlineData( "-d", "0", "delay must be positive integer" )]
	[InlineData( "-d", "-1", "delay must be positive integer" )]
	[InlineData( "-d", "4294967296", "too large delay value" )]
	[InlineData( "-s", "-1", "scale cannot be negative" )]
	[InlineData( "--unknown", null, "unrecognized option" )]
	public async Task InvalidOptionsReturnFailure( string option, string? value, string expected ) {
		ArgumentNullException.ThrowIfNull( option );
		ArgumentNullException.ThrowIfNull( expected );
		string[] args;
		if ( null == value ) {
			args = new[] { option };
		} else {
			args = new[] { option, value };
		}
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 25 ) );
		var result = await RunAsync(
			args,
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) )
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( expected, result.Stderr, StringComparison.Ordinal );
		Assert.Equal( 0, terminal.BeginCount );
	}

	/// <summary>Verifies help and version complete without opening or mutating a terminal.</summary>
	[Fact]
	public async Task HelpAndVersionDoNotOpenTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 25 ) );
		var factory = new FakeTerminalFactory( terminal );
		var help = await RunAsync(
			new[] { "--help" },
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) ),
			factory
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage:", help.Stdout, StringComparison.Ordinal );
		Assert.Equal( 0, factory.OpenCount );
		var version = await RunAsync(
			new[] { "--version" },
			terminal,
			new FiniteScheduler( 1 ),
			new FakeMetricsProvider( Available( 0.5d, 0.25d, 0.1d ) ),
			factory
		);
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "procps-ng 4.0.6", version.Stdout, StringComparison.Ordinal );
		Assert.Equal( 0, factory.OpenCount );
	}

	private static ProcObservedValue<ProcLoadAverages> Available( double one, double five, double fifteen ) {
		return ProcObservedValue<ProcLoadAverages>.Available(
			new ProcLoadAverages( one, five, fifteen ),
			ProcObservationSource.PlatformApi,
			ObservationFidelity.Equivalent
		);
	}

	private static async Task<RunResult> RunAsync(
		IReadOnlyList<string> args,
		FakeTerminal terminal,
		FiniteScheduler scheduler,
		FakeMetricsProvider metrics,
		FakeTerminalFactory? terminalFactory = null,
		FakeSignalSourceFactory? signalFactory = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( scheduler );
		ArgumentNullException.ThrowIfNull( metrics );
		await using var stdout = new MemoryStream();
		await using var stderr = new MemoryStream();
		terminalFactory ??= new FakeTerminalFactory( terminal );
		signalFactory ??= new FakeSignalSourceFactory();
		var sampler = new ProcSampler( new FakeClock(), scheduler );
		var exitCode = await Command.RunAsync(
			args,
			stdout,
			stderr,
			metrics,
			sampler,
			terminalFactory,
			signalFactory
		);
		return new RunResult(
			exitCode,
			Encoding.UTF8.GetString( stdout.ToArray() ),
			Encoding.UTF8.GetString( stderr.ToArray() )
		);
	}

	private sealed record RunResult( int ExitCode, string Stdout, string Stderr );

	private sealed class FakeClock : IMonotonicClock {
		private long _timestamp;

		public long GetTimestamp() => Interlocked.Increment( ref this._timestamp );
		public TimeSpan GetElapsedTime( long startingTimestamp, long endingTimestamp ) {
			return TimeSpan.FromSeconds( Math.Max( 0L, endingTimestamp - startingTimestamp ) );
		}
		public ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) {
			if ( TimeSpan.Zero > delay ) {
				throw new ArgumentOutOfRangeException( nameof( delay ) );
			}
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Add( ref this._timestamp, Math.Max( 1L, (long)Math.Ceiling( delay.TotalSeconds ) ) );
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FiniteScheduler : IPeriodicScheduler {
		private readonly int _ticks;
		private readonly bool _cancelAfterTicks;
		public TimeSpan LastInterval { get; private set; }

		public FiniteScheduler( int ticks, bool cancelAfterTicks = false ) {
			ArgumentOutOfRangeException.ThrowIfNegative( ticks );
			this._ticks = ticks;
			this._cancelAfterTicks = cancelAfterTicks;
		}

		public async IAsyncEnumerable<PeriodicTick> ScheduleAsync(
			TimeSpan interval,
			bool fireImmediately = false,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		) {
			if ( TimeSpan.Zero >= interval ) {
				throw new ArgumentOutOfRangeException( nameof( interval ) );
			}
			this.LastInterval = interval;
			for ( var index = 0; index < this._ticks; index++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				yield return new PeriodicTick(
					index,
					TimeSpan.FromTicks( interval.Ticks * index ),
					TimeSpan.FromTicks( interval.Ticks * index )
				);
				await Task.Yield();
			}
			if ( this._cancelAfterTicks ) {
				throw new OperationCanceledException();
			}
		}
	}

	private sealed class FakeMetricsProvider : IProcSystemMetricsProvider {
		private readonly Queue<ProcObservedValue<ProcLoadAverages>> _values;
		private ProcObservedValue<ProcLoadAverages> _last;
		public ProcSystemCapabilities Capabilities => ProcSystemCapabilities.LoadAverage;

		public FakeMetricsProvider( params ProcObservedValue<ProcLoadAverages>[] values ) {
			ArgumentNullException.ThrowIfNull( values );
			if ( 0 == values.Length ) {
				throw new ArgumentException( "At least one observation is required.", nameof( values ) );
			}
			this._values = new Queue<ProcObservedValue<ProcLoadAverages>>( values );
			this._last = values[ ^1 ];
		}

		public Task<ProcSystemSnapshot> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( 0 < this._values.Count ) {
				this._last = this._values.Dequeue();
			}
			return Task.FromResult(
				new ProcSystemSnapshot {
					LoadAverages = this._last
				}
			);
		}
	}

	private sealed class FakeTerminalFactory : IProcFullScreenTerminalFactory {
		private readonly FakeTerminal _terminal;
		public int OpenCount { get; private set; }
		public string? TerminalPath { get; private set; }

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
			this.OpenCount++;
			this.TerminalPath = terminalPath;
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
		public IOException? WriteFailure { get; init; }
		public List<string> Frames { get; } = new();

		public FakeTerminal( bool interactive, params TerminalDimensions[] dimensions ) {
			ArgumentNullException.ThrowIfNull( dimensions );
			if ( 0 == dimensions.Length ) {
				throw new ArgumentException( "At least one dimension is required.", nameof( dimensions ) );
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
			var writeFailure = this.WriteFailure;
			if ( null != writeFailure ) {
				throw writeFailure;
			}
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
		private readonly bool _suspendOnCreate;
		private readonly bool _resumeOnce;

		public FakeSignalSourceFactory( bool suspendOnCreate = false, bool resumeOnce = false ) {
			this._suspendOnCreate = suspendOnCreate;
			this._resumeOnce = resumeOnce;
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
		private int _resumePending;
		public CancellationToken TerminationToken => CancellationToken.None;

		public FakeSignalSource( bool resumeOnce ) {
			this._resumePending = ( resumeOnce )
				? 1
				: 0
			;
		}

		public bool ConsumeResize() => false;
		public bool ConsumeResume() => 0 != Interlocked.Exchange( ref this._resumePending, 0 );
		public void Dispose() {
		}
	}
}
