namespace Icod.CoreUtils.Shared.IO;

using Icod.CoreUtils.Shared.Records;

/// <summary>
/// Reads byte records from a forward-only stream while preserving each record's
/// delimiter and a final unterminated record exactly as supplied.
/// </summary>
/// <remarks>
/// <para>The reader owns only its shared materializing reader. Ownership of the source stream remains with the caller. Each returned array is independent and may be retained.</para>
/// <para>New consumers that need explicit termination metadata should prefer <see cref="ByteRecordReader"/>. Consumers that can process a record incrementally should prefer <see cref="DelimitedByteRecordSegmentReader"/>.</para>
/// </remarks>
public sealed class DelimitedByteRecordReader : IDisposable {

	private readonly ByteRecordReader myReader;
	private readonly byte mySeparator;

	/// <summary>
	/// Initializes a byte-record reader.
	/// </summary>
	/// <param name="stream">The source stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record delimiter, normally LF or NUL.</param>
	/// <param name="bufferSize">The reusable input-buffer size.</param>
	public DelimitedByteRecordReader(
		Stream stream,
		byte separator = (byte)'\n',
		int bufferSize = StreamOperations.DefaultBufferSize
	) {
		this.mySeparator = separator;
		this.myReader = new ByteRecordReader(
			stream,
			separator,
			bufferSize
		);
	}

	/// <summary>
	/// Reads the next record, including its delimiter when one was present.
	/// </summary>
	/// <returns>
	/// An independent byte array containing the record and delimiter, or
	/// <see langword="null"/> after the final record.
	/// </returns>
	public async ValueTask<byte[]?> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		var record = await this.myReader.ReadAsync(
			cancellationToken
		).ConfigureAwait( false );
		if ( null == record ) {
			return null;
		}
		var result = new byte[
			record.Content.Length + ( record.IsTerminated ? 1 : 0 )
		];
		record.Content.Span.CopyTo( result );
		if ( record.IsTerminated ) {
			result[^1] = this.mySeparator;
		}
		return result;
	}

	/// <inheritdoc/>
	public void Dispose() {
		this.myReader.Dispose();
	}

}
