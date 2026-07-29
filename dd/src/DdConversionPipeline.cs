namespace Icod.CoreUtils.DD;

/// <summary>
/// Applies stateful GNU <c>dd</c> byte and record conversions across successive input blocks.
/// </summary>
/// <remarks>
/// The pipeline preserves pending <c>swab</c> bytes and partial <c>block</c>/<c>unblock</c> records between calls, updates truncation statistics, and emits any final buffered data from <see cref="Complete"/>.
/// </remarks>
internal sealed class DdConversionPipeline {
	private const byte LineFeed = 0x0A;
	private const byte Space = 0x20;
	private readonly DdOptions myOptions;
	private readonly DdStatistics myStatistics;
	private readonly byte[] myRecordBuffer;
	private int myRecordLength;
	private bool myRecordTruncated;
	private byte mySwabPending;
	private bool mySwabPendingAvailable;

	/// <summary>
	/// Initializes a stateful conversion pipeline for the selected operands and transfer counters.
	/// </summary>
	/// <param name="options">The validated <c>dd</c> operand state.</param>
	/// <param name="statistics">The transfer counters read or updated by the operation.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="statistics"/> is <see langword="null"/>.</exception>
	public DdConversionPipeline(
		DdOptions options,
		DdStatistics statistics
	) {
		this.myOptions = options ?? throw new ArgumentNullException(
			nameof( options )
		);
		this.myStatistics = statistics ?? throw new ArgumentNullException(
			nameof( statistics )
		);
		this.myRecordBuffer = 0 < options.ConversionBlockSize
			? new byte[ options.ConversionBlockSize ]
			: []
		;
	}

	/// <summary>
	/// Applies padding, byte-pair swapping, translation, case conversion, and record conversion to one input block.
	/// </summary>
	/// <param name="source">The source bytes for the current conversion pass.</param>
	/// <param name="padToInputBlock"><see langword="true"/> to pad a short source block to the configured input block size before conversion.</param>
	/// <returns>The converted bytes ready for output; the result may be empty while a partial record remains buffered.</returns>
	public byte[] TransformBlock(
		ReadOnlySpan<byte> source,
		bool padToInputBlock
	) {
		var length = padToInputBlock
			? this.myOptions.InputBlockSize
			: source.Length
		;
		var data = new byte[ length ];
		source.CopyTo(
			data
		);
		if ( source.Length < length ) {
			data.AsSpan(
				source.Length
			).Fill(
				this.myOptions.HasConversion( DdConversion.Block )
				|| this.myOptions.HasConversion( DdConversion.Unblock )
					? Space
					: (byte)0
			);
		}
		if ( this.myOptions.HasConversion( DdConversion.Swab ) ) {
			data = this.Swab(
				data
			);
		}
		return this.TransformPrepared(
			data
		);
	}

	/// <summary>
	/// Flushes an odd pending swab byte and any partial block or unblock record at end of input.
	/// </summary>
	/// <returns>The final converted bytes produced from pending state, or an empty array when no data remains.</returns>
	public byte[] Complete() {
		using var output = new MemoryStream();
		if ( this.mySwabPendingAvailable ) {
			var transformed = this.TransformPrepared(
				[ this.mySwabPending ]
			);
			output.Write(
				transformed
			);
			this.mySwabPendingAvailable = false;
		}
		if ( this.myOptions.UsesBlockConversion ) {
			if (
				0 < this.myRecordLength
				|| this.myRecordTruncated
			) {
				using var record = new MemoryStream(
					this.myOptions.ConversionBlockSize
				);
				this.CompleteBlockedRecord(
					record
				);
				var bytes = record.ToArray();
				this.ApplyOutputTranslation(
					bytes
				);
				output.Write(
					bytes
				);
			}
		} else if (
			this.myOptions.UsesUnblockConversion
			&& 0 < this.myRecordLength
		) {
			using var record = new MemoryStream(
				this.myRecordLength + 1
			);
			this.CompleteUnblockedRecord(
				record
			);
			var bytes = record.ToArray();
			this.ApplyOutputTranslation(
				bytes
			);
			output.Write(
				bytes
			);
		}
		return output.ToArray();
	}

