namespace Icod.CoreUtils.DD;

using Icod.CoreUtils.Shared.Diagnostics;

internal sealed class DdCopyEngine {
	private readonly CommandContext myContext;
	private readonly DdConversionPipeline myConversions;
	private readonly Stream myInput;
	private readonly DdOptions myOptions;
	private readonly DdOutputSink myOutput;
	private readonly DdStatisticsReporter myReporter;
	private readonly DdStatistics myStatistics;
	private int mySignalReportRequested;

	public DdCopyEngine(
		DdOptions options,
		Stream input,
		Stream output,
		CommandContext context,
		DdStatistics statistics,
		DdStatisticsReporter reporter
	) {
		this.myOptions = options ?? throw new ArgumentNullException(
			nameof( options )
		);
		this.myInput = input ?? throw new ArgumentNullException(
			nameof( input )
		);
		this.myContext = context ?? throw new ArgumentNullException(
			nameof( context )
		);
		this.myStatistics = statistics ?? throw new ArgumentNullException(
			nameof( statistics )
		);
		this.myReporter = reporter ?? throw new ArgumentNullException(
			nameof( reporter )
		);
		this.myConversions = new DdConversionPipeline(
			options,
			statistics
		);
		this.myOutput = new DdOutputSink(
			output,
			options.OutputBlockSize,
			options.HasConversion( DdConversion.Sparse ),
			options.HasOutputFlag( DdFlag.Append ),
			options.HasOutputFlag( DdFlag.DataSync )
			|| options.HasOutputFlag( DdFlag.Sync ),
			statistics
		);
	}

	public void RequestSignalReport() => Interlocked.Exchange(
		ref this.mySignalReportRequested,
		1
	);

	public async Task CopyAsync() {
		await this.SkipInputAsync().ConfigureAwait( false );
		this.PrepareOutput();
		var inputBuffer = new byte[ this.myOptions.InputBlockSize ];
		var bytesRemaining = this.myOptions.Count is {
			IsBytes: true,
		} byteCount
			? byteCount.Value
			: long.MaxValue
		;
		var blocksRemaining = this.myOptions.Count is {
			IsBytes: false,
		} blockLimit
			? blockLimit.Value
			: long.MaxValue
		;

		while (
			0L < bytesRemaining
			&& 0L < blocksRemaining
		) {
			this.myContext.CancellationToken.ThrowIfCancellationRequested();
			var requested = (int)Math.Min(
				inputBuffer.Length,
				bytesRemaining
			);
			var count = await this.ReadInputBlockAsync(
				inputBuffer.AsMemory(
					0,
					requested
				)
			).ConfigureAwait( false );
			if ( 0 == count ) {
				break;
			}

			var full = count == this.myOptions.InputBlockSize;
			this.myStatistics.AddInputRecord(
				full
			);
			var transformed = this.myConversions.TransformBlock(
				inputBuffer.AsSpan(
					0,
					count
				),
				this.myOptions.HasConversion( DdConversion.Sync )
				&& !full
			);
			await this.myOutput.WriteAsync(
				transformed,
				this.myOptions.UseDirectBlockCopy,
				this.myContext.CancellationToken
			).ConfigureAwait( false );

			if ( long.MaxValue != blocksRemaining ) {
				blocksRemaining--;
			}
			if ( long.MaxValue != bytesRemaining ) {
				bytesRemaining -= count;
			}
			await this.WriteRequestedSignalReportAsync().ConfigureAwait( false );
		}

		var trailing = this.myConversions.Complete();
		if ( 0 < trailing.Length ) {
			await this.myOutput.WriteAsync(
				trailing,
				preserveBlockBoundary: false,
				this.myContext.CancellationToken
			).ConfigureAwait( false );
		}
		await this.myOutput.CompleteAsync(
			this.myContext.CancellationToken
		).ConfigureAwait( false );
		await this.WriteRequestedSignalReportAsync().ConfigureAwait( false );
	}

