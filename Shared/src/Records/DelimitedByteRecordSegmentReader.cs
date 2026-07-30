namespace Icod.CoreUtils.Shared.Records;

using System.Buffers;
using Icod.CoreUtils.Shared.IO;

/// <summary>
/// Reads bounded, independently owned segments from separator-delimited byte records.
/// </summary>
/// <remarks>
/// <para>The reader owns only its rented buffer. The caller retains ownership of the source stream.</para>
/// <para>A segment whose <see cref="ByteRecordSegment.EndsRecord"/> property is false is followed by another segment in the same record. A final unterminated record is reported with <c>EndsRecord</c> true and <c>IsTerminated</c> false.</para>
/// </remarks>
public sealed class DelimitedByteRecordSegmentReader : IDisposable {

	private readonly byte[] myBuffer;
	private readonly int myBufferSize;
	private int myCount;
	private bool myDisposed;
	private bool myEndOfInput;
	private int myIndex;
	private readonly RecordSeparator mySeparator;
	private readonly Stream myStream;

	/// <summary>Initializes a segmented byte-record reader.</summary>
	/// <param name="stream">The readable source stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record separator.</param>
	/// <param name="bufferSize">The maximum bytes requested per refill and returned in one data segment.</param>
	public DelimitedByteRecordSegmentReader(
		Stream stream,
		RecordSeparator separator,
		int bufferSize = StreamOperations.DefaultBufferSize
	) {
		ArgumentNullException.ThrowIfNull( stream );
		if ( !stream.CanRead ) {
			throw new ArgumentException(
				"The source stream must be readable.",
				nameof( stream )
			);
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( bufferSize ) );
		}
		this.myStream = stream;
		this.mySeparator = separator;
		this.myBufferSize = bufferSize;
		this.myBuffer = ArrayPool<byte>.Shared.Rent( bufferSize );
	}

	/// <summary>Initializes a segmented byte-record reader using a separator byte.</summary>
	/// <param name="stream">The readable source stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record separator.</param>
	/// <param name="bufferSize">The maximum bytes requested per refill and returned in one data segment.</param>
	public DelimitedByteRecordSegmentReader(
		Stream stream,
		byte separator = (byte)'\n',
		int bufferSize = StreamOperations.DefaultBufferSize
	) : this(
		stream,
		new RecordSeparator( separator ),
		bufferSize
	) {
	}

	/// <summary>Reads the next bounded record segment.</summary>
	/// <param name="cancellationToken">A token that may cancel the asynchronous read.</param>
	/// <returns>The next segment, or <see langword="null"/> after the final record.</returns>
	public async ValueTask<ByteRecordSegment?> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		this.ThrowIfDisposed();
		cancellationToken.ThrowIfCancellationRequested();
		if ( this.myEndOfInput ) {
			return null;
		}
		if ( this.myCount <= this.myIndex ) {
			if ( !await this.FillAsync( cancellationToken ).ConfigureAwait( false ) ) {
				this.myEndOfInput = true;
				return null;
			}
		}

		var start = this.myIndex;
		while (
			this.myIndex < this.myCount
			&& this.mySeparator.Value != this.myBuffer[ this.myIndex ]
		) {
			this.myIndex++;
		}
		if ( this.myIndex < this.myCount ) {
			var data = this.myBuffer.AsSpan( start, this.myIndex - start );
			this.myIndex++;
			return new ByteRecordSegment(
				data,
				endsRecord: true,
				isTerminated: true
			);
		}

		var segment = this.myBuffer.AsSpan( start, this.myIndex - start ).ToArray();
		if ( await this.FillAsync( cancellationToken ).ConfigureAwait( false ) ) {
			if ( this.mySeparator.Value == this.myBuffer[0] ) {
				this.myIndex = 1;
				return new ByteRecordSegment(
					segment,
					endsRecord: true,
					isTerminated: true
				);
			}
			return new ByteRecordSegment(
				segment,
				endsRecord: false,
				isTerminated: false
			);
		}
		this.myEndOfInput = true;
		return new ByteRecordSegment(
			segment,
			endsRecord: true,
			isTerminated: false
		);
	}

	/// <inheritdoc/>
	public void Dispose() {
		if ( this.myDisposed ) {
			return;
		}
		this.myDisposed = true;
		ArrayPool<byte>.Shared.Return( this.myBuffer );
	}

	private async ValueTask<bool> FillAsync( CancellationToken cancellationToken ) {
		this.myCount = await this.myStream.ReadAsync(
			this.myBuffer.AsMemory( 0, this.myBufferSize ),
			cancellationToken
		).ConfigureAwait( false );
		this.myIndex = 0;
		return 0 < this.myCount;
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf(
			this.myDisposed,
			this
		);
	}

}
