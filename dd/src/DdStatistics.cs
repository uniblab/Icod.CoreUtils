namespace Icod.CoreUtils.DD;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Collects thread-safe <c>dd</c> transfer counters and elapsed time.
/// </summary>
internal sealed class DdStatistics {
	private readonly Stopwatch myStopwatch = Stopwatch.StartNew();
	private long myBytesCopied;
	private long myInputFull;
	private long myInputPartial;
	private long myOutputFull;
	private long myOutputPartial;
	private long myTruncatedRecords;

	/// <summary>
	/// Gets the number of output data bytes committed or logically represented by sparse holes.
	/// </summary>
	/// <value>The number of output data bytes committed or logically represented by sparse holes.</value>
	public long BytesCopied => Interlocked.Read( ref this.myBytesCopied );
	/// <summary>
	/// Gets the number of complete input blocks read.
	/// </summary>
	/// <value>The number of complete input blocks read.</value>
	public long InputFull => Interlocked.Read( ref this.myInputFull );
	/// <summary>
	/// Gets the number of short input blocks read.
	/// </summary>
	/// <value>The number of short input blocks read.</value>
	public long InputPartial => Interlocked.Read( ref this.myInputPartial );
	/// <summary>
	/// Gets the number of complete output blocks written.
	/// </summary>
	/// <value>The number of complete output blocks written.</value>
	public long OutputFull => Interlocked.Read( ref this.myOutputFull );
	/// <summary>
	/// Gets the number of short output blocks written.
	/// </summary>
	/// <value>The number of short output blocks written.</value>
	public long OutputPartial => Interlocked.Read( ref this.myOutputPartial );
	/// <summary>
	/// Gets the number of overlong records truncated by the <c>block</c> conversion.
	/// </summary>
	/// <value>The number of overlong records truncated by the <c>block</c> conversion.</value>
	public long TruncatedRecords => Interlocked.Read( ref this.myTruncatedRecords );
	/// <summary>
	/// Gets the elapsed transfer time measured since statistics collection began.
	/// </summary>
	/// <value>The elapsed transfer time measured since statistics collection began.</value>
	public TimeSpan Elapsed => this.myStopwatch.Elapsed;

	/// <summary>
	/// Atomically adds transferred bytes to the cumulative count.
	/// </summary>
	/// <param name="count">The non-negative number of output bytes to add.</param>
	public void AddBytes(
		long count
	) => Interlocked.Add(
		ref this.myBytesCopied,
		count
	);

	/// <summary>
	/// Atomically records one complete or partial input record.
	/// </summary>
	/// <param name="full"><see langword="true"/> to count a complete input block; <see langword="false"/> to count a short block.</param>
	public void AddInputRecord(
		bool full
	) {
		if ( full ) {
			Interlocked.Increment( ref this.myInputFull );
		} else {
			Interlocked.Increment( ref this.myInputPartial );
		}
	}

	/// <summary>
	/// Atomically records one complete or partial output record.
	/// </summary>
	/// <param name="full"><see langword="true"/> to count a complete output block; <see langword="false"/> to count a short block.</param>
	public void AddOutputRecord(
		bool full
	) {
		if ( full ) {
			Interlocked.Increment( ref this.myOutputFull );
		} else {
			Interlocked.Increment( ref this.myOutputPartial );
		}
	}

	/// <summary>
	/// Atomically records one record truncated by fixed-width conversion.
	/// </summary>
	public void AddTruncatedRecord() => Interlocked.Increment(
		ref this.myTruncatedRecords
	);

	/// <summary>
	/// Formats the GNU-style byte count, elapsed seconds, and bytes-per-second transfer line.
	/// </summary>
	/// <returns>A culture-invariant GNU-style transfer summary.</returns>
	public string FormatTransferLine() {
		var elapsedSeconds = Math.Max(
			this.Elapsed.TotalSeconds,
			0.000001D
		);
		var rate = this.BytesCopied / elapsedSeconds;
		return string.Concat(
			this.BytesCopied.ToString( CultureInfo.InvariantCulture ),
			" bytes copied, ",
			elapsedSeconds.ToString( "0.######", CultureInfo.InvariantCulture ),
			" s, ",
			rate.ToString( "0.###", CultureInfo.InvariantCulture ),
			" B/s"
		);
	}
}

/// <summary>
/// Writes GNU-style <c>dd</c> record and transfer reports, including periodic progress output.
/// </summary>
/// <remarks>
/// Writes are serialized so periodic, signal-triggered, and final reports cannot interleave.
/// </remarks>
internal sealed class DdStatisticsReporter : IAsyncDisposable {
	private readonly CancellationTokenSource myProgressCancellation = new();
	private readonly SemaphoreSlim myWriteLock = new( 1, 1 );
	private readonly TextWriter myWriter;
	private readonly DdStatistics myStatistics;
	private readonly DdStatusMode myStatus;
	private Task? myProgressTask;