	private byte[] Swab(
		ReadOnlySpan<byte> data
	) {
		using var output = new MemoryStream(
			data.Length + 1
		);
		var index = 0;
		if (
			this.mySwabPendingAvailable
			&& 0 < data.Length
		) {
			output.WriteByte(
				data[ 0 ]
			);
			output.WriteByte(
				this.mySwabPending
			);
			this.mySwabPendingAvailable = false;
			index = 1;
		}
		for ( ; index + 1 < data.Length; index += 2 ) {
			output.WriteByte(
				data[ index + 1 ]
			);
			output.WriteByte(
				data[ index ]
			);
		}
		if ( index < data.Length ) {
			this.mySwabPending = data[ index ];
			this.mySwabPendingAvailable = true;
		}
		return output.ToArray();
	}

	private byte[] TransformPrepared(
		byte[] data
	) {
		if ( this.myOptions.HasConversion( DdConversion.Ascii ) ) {
			DdConversions.TranslateFromEbcdic(
				data
			);
		}
		if ( this.myOptions.HasConversion( DdConversion.LowerCase ) ) {
			DdConversions.ToLowerAscii(
				data
			);
		}
		if ( this.myOptions.HasConversion( DdConversion.UpperCase ) ) {
			DdConversions.ToUpperAscii(
				data
			);
		}

		byte[] converted;
		if ( this.myOptions.UsesBlockConversion ) {
			converted = this.Block(
				data
			);
		} else if ( this.myOptions.UsesUnblockConversion ) {
			converted = this.Unblock(
				data
			);
		} else {
			converted = data;
		}
		this.ApplyOutputTranslation(
			converted
		);
		return converted;
	}

	private byte[] Block(
		ReadOnlySpan<byte> data
	) {
		using var output = new MemoryStream(
			data.Length
		);
		foreach ( var value in data ) {
			if ( LineFeed == value ) {
				this.CompleteBlockedRecord(
					output
				);
				continue;
			}
			if ( this.myRecordLength < this.myRecordBuffer.Length ) {
				this.myRecordBuffer[ this.myRecordLength++ ] = value;
			} else {
				this.myRecordTruncated = true;
			}
		}
		return output.ToArray();
	}

	private byte[] Unblock(
		ReadOnlySpan<byte> data
	) {
		using var output = new MemoryStream(
			data.Length + Math.Max(
				1,
				data.Length / Math.Max(
					1,
					this.myOptions.ConversionBlockSize
				)
			)
		);
		foreach ( var value in data ) {
			this.myRecordBuffer[ this.myRecordLength++ ] = value;
			if ( this.myRecordLength == this.myRecordBuffer.Length ) {
				this.CompleteUnblockedRecord(
					output
				);
			}
		}
		return output.ToArray();
	}

	private void CompleteBlockedRecord(
		Stream output
	) {
		output.Write(
			this.myRecordBuffer,
			0,
			this.myRecordLength
		);
		for (
			var index = this.myRecordLength;
			index < this.myRecordBuffer.Length;
			index++
		) {
			output.WriteByte(
				Space
			);
		}
		if ( this.myRecordTruncated ) {
			this.myStatistics.AddTruncatedRecord();
		}
		this.myRecordLength = 0;
		this.myRecordTruncated = false;
	}

	private void CompleteUnblockedRecord(
		Stream output
	) {
		var length = this.myRecordLength;
		while (
			0 < length
			&& Space == this.myRecordBuffer[ length - 1 ]
		) {
			length--;
		}
		output.Write(
			this.myRecordBuffer,
			0,
			length
		);
		output.WriteByte(
			LineFeed
		);
		this.myRecordLength = 0;
	}

	private void ApplyOutputTranslation(
		Span<byte> data
	) {
		if ( this.myOptions.HasConversion( DdConversion.Ebcdic ) ) {
			DdConversions.TranslateToEbcdic(
				data
			);
		} else if ( this.myOptions.HasConversion( DdConversion.Ibm ) ) {
			DdConversions.TranslateToIbm(
				data
			);
		}
	}
}
