namespace Icod.ProcPs.SlabTop.Tests;

using System.Runtime.CompilerServices;
using System.Text;
using Icod.CommandFramework.Host;
using Icod.CommandFramework.Terminal;
using Icod.Timing;
using Icod.ProcPs.Shared;
using Xunit;
/// <summary>Exercises Batch 67 procps-ng 4.0.6 slabtop compatibility and lifecycle behavior.</summary>
public sealed class SlabTopCommandTests {
	/// <summary>Verifies one-shot output contains summary data and defaults to total-object sorting.</summary>
	[Fact]
	public async Task OnceReportRendersSummaryAndDefaultSort() {
		var result = await RunOnceAsync( new[] { "--once" }, AvailableSlabs() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "Active / Total Objects", result.Stdout, StringComparison.Ordinal );
		Assert.Contains( "CACHE SIZE NAME", result.Stdout, StringComparison.Ordinal );
		Assert.True( result.Stdout.IndexOf( "large_cache", StringComparison.Ordinal ) < result.Stdout.IndexOf( "small_cache", StringComparison.Ordinal ) );
		Assert.Equal( string.Empty, result.Stderr );
	}
	/// <summary>Verifies name sorting and human-readable size presentation.</summary>
	[Fact]
	public async Task SortAndHumanOptionsAreApplied() {
		var result = await RunOnceAsync( new[] { "--once", "--sort=n", "--human" }, AvailableSlabs() );
		Assert.Equal( 0, result.ExitCode );
		Assert.True( result.Stdout.IndexOf( "large_cache", StringComparison.Ordinal ) < result.Stdout.IndexOf( "small_cache", StringComparison.Ordinal ) );
		Assert.Contains( "1.0Ki", result.Stdout, StringComparison.Ordinal );
	}
	/// <summary>Verifies all documented sort letters are accepted deterministically.</summary>
	[Theory]
	[InlineData( "a" )]
	[InlineData( "b" )]
	[InlineData( "c" )]
	[InlineData( "l" )]
	[InlineData( "v" )]
	[InlineData( "n" )]
	[InlineData( "o" )]
	[InlineData( "p" )]
	[InlineData( "s" )]
	[InlineData( "u" )]
	public async Task DocumentedSortCriteriaAreAccepted( string criterion ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( criterion );
		var result = await RunOnceAsync( new[] { "--once", $"--sort={criterion}" }, AvailableSlabs() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "large_cache", result.Stdout, StringComparison.Ordinal );
	}
	/// <summary>Verifies the exact slabdata active and total slab counts are parsed from slabinfo.</summary>
	[Fact]
	public void SlabInfoParserUsesKernelSlabDataCounts() {
		var text = string.Join(
			Environment.NewLine,
			"slabinfo - version: 2.1",
			"cache_a 10 20 64 8 1 : tunables 0 0 0 : slabdata 2 4 0",
			string.Empty
		);
		var entries = ProcKernelMemoryParsers.ParseSlabInfo( text );
		var entry = Assert.Single( entries );
		Assert.Equal( 2UL, entry.ActiveSlabs );
		Assert.Equal( 4UL, entry.TotalSlabs );
	}
	/// <summary>Verifies missing slabdata is rejected instead of being approximated.</summary>
	[Fact]
	public void SlabInfoParserRejectsMissingSlabData() {
		const string text = "cache_a 10 20 64 8 1";
		Assert.Throws<FormatException>( () => ProcKernelMemoryParsers.ParseSlabInfo( text ) );
	}
	/// <summary>Verifies interactive mode uses the three-second default cadence and restores the terminal.</summary>
	[Fact]
	public async Task InteractiveModeRefreshesAndRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 90, 14 ) );
		var scheduler = new FiniteScheduler( 1 );
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, scheduler, AvailableSlabs() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromSeconds( 3d ), scheduler.LastInterval );
		Assert.Single( terminal.Frames );
		Assert.Contains( "Active / Total Objects", terminal.Frames[ 0 ], StringComparison.Ordinal );
		Assert.Equal( 1, terminal.BeginCount );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
	}
	/// <summary>Verifies the delay option controls the monotonic refresh cadence.</summary>
	[Fact]
	public async Task DelayOptionControlsRefreshCadence() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 90, 14 ) );
		var scheduler = new FiniteScheduler( 1 );
		var result = await RunInteractiveAsync( new[] { "--delay=5" }, terminal, scheduler, AvailableSlabs() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( TimeSpan.FromSeconds( 5d ), scheduler.LastInterval );
	}
	/// <summary>Verifies geometry changes are reflected in subsequent full-screen frames.</summary>
	[Fact]
	public async Task ResizeUsesUpdatedTerminalGeometry() {
		var terminal = new FakeTerminal(
			true,
			new TerminalDimensions( 80, 12 ),
			new TerminalDimensions( 80, 12 ),
			new TerminalDimensions( 100, 15 )
		);
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, new FiniteScheduler( 2 ), AvailableSlabs(), AvailableSlabs() );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( 2, terminal.Frames.Count );
		Assert.Equal( ( 80 * 12 ) + ( 11 * Environment.NewLine.Length ), terminal.Frames[ 0 ].Length );
		Assert.Equal( ( 100 * 15 ) + ( 14 * Environment.NewLine.Length ), terminal.Frames[ 1 ].Length );
	}
	/// <summary>Verifies redirected full-screen output is rejected while one-shot output remains supported.</summary>
	[Fact]
	public async Task RedirectedInteractiveOutputIsRejected() {
		var terminal = new FakeTerminal( false, new TerminalDimensions( 80, 25 ) );
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, new FiniteScheduler( 1 ), AvailableSlabs() );
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "use --once", result.Stderr, StringComparison.Ordinal );
		Assert.Empty( terminal.Frames );
	}
	/// <summary>Verifies unsupported kernel interfaces produce a controlled diagnostic.</summary>
	[Fact]
	public async Task UnsupportedProviderReturnsControlledFailure() {
		var unavailable = ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing(
			ProcObservationAvailability.Unsupported,
			"slabinfo is unavailable"
		);
		var result = await RunOnceAsync( new[] { "--once" }, unavailable );
		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "slabinfo is unavailable", result.Stderr, StringComparison.Ordinal );
	}
	/// <summary>Verifies the slab system provider reports an explicit unsupported capability outside Linux.</summary>
	[Fact]
	public async Task SystemProviderIsExplicitlyUnsupportedOutsideLinux() {
		if ( OperatingSystem.IsLinux() ) {
			return;
		}
		var observed = await SystemProcSlabProvider.Instance.GetSlabsAsync();
		Assert.False( observed.HasValue );
		Assert.Equal( ProcObservationAvailability.Unsupported, observed.Availability );
	}
	/// <summary>Verifies cancellation returns the conventional status and restores the terminal.</summary>
	[Fact]
	public async Task CancellationRestoresTerminal() {
		var terminal = new FakeTerminal( true, new TerminalDimensions( 90, 14 ) );
		var result = await RunInteractiveAsync( Array.Empty<string>(), terminal, new FiniteScheduler( 1, cancelAfterTicks: true ), AvailableSlabs() );
		Assert.Equal( 130, result.ExitCode );
		Assert.Equal( 1, terminal.RestoreCount );
		Assert.True( terminal.Disposed );
	}
	/// <summary>Verifies command-line conflicts and help/version behavior.</summary>
	[Fact]
	public async Task CommandLineValidationIsDeterministic() {
		var conflict = await RunOnceAsync( new[] { "--delay", "2", "--once" }, AvailableSlabs() );
		Assert.Equal( 1, conflict.ExitCode );
		Assert.Contains( "Cannot combine -d and -o", conflict.Stderr, StringComparison.Ordinal );
		var help = await RunOnceAsync( new[] { "--help" }, AvailableSlabs() );
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Valid sort criteria", help.Stdout, StringComparison.Ordinal );
		var version = await RunOnceAsync( new[] { "--version" }, AvailableSlabs() );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "procps-ng 4.0.6", version.Stdout, StringComparison.Ordinal );
	}
	private static ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>> AvailableSlabs() {
		IReadOnlyList<ProcSlabCacheEntry> entries = new[] {
			new ProcSlabCacheEntry( "small_cache", 12UL, 20UL, 64UL, 8UL, 1UL, 2UL, 3UL ),
			new ProcSlabCacheEntry( "large_cache", 40UL, 80UL, 1024UL, 4UL, 2UL, 15UL, 20UL )
		};
		return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Available(
			entries,
			ProcObservationSource.LinuxProcfs,
			ObservationFidelity.Exact
		);
	}
	private static async Task<RunResult> RunOnceAsync(
		IReadOnlyList<string> args,
		ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>> slabs
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( slabs );
		await using var stdout = new MemoryStream();
		await using var stderr = new MemoryStream();
		var exitCode = await Command.RunAsync( args, stdout, stderr, new FakeSlabProvider( slabs ) );
		return new RunResult( exitCode, Encoding.UTF8.GetString( stdout.ToArray() ), Encoding.UTF8.GetString( stderr.ToArray() ) );
	}
	private static async Task<RunResult> RunInteractiveAsync(
		IReadOnlyList<string> args,
		FakeTerminal terminal,
		FiniteScheduler scheduler,
		params ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>[] slabs
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( scheduler );
		ArgumentNullException.ThrowIfNull( slabs );
		await using var stdout = new MemoryStream();
		await using var stderr = new MemoryStream();
		var sampler = new ProcSampler( new FakeClock(), scheduler );
		var exitCode = await Command.RunAsync(
			args,
			stdout,
			stderr,
			new FakeSlabProvider( slabs ),
			sampler,
			new FakeTerminalFactory( terminal ),
			new FakeSignalSourceFactory()
		);
		return new RunResult( exitCode, Encoding.UTF8.GetString( stdout.ToArray() ), Encoding.UTF8.GetString( stderr.ToArray() ) );
	}
	private sealed record RunResult( int ExitCode, string Stdout, string Stderr );
	private sealed class FakeSlabProvider : IProcSlabProvider {
		private readonly Queue<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>> _values;
		private ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>> _last;
		public FakeSlabProvider( params ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>[] values ) {
			ArgumentNullException.ThrowIfNull( values );
			if ( 0 == values.Length ) {
				throw new ArgumentException( "At least one slab observation is required.", nameof( values ) );
			}
			this._values = new Queue<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>>( values );
			this._last = values[ ^1 ];
		}
		/// <inheritdoc />
		public Task<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>> GetSlabsAsync( CancellationToken cancellationToken = default ) {
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
			[EnumeratorCancellation] CancellationToken cancellationToken = default,
			PeriodicMissedTickPolicy missedTickPolicy = PeriodicMissedTickPolicy.SkipMissed
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