	/// <summary>
	/// Initializes a reporter for the supplied destination, counters, and status mode.
	/// </summary>
	/// <param name="writer">The destination for status reports.</param>
	/// <param name="statistics">The transfer counters read or updated by the operation.</param>
	/// <param name="status">The selected status-reporting mode.</param>
	/// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="statistics"/> is <see langword="null"/>.</exception>
	public DdStatisticsReporter(
		TextWriter writer,
		DdStatistics statistics,
		DdStatusMode status
	) {
		this.myWriter = writer ?? throw new ArgumentNullException(
			nameof( writer )
		);
		this.myStatistics = statistics ?? throw new ArgumentNullException(
			nameof( statistics )
		);
		this.myStatus = status;
	}

	/// <summary>
	/// Starts once-per-second progress reporting when <see cref="DdStatusMode.Progress"/> is selected.
	/// </summary>
	public void StartProgress() {
		if ( DdStatusMode.Progress != this.myStatus ) {
			return;
		}
		this.myProgressTask = this.ProgressLoopAsync(
			this.myProgressCancellation.Token
		);
	}

	/// <summary>
	/// Writes the status report requested by a supported user signal.
	/// </summary>
	/// <param name="cancellationToken">The token used to cancel the serialized report write.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public async Task WriteSignalReportAsync(
		CancellationToken cancellationToken
	) {
		if ( DdStatusMode.None == this.myStatus ) {
			return;
		}
		await this.WriteFullReportAsync(
			includeRecords: true,
			includeTransfer: DdStatusMode.NoTransfer != this.myStatus,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Writes the final record counts and optional transfer-rate line.
	/// </summary>
	/// <param name="cancellationToken">The token used to cancel the serialized final report write.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public async Task WriteFinalReportAsync(
		CancellationToken cancellationToken
	) {
		if ( DdStatusMode.None == this.myStatus ) {
			return;
		}
		await this.WriteFullReportAsync(
			includeRecords: true,
			includeTransfer: DdStatusMode.NoTransfer != this.myStatus,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Stops periodic progress reporting and releases synchronization resources.
	/// </summary>
	/// <returns>A value task that represents the asynchronous operation.</returns>
	public async ValueTask DisposeAsync() {
		this.myProgressCancellation.Cancel();
		if ( null != this.myProgressTask ) {
			try {
				await this.myProgressTask.ConfigureAwait( false );
			} catch ( OperationCanceledException ) {
			}
		}
		this.myProgressCancellation.Dispose();
		this.myWriteLock.Dispose();
	}

	private async Task ProgressLoopAsync(
		CancellationToken cancellationToken
	) {
		using var timer = new PeriodicTimer(
			TimeSpan.FromSeconds( 1D )
		);
		while (
			await timer.WaitForNextTickAsync(
				cancellationToken
			).ConfigureAwait( false )
		) {
			await this.WriteFullReportAsync(
				includeRecords: false,
				includeTransfer: true,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private async Task WriteFullReportAsync(
		bool includeRecords,
		bool includeTransfer,
		CancellationToken cancellationToken
	) {
		await this.myWriteLock.WaitAsync(
			cancellationToken
		).ConfigureAwait( false );
		try {
			if ( includeRecords ) {
				await this.myWriter.WriteLineAsync(
					string.Concat(
						this.myStatistics.InputFull.ToString( CultureInfo.InvariantCulture ),
						"+",
						this.myStatistics.InputPartial.ToString( CultureInfo.InvariantCulture ),
						" records in"
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				await this.myWriter.WriteLineAsync(
					string.Concat(
						this.myStatistics.OutputFull.ToString( CultureInfo.InvariantCulture ),
						"+",
						this.myStatistics.OutputPartial.ToString( CultureInfo.InvariantCulture ),
						" records out"
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0L < this.myStatistics.TruncatedRecords ) {
					await this.myWriter.WriteLineAsync(
						string.Concat(
							this.myStatistics.TruncatedRecords.ToString( CultureInfo.InvariantCulture ),
							1L == this.myStatistics.TruncatedRecords
								? " truncated record"
								: " truncated records"
						).AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
				}
			}
			if ( includeTransfer ) {
				await this.myWriter.WriteLineAsync(
					this.myStatistics.FormatTransferLine().AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			this.myWriteLock.Release();
		}
	}
}
