namespace Icod.CoreUtils.Shared.IO;

using System.Buffers;

/// <summary>
/// Reads byte records from a forward-only stream while preserving each record's
/// delimiter and a final unterminated record exactly as supplied.
/// </summary>
/// <remarks>
/// The reader owns only its rented buffer. Ownership of the source stream remains
/// with the caller. Each returned array is independent and may be retained.
/// </remarks>
public sealed class DelimitedByteRecordReader : IDisposable {

	private readonly byte[] myBuffer;
	private int myCount;
	private bool myDisposed;
	private bool myEndOfInput;
	private int myIndex;
	private readonly byte mySeparator;
	private readonly Stream myStream;

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
		ArgumentNullException.ThrowIfNull(
			stream
		);
		if ( !stream.CanRead ) {
			throw new ArgumentException(
				"The source stream must be readable.",
				nameof( stream )
			);
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}

		this.myStream = stream;
		this.mySeparator = separator;
		this.myBuffer = ArrayPool<byte>.Shared.Rent(
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
		this.ThrowIfDisposed();
		cancellationToken.ThrowIfCancellationRequested();
		if ( this.myEndOfInput ) {
			return null;
		}

		ArrayBufferWriter<byte>? builder = null;
		while ( true ) {
			if ( this.myCount <= this.myIndex ) {
				this.myCount = await this.myStream.ReadAsync(
					this.myBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				this.myIndex = 0;
				if ( 0 == this.myCount ) {
					this.myEndOfInput = true;
					return null == builder
						? null
						: builder.WrittenSpan.ToArray()
					;
				}
			}

			var start = this.myIndex;
			while (
				this.myIndex < this.myCount
				&& this.mySeparator != this.myBuffer[ this.myIndex ]
			) {
				this.myIndex++;
			}

			if (
				this.myIndex < this.myCount
				&& this.mySeparator == this.myBuffer[ this.myIndex ]
			) {
				var length = this.myIndex - start + 1;
				this.myIndex++;
				if ( null == builder ) {
					var output = new byte[ length ];
					this.myBuffer.AsSpan(
						start,
						length
					).CopyTo(
						output
					);
					return output;
				}

				builder.Write(
					this.myBuffer.AsSpan(
						start,
						length
					)
				);
				return builder.WrittenSpan.ToArray();
			}

			builder ??= new ArrayBufferWriter<byte>(
				Math.Max(
					256,
					this.myCount - start
				)
			);
			builder.Write(
				this.myBuffer.AsSpan(
					start,
					this.myIndex - start
				)
			);
		}
	}

	/// <inheritdoc/>
	public void Dispose() {
		if ( this.myDisposed ) {
			return;
		}
		this.myDisposed = true;
		ArrayPool<byte>.Shared.Return(
			this.myBuffer
		);
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf(
			this.myDisposed,
			this
		);
	}

}
