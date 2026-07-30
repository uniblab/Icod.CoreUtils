namespace Icod.CoreUtils.Shared.Records;

using System.Buffers;
using Icod.CoreUtils.Shared.IO;

/// <summary>Materializes separator-delimited byte records as content plus explicit termination metadata.</summary>
/// <remarks>
/// <para>The reader owns only its bounded segmented reader. Ownership of the source stream remains with the caller.</para>
/// <para>Consumers that can process a record incrementally should prefer <see cref="DelimitedByteRecordSegmentReader"/> to avoid materializing an extremely large record.</para>
/// </remarks>
public sealed class ByteRecordReader : IDisposable {

	private readonly DelimitedByteRecordSegmentReader myReader;

	/// <summary>Initializes a materializing byte-record reader.</summary>
	/// <param name="stream">The readable source stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record separator.</param>
	/// <param name="bufferSize">The maximum bytes requested per segmented-reader refill and returned in one data segment.</param>
	public ByteRecordReader(
		Stream stream,
		RecordSeparator separator,
		int bufferSize = StreamOperations.DefaultBufferSize
	) {
		this.myReader = new DelimitedByteRecordSegmentReader(
			stream,
			separator,
			bufferSize
		);
	}

	/// <summary>Initializes a materializing byte-record reader using a separator byte.</summary>
	/// <param name="stream">The readable source stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record separator.</param>
	/// <param name="bufferSize">The maximum bytes requested per segmented-reader refill and returned in one data segment.</param>
	public ByteRecordReader(
		Stream stream,
		byte separator = (byte)'\n',
		int bufferSize = StreamOperations.DefaultBufferSize
	) : this(
		stream,
		new RecordSeparator( separator ),
		bufferSize
	) {
	}

	/// <summary>Reads the next materialized record without its separator.</summary>
	/// <param name="cancellationToken">A token that may cancel the asynchronous read.</param>
	/// <returns>The next independent record, or <see langword="null"/> after the final record.</returns>
	public async ValueTask<ByteRecord?> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		ArrayBufferWriter<byte>? builder = null;
		while ( true ) {
			var segment = await this.myReader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( null == segment ) {
				return null;
			}
			builder ??= new ArrayBufferWriter<byte>( Math.Max( 1, segment.Data.Length ) );
			builder.Write( segment.Data.Span );
			if ( segment.EndsRecord ) {
				return new ByteRecord(
					builder.WrittenSpan,
					segment.IsTerminated
				);
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose() {
		this.myReader.Dispose();
	}

}
