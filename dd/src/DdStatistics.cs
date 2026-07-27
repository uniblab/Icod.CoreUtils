namespace Icod.CoreUtils.DD;

using System.Diagnostics;
using System.Globalization;

internal sealed class DdStatistics {
	private readonly Stopwatch myStopwatch = Stopwatch.StartNew();
	private long myBytesCopied;
	private long myInputFull;
	private long myInputPartial;
	private long myOutputFull;
	private long myOutputPartial;
	private long myTruncatedRecords;

	public long BytesCopied => Interlocked.Read( ref this.myBytesCopied );
	public long InputFull => Interlocked.Read( ref this.myInputFull );
	public long InputPartial => Interlocked.Read( ref this.myInputPartial );
	public long OutputFull => Interlocked.Read( ref this.myOutputFull );
	public long OutputPartial => Interlocked.Read( ref this.myOutputPartial );
	public long TruncatedRecords => Interlocked.Read( ref this.myTruncatedRecords );
	public TimeSpan Elapsed => this.myStopwatch.Elapsed;

	public void AddBytes(
		long count
	) => Interlocked.Add(
		ref this.myBytesCopied,
		count
	);

	public void AddInputRecord(
		bool full
	) {
		if ( full ) {
			Interlocked.Increment( ref this.myInputFull );
		} else {
			Interlocked.Increment( ref this.myInputPartial );
		}
	}

	public void AddOutputRecord(
		bool full
	) {
		if ( full ) {
			Interlocked.Increment( ref this.myOutputFull );
		} else {
			Interlocked.Increment( ref this.myOutputPartial );
		}
	}

	public void AddTruncatedRecord() => Interlocked.Increment(
		ref this.myTruncatedRecords
	);

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

internal sealed class DdStatisticsReporter : IAsyncDisposable {
	private readonly CancellationTokenSource myProgressCancellation = new();
	private readonly SemaphoreSlim myWriteLock = new( 1, 1 );
	private readonly TextWriter myWriter;
	private readonly DdStatistics myStatistics;
	private readonly DdStatusMode myStatus;
	private Task? myProgressTask;

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

	public void StartProgress() {
		if ( DdStatusMode.Progress != this.myStatus ) {
			return;
		}
		this.myProgressTask = this.ProgressLoopAsync(
			this.myProgressCancellation.Token
		);
	}

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