	private async Task<int> ReadInputBlockAsync(
		Memory<byte> buffer
	) {
		if ( this.myOptions.HasInputFlag( DdFlag.FullBlock ) ) {
			var total = 0;
			while ( total < buffer.Length ) {
				var count = await this.TryReadAsync(
					buffer.Slice(
						total
					)
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}
				total += count;
			}
			return total;
		}
		return await this.TryReadAsync(
			buffer
		).ConfigureAwait( false );
	}

	private async Task<int> TryReadAsync(
		Memory<byte> buffer
	) {
		while ( true ) {
			try {
				return await this.myInput.ReadAsync(
					buffer,
					this.myContext.CancellationToken
				).ConfigureAwait( false );
			} catch ( IOException exception ) when (
				this.myOptions.HasConversion( DdConversion.NoError )
			) {
				await this.myContext.Diagnostics.ErrorAsync(
					string.Concat(
						"error reading input: ",
						exception.Message
					),
					this.myContext.CancellationToken
				).ConfigureAwait( false );
				if ( !this.myInput.CanSeek ) {
					throw;
				}
				this.myInput.Seek(
					this.myOptions.InputBlockSize,
					SeekOrigin.Current
				);
				if ( this.myOptions.HasConversion( DdConversion.Sync ) ) {
					buffer.Span.Fill(
						this.myOptions.UsesBlockConversion
						|| this.myOptions.UsesUnblockConversion
							? (byte)0x20
							: (byte)0
					);
					return buffer.Length;
				}
			}
		}
	}

