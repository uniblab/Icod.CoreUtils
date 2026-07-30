namespace Icod.CoreUtils.Shared.IO;

using System.Buffers;

/// <summary>
/// Reads nonempty byte tokens separated by any byte in a caller-supplied
/// separator set.
/// </summary>
/// <remarks>
/// The reader performs incremental asynchronous reads, preserves every
/// non-separator byte, and never takes ownership of the supplied stream. This
/// API is a provisional cross-suite command-framework candidate because token
/// streams are required by multiple text-processing commands.
/// </remarks>
public sealed class ByteTokenReader : IDisposable {
	private readonly byte[] myBuffer;
	private int myBufferCount;
	private int myBufferOffset;
	private bool myDisposed;
	private readonly Stream myInput;
	private readonly int myReadSize;
	private readonly bool[] mySeparators = new bool[ byte.MaxValue + 1 ];

	/// <summary>Initializes a byte-token reader.</summary>
	/// <param name="input">The readable source stream.</param>
	/// <param name="separators">The byte values that separate tokens.</param>
	/// <param name="bufferSize">The incremental read-buffer size.</param>
	/// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="input"/> is not readable.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is not positive.</exception>
	public ByteTokenReader(
		Stream input,
		ReadOnlySpan<byte> separators,
		int bufferSize = StreamOperations.DefaultBufferSize
	) {
		this.myInput = input ?? throw new ArgumentNullException( nameof( input ) );
		if ( !input.CanRead ) {
			throw new ArgumentException( "The source stream must be readable.", nameof( input ) );
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( bufferSize ) );
		}
		foreach ( var separator in separators ) {
			this.mySeparators[ separator ] = true;
		}
		this.myReadSize = bufferSize;
		this.myBuffer = ArrayPool<byte>.Shared.Rent( bufferSize );
	}

	/// <summary>Reads the next token from the source.</summary>
	/// <param name="cancellationToken">The token used to cancel asynchronous reads.</param>
	/// <returns>
	/// A value task whose result is an independently owned token, or
	/// <see langword="null"/> after the final token.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The operation is canceled.</exception>
	public async ValueTask<byte[]?> ReadTokenAsync(
		CancellationToken cancellationToken = default
	) {
		this.ThrowIfDisposed();
		cancellationToken.ThrowIfCancellationRequested();
		ArrayBufferWriter<byte>? token = null;

		while ( true ) {
			if (
				this.myBufferOffset >= this.myBufferCount
				&& !await this.FillBufferAsync( cancellationToken ).ConfigureAwait( false )
			) {
				return token?.WrittenSpan.ToArray();
			}

			if ( null == token ) {
				while (
					this.myBufferOffset < this.myBufferCount
					&& this.mySeparators[ this.myBuffer[ this.myBufferOffset ] ]
				) {
					this.myBufferOffset++;
				}
				if ( this.myBufferOffset >= this.myBufferCount ) {
					continue;
				}
				token = new ArrayBufferWriter<byte>();
			}

			var start = this.myBufferOffset;
			while (
				this.myBufferOffset < this.myBufferCount
				&& !this.mySeparators[ this.myBuffer[ this.myBufferOffset ] ]
			) {
				this.myBufferOffset++;
			}
			Append(
				token,
				this.myBuffer.AsSpan( start, this.myBufferOffset - start )
			);
			cancellationToken.ThrowIfCancellationRequested();

			if ( this.myBufferOffset < this.myBufferCount ) {
				this.myBufferOffset++;
				return token.WrittenSpan.ToArray();
			}
		}
	}

	/// <summary>Releases the rented read buffer without closing the source stream.</summary>
	public void Dispose() {
		if ( this.myDisposed ) {
			return;
		}
		this.myDisposed = true;
		ArrayPool<byte>.Shared.Return( this.myBuffer );
	}

	private static void Append( ArrayBufferWriter<byte> destination, ReadOnlySpan<byte> source ) {
		if ( source.IsEmpty ) {
			return;
		}
		var span = destination.GetSpan( source.Length );
		source.CopyTo( span );
		destination.Advance( source.Length );
	}

	private async ValueTask<bool> FillBufferAsync( CancellationToken cancellationToken ) {
		this.myBufferCount = await this.myInput.ReadAsync(
			this.myBuffer.AsMemory( 0, this.myReadSize ),
			cancellationToken
		).ConfigureAwait( false );
		this.myBufferOffset = 0;
		return 0 < this.myBufferCount;
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf( this.myDisposed, this );
	}
}
