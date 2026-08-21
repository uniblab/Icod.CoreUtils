namespace Icod.ProcPs.HugeTop.Tests;

using System.Runtime.CompilerServices;
using System.Text;
using Icod.CommandFramework.Host;
using Icod.CommandFramework.Terminal;
using Icod.CommandFramework.Time;
using Icod.ProcPs.Shared;
using Xunit;
/// <summary>Exercises Batch 67 procps-ng 4.0.6 hugetop compatibility and lifecycle behavior.</summary>
public sealed class HugeTopCommandTests {
	/// <summary>Verifies one-shot output aggregates nodes and sorts process huge-page users.</summary>
	[Fact]
	public async Task OnceReportAggregatesNodesAndRendersProcesses() {
		var result = await RunOnceAsync( new[] { "--once" }, AvailableSnapshot() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "node(s): 2.0Mi - 3/7", result.Stdout, StringComparison.Ordinal );
		Assert.Contains( "42", result.Stdout, StringComparison.Ordinal );
		Assert.Contains( "server", result.Stdout, StringComparison.Ordinal );
		Assert.Contains( "worker", result.Stdout, StringComparison.Ordinal );
		Assert.True( result.Stdout.IndexOf( "server", StringComparison.Ordinal ) < result.Stdout.IndexOf( "worker", StringComparison.Ordinal ) );
		Assert.Equal( string.Empty, result.Stderr );
	}
	/// <summary>Verifies NUMA and human-readable modes preserve per-node pool information.</summary>
	[Fact]
	public async Task NumaAndHumanModesRenderPerNodePools() {
		var result = await RunOnceAsync( new[] { "--once", "--numa", "--human" }, AvailableSnapshot() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "node0: 2.0Mi - 2/4", result.Stdout, StringComparison.Ordinal );
		Assert.Contains( "node1: 2.0Mi - 1/3", result.Stdout, StringComparison.Ordinal );
		Assert.Contains( "1.0Mi", result.Stdout, StringComparison.Ordinal );
	}
	/// <summary>Verifies interactive mode uses the three-second default cadence and restores the terminal.</summary>
	[Fact]
	public async Task InteractiveModeRefreshesAndRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 12 ) );
		var scheduler = new FiniteScheduler( 1 );
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, scheduler, AvailableSnapshot() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromSeconds( 3d ), scheduler.LastInterval );
		Assert.Single( terminal.Frames );
		Assert.Contains( "node(s):", terminal.Frames[ 0 ], StringComparison.Ordinal );
		Assert.Equal( 1, terminal.BeginCount );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
	}
	/// <summary>Verifies delay selection reaches the monotonic scheduler.</summary>
	[Fact]
	public async Task DelayOptionControlsRefreshCadence() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 12 ) );
		var scheduler = new FiniteScheduler( 1 );
		var result = await RunInteractiveAsync( new[] { "--delay", "7" }, terminal, scheduler, AvailableSnapshot() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromSeconds( 7d ), scheduler.LastInterval );
	}
	/// <summary>Verifies geometry changes are reflected in subsequent complete frames.</summary>
	[Fact]
	public async Task ResizeUsesUpdatedTerminalGeometry() {
		var terminal = new FakeTerminal(
			true,
			new TerminalDimensions( 60, 10 ),
			new TerminalDimensions( 60, 10 ),
			new TerminalDimensions( 70, 11 )
		);
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, new FiniteScheduler( 2 ), AvailableSnapshot(), AvailableSnapshot() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 2, terminal.Frames.Count );
		Assert.Equal( ( 60 * 10 ) + ( 10 - 1 ) * Environment.NewLine.Length, terminal.Frames[ 0 ].Length );
		Assert.Equal( ( 70 * 11 ) + ( 11 - 1 ) * Environment.NewLine.Length, terminal.Frames[ 1 ].Length );
	}
	/// <summary>Verifies full-screen mode rejects redirected output while one-shot mode remains available.</summary>
	[Fact]
	public async Task RedirectedInteractiveOutputIsRejected() {
		var terminal = new FakeTerminal( false, new TerminalDimensions( 80, 25 ) );
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, new FiniteScheduler( 1 ), AvailableSnapshot() );
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "use --once", result.Stderr, StringComparison.Ordinal );
		Assert.Empty( terminal.Frames );
	}
	/// <summary>Verifies unsupported kernel interfaces produce a controlled diagnostic.</summary>
	[Fact]
	public async Task UnsupportedProviderReturnsControlledFailure() {
		var unavailable = ProcObservedValue<ProcHugePageSnapshot>.Missing(
			ProcObservationAvailability.Unsupported,
			"huge pages are unavailable"
		);
		var result = await RunOnceAsync( new[] { "--once" }, unavailable );
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "huge pages are unavailable", result.Stderr, StringComparison.Ordinal );
	}
	/// <summary>Verifies the huge-page system provider reports an explicit unsupported capability outside Linux.</summary>
	[Fact]
	public async Task SystemProviderIsExplicitlyUnsupportedOutsideLinux() {
		if ( OperatingSystem.IsLinux() ) {
			return;
		}
		var observed = await SystemProcHugePageProvider.Instance.GetSnapshotAsync();
		Assert.False( observed.HasValue );
		Assert.Equal( ProcObservationAvailability.Unsupported, observed.Availability );
	}
	/// <summary>Verifies cancellation returns the conventional status and restores the terminal.</summary>
	[Fact]
	public async Task CancellationRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 80, 12 ) );
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, new FiniteScheduler( 1, cancelAfterTicks: true ), AvailableSnapshot() );
		Assert.Equal( 130, result.ExitCode );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
	}
	/// <summary>Verifies help, version, invalid delays, and extra operands receive deterministic results.</summary>
	[Fact]
	public async Task CommandLineValidationIsDeterministic() {
		var help = await RunOnceAsync( new[] { "--help" }, AvailableSnapshot() );
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage:", help.Stdout, StringComparison.Ordinal );
		var version = await RunOnceAsync( new[] { "--version" }, AvailableSnapshot() );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "procps-ng 4.0.6", version.Stdout, StringComparison.Ordinal );
		var delay = await RunOnceAsync( new[] { "--delay=0" }, AvailableSnapshot() );
		Assert.Equal( 1, delay.ExitCode );
		Assert.Contains( "delay must be positive", delay.Stderr, StringComparison.Ordinal );
		var operand = await RunOnceAsync( new[] { "unexpected" }, AvailableSnapshot() );
		Assert.Equal( 1, operand.ExitCode );
		Assert.Contains( "unexpected operand", operand.Stderr, StringComparison.Ordinal );
	}
	private static ProcObservedValue<ProcHugePageSnapshot> AvailableSnapshot() {
		return ProcObservedValue<ProcHugePageSnapshot>.Available(
			new ProcHugePageSnapshot(
				new[] {
					new ProcHugePageNode( 0, new[] { new ProcHugePagePool( 2UL * 1024UL * 1024UL, 4UL, 2UL ) } ),
					new ProcHugePageNode( 1, new[] { new ProcHugePagePool( 2UL * 1024UL * 1024UL, 3UL, 1UL ) } )
				},
				new[] {
					new ProcHugePageProcess( 7, "worker", 512UL * 1024UL, 0UL ),
					new ProcHugePageProcess( 42, "server", 1024UL * 1024UL, 2UL * 1024UL * 1024UL )
				}
			),
			ProcObservationSource.LinuxSysfs,
			ObservationFidelity.Exact
		);
	}
	private static async Task<RunResult> RunOnceAsync( IReadOnlyList<string> args, ProcObservedValue<ProcHugePageSnapshot> snapshot ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( snapshot );
		await using var stdout = new MemoryStream();
		await using var stderr = new MemoryStream();
		var exitCode = await Command.RunAsync(
			args,
			stdout,
			stderr,
			new FakeHugePageProvider( snapshot ),
			wallClockProvider: FixedTime
		);
		return new RunResult( exitCode, Encoding.UTF8.GetString( stdout.ToArray() ), Encoding.UTF8.GetString( stderr.ToArray() ) );
	}
	private static async Task<RunResult> RunInteractiveAsync(
		IReadOnlyList<string> args,
		FakeTerminal terminal,
		FiniteScheduler scheduler,
		params ProcObservedValue<ProcHugePageSnapshot>[] snapshots
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( scheduler );
		ArgumentNullException.ThrowIfNull( snapshots );
		await using var stdout = new MemoryStream();
		await using var stderr = new MemoryStream();
		var sampler = new ProcSampler( new FakeClock(), scheduler );
		var exitCode = await Command.RunAsync(
			args,
			stdout,
			stderr,
			new FakeHugePageProvider( snapshots ),
			sampler,
			new FakeTerminalFactory( terminal ),
			new FakeSignalSourceFactory(),
			FixedTime
		);
		return new RunResult( exitCode, Encoding.UTF8.GetString( stdout.ToArray() ), Encoding.UTF8.GetString( stderr.ToArray() ) );
	}
	private static DateTimeOffset FixedTime() {
		return new DateTimeOffset( 2026, 8, 9, 12, 34, 56, TimeSpan.Zero );
	}
	private sealed record RunResult( int ExitCode, string Stdout, string Stderr );
	private sealed class FakeHugePageProvider : IProcHugePageProvider {
		private readonly Queue<ProcObservedValue<ProcHugePageSnapshot>> _values;
		private ProcObservedValue<ProcHugePageSnapshot> _last;
		public FakeHugePageProvider( params ProcObservedValue<ProcHugePageSnapshot>[] values ) {
			ArgumentNullException.ThrowIfNull( values );
			if ( 0 == values.Length ) {
				throw new ArgumentException( "At least one snapshot is required.", nameof( values ) );
			}
			this._values = new Queue<ProcObservedValue<ProcHugePageSnapshot>>( values );
			this._last = values[ ^1 ];
		}
		/// <inheritdoc />
		public Task<ProcObservedValue<ProcHugePageSnapshot>> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( 0 < this._values.Count ) {
				this._last = this._values.Dequeue();
			}
			return Task.FromResult( this._last );
		}
	}
	private sealed class FakeClock : IMonotonicClock {
		private long _timestamp;
		/// <inheritdoc />
		public long GetTimestamp() {
			return Interlocked.Increment( ref this._timestamp );
		}
		/// <inheritdoc />
		public TimeSpan GetElapsedTime( long startingTimestamp, long endingTimestamp ) {
			return TimeSpan.FromSeconds( Math.Max( 0L, endingTimestamp - startingTimestamp ) );
		}
		/// <inheritdoc />
		public ValueTask DelayAsync( TimeSpan delay, CancellationToken cancellationToken = default ) {
			if ( TimeSpan.Zero > delay ) {
				throw new ArgumentOutOfRangeException( nameof( delay ) );
			}
			cancellationToken.ThrowIfCancellationRequested();
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
		/// <inheritdoc />
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
				yield return new PeriodicTick( index, interval * index, interval * index );
				await Task.Yield();
			}
			if ( this._cancelAfterTicks ) {
				throw new OperationCanceledException();
			}
		}
	}
	private sealed class FakeTerminalFactory : IProcFullScreenTerminalFactory {
		private readonly FakeTerminal _terminal;
		public FakeTerminalFactory( FakeTerminal terminal ) {
			ArgumentNullException.ThrowIfNull( terminal );
			this._terminal = terminal;
		}
		/// <inheritdoc />
		public ValueTask<IProcFullScreenTerminal> OpenAsync( string? terminalPath, Stream? standardOutput, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult<IProcFullScreenTerminal>( this._terminal );
		}
	}
	private sealed class FakeSignalSourceFactory : IProcFullScreenSignalSourceFactory {
		/// <inheritdoc />
		public IProcFullScreenSignalSource Create( Action restoreForSuspend ) {
			ArgumentNullException.ThrowIfNull( restoreForSuspend );
			return new FakeSignalSource();
		}
	}
	private sealed class FakeSignalSource : IProcFullScreenSignalSource {
		/// <inheritdoc />
		public CancellationToken TerminationToken => CancellationToken.None;
		/// <inheritdoc />
		public bool ConsumeResize() {
			return false;
		}
		/// <inheritdoc />
		public bool ConsumeResume() {
			return false;
		}
		/// <inheritdoc />
		public void Dispose() {
		}
	}
	private sealed class FakeTerminal : IProcFullScreenTerminal {
		private readonly Queue<TerminalDimensions> _dimensions;
		private TerminalDimensions _lastDimensions;
		public string DisplayName => "fake";
		public bool IsInteractive { get; }
		public List<string> Frames { get; } = new();
		public int BeginCount { get; private set; }
		public int RestoreCount { get; private set; }
		public bool Disposed { get; private set; }
		public FakeTerminal( bool interactive, params TerminalDimensions[] dimensions ) {
			ArgumentNullException.ThrowIfNull( dimensions );
			if ( 0 == dimensions.Length ) {
				throw new ArgumentException( "At least one dimension is required.", nameof( dimensions ) );
			}
			this.IsInteractive = interactive;
			this._dimensions = new Queue<TerminalDimensions>( dimensions );
			this._lastDimensions = dimensions[ ^1 ];
		}
		/// <inheritdoc />
		public TerminalDimensions GetDimensions() {
			if ( 0 < this._dimensions.Count ) {
				this._lastDimensions = this._dimensions.Dequeue();
			}
			return this._lastDimensions;
		}
		/// <inheritdoc />
		public ValueTask BeginAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			this.BeginCount++;
			return ValueTask.CompletedTask;
		}
		/// <inheritdoc />
		public ValueTask WriteFrameAsync( string frame, CancellationToken cancellationToken = default ) {
			ArgumentNullException.ThrowIfNull( frame );
			cancellationToken.ThrowIfCancellationRequested();
			this.Frames.Add( frame );
			return ValueTask.CompletedTask;
		}
		/// <inheritdoc />
		public ValueTask RestoreAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			this.RestoreCount++;
			return ValueTask.CompletedTask;
		}
		/// <inheritdoc />
		public void RestoreForSuspend() {
		}
		/// <inheritdoc />
		public ValueTask DisposeAsync() {
			this.Disposed = true;
			return ValueTask.CompletedTask;
		}
	}
}