	private async Task SkipInputAsync() {
		if ( 0L == this.myOptions.Skip.Value ) {
			return;
		}
		if ( this.myInput.CanSeek ) {
			this.myInput.Seek(
				this.GetByteOffset(
					this.myOptions.Skip,
					this.myOptions.InputBlockSize
				),
				SeekOrigin.Current
			);
			return;
		}

		var buffer = new byte[ this.myOptions.InputBlockSize ];
		if ( this.myOptions.Skip.IsBytes ) {
			var remaining = this.myOptions.Skip.Value;
			while ( 0L < remaining ) {
				var read = await this.TryReadAsync(
					buffer.AsMemory(
						0,
						(int)Math.Min( buffer.Length, remaining )
					)
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				remaining -= read;
			}
			return;
		}

		var blocksRemaining = this.myOptions.Skip.Value;
		while ( 0L < blocksRemaining ) {
			var read = await this.ReadInputBlockAsync(
				buffer
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			blocksRemaining--;
		}
	}

	private void PrepareOutput() {
		var count = this.GetByteOffset(
			this.myOptions.Seek,
			this.myOptions.OutputBlockSize
		);
		if (
			0L == count
			&& null == this.myOptions.OutputFile
		) {
			return;
		}
		if (
			0L < count
			&& !this.myOutput.CanSeek
		) {
			throw new IOException(
				"cannot seek on output"
			);
		}
		this.myOutput.Prepare(
			count,
			0L < count
			&& null != this.myOptions.OutputFile
			&& !this.myOptions.HasConversion( DdConversion.NoTruncate )
		);
	}

	private long GetByteOffset(
		DdQuantity quantity,
		int blockSize
	) {
		checked {
			return quantity.IsBytes
				? quantity.Value
				: quantity.Value * blockSize
			;
		}
	}

	private async Task WriteRequestedSignalReportAsync() {
		if (
			0 == Interlocked.Exchange(
				ref this.mySignalReportRequested,
				0
			)
		) {
			return;
		}
		await this.myReporter.WriteSignalReportAsync(
			this.myContext.CancellationToken
		).ConfigureAwait( false );
	}
}

internal sealed class DdOutputSink {
	private readonly byte[] myBuffer;
	private int myBufferLength;
	private readonly Stream myOutput;
	private readonly bool myAppend;
	private readonly bool mySparse;
	private readonly bool mySynchronizeWrites;
	private readonly DdStatistics myStatistics;
	private long myLogicalPosition;

	public bool CanSeek => this.myOutput.CanSeek;

	public DdOutputSink(
		Stream output,
		int blockSize,
		bool sparse,
		bool append,
		bool synchronizeWrites,
		DdStatistics statistics
	) {
		this.myOutput = output ?? throw new ArgumentNullException(
			nameof( output )
		);
		this.myBuffer = new byte[ blockSize ];
		this.mySparse = sparse;
		this.myAppend = append;
		this.mySynchronizeWrites = synchronizeWrites;
		this.myStatistics = statistics ?? throw new ArgumentNullException(
			nameof( statistics )
		);
		this.myLogicalPosition = output.CanSeek
			? output.Position
			: 0L
		;
	}

	public async Task WriteAsync(
		ReadOnlyMemory<byte> data,
		bool preserveBlockBoundary,
		CancellationToken cancellationToken
	) {
		if (
			preserveBlockBoundary
			&& !data.IsEmpty
		) {
			if ( 0 < this.myBufferLength ) {
				await this.FlushBlockAsync(
					full: false,
					cancellationToken
				).ConfigureAwait( false );
			}
			await this.WriteBlockAsync(
				data,
				data.Length == this.myBuffer.Length,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var remaining = data;
		while ( !remaining.IsEmpty ) {
			var count = Math.Min(
				remaining.Length,
				this.myBuffer.Length - this.myBufferLength
			);
			remaining.Slice(
				0,
				count
			).CopyTo(
				this.myBuffer.AsMemory(
					this.myBufferLength,
					count
				)
			);
			this.myBufferLength += count;
			remaining = remaining.Slice(
				count
			);
			if ( this.myBufferLength == this.myBuffer.Length ) {
				await this.FlushBlockAsync(
					full: true,
					cancellationToken
				).ConfigureAwait( false );
			}
		}
	}

	public void Prepare(
		long offset,
		bool truncateAtOffset
	) {
		if ( 0 != this.myBufferLength ) {
			throw new InvalidOperationException(
				"Output preparation must occur before copying starts."
			);
		}
		if ( !this.myOutput.CanSeek ) {
			return;
		}
		if ( truncateAtOffset ) {
			this.myOutput.SetLength(
				offset
			);
		}
		this.myOutput.Seek(
			offset,
			SeekOrigin.Begin
		);
		this.myLogicalPosition = offset;
	}

	public async Task CompleteAsync(
		CancellationToken cancellationToken
	) {
		if ( 0 < this.myBufferLength ) {
			await this.FlushBlockAsync(
				full: false,
				cancellationToken
			).ConfigureAwait( false );
		}
		if (
			this.mySparse
			&& this.myOutput.CanSeek
			&& this.myOutput.Length < this.myLogicalPosition
		) {
			this.myOutput.SetLength(
				this.myLogicalPosition
			);
		}
		await this.myOutput.FlushAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

	private async Task FlushBlockAsync(
		bool full,
		CancellationToken cancellationToken
	) {
		await this.WriteBlockAsync(
			this.myBuffer.AsMemory(
				0,
				this.myBufferLength
			),
			full,
			cancellationToken
		).ConfigureAwait( false );
		this.myBufferLength = 0;
	}

	private async Task WriteBlockAsync(
		ReadOnlyMemory<byte> block,
		bool full,
		CancellationToken cancellationToken
	) {
		if (
			this.myAppend
			&& this.myOutput.CanSeek
		) {
			this.myOutput.Seek(
				0L,
				SeekOrigin.End
			);
			this.myLogicalPosition = this.myOutput.Position;
		}
		if (
			this.mySparse
			&& this.myOutput.CanSeek
			&& IsAllZero( block.Span )
		) {
			this.myOutput.Seek(
				block.Length,
				SeekOrigin.Current
			);
		} else {
			await this.myOutput.WriteAsync(
				block,
				cancellationToken
			).ConfigureAwait( false );
		}
		this.myLogicalPosition += block.Length;
		this.myStatistics.AddBytes(
			block.Length
		);
		this.myStatistics.AddOutputRecord(
			full
		);
		if ( this.mySynchronizeWrites ) {
			await this.myOutput.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static bool IsAllZero(
		ReadOnlySpan<byte> data
	) {
		foreach ( var value in data ) {
			if ( 0 != value ) {
				return false;
			}
		}
		return true;
	}
}
