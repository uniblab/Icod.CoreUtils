namespace Icod.CoreUtils.Shared.Text;

using System.Buffers;
using System.Text;

/// <summary>
/// Incrementally reads byte-oriented or UTF-8 text units without normalizing or discarding source bytes.
/// </summary>
/// <remarks>
/// The reader does not own or dispose the supplied stream. In UTF-8 mode, malformed input is handled one
/// source byte at a time according to <see cref="InvalidEncodingPolicy"/>.
/// </remarks>
public sealed class TextUnitReader {
	/// <summary>Specifies the default internal read-buffer size.</summary>
	public const int DefaultBufferSize = 4096;

	private readonly byte[] myBuffer;
	private readonly Stream myInput;
	private int myBufferEnd;
	private int myBufferStart;
	private bool myEndOfStream;

	/// <summary>
	/// Initializes a new instance of the <see cref="TextUnitReader"/> class.
	/// </summary>
	/// <param name="input">The readable source stream. The reader does not take ownership of it.</param>
	/// <param name="decodingMode">The unit-decoding mode.</param>
	/// <param name="invalidEncodingPolicy">The policy used for malformed UTF-8.</param>
	/// <param name="bufferSize">The requested internal read-buffer size.</param>
	/// <exception cref="ArgumentNullException">The input stream is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream is not readable.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The buffer size is not positive.</exception>
	public TextUnitReader(
		Stream input,
		TextDecodingMode decodingMode = TextDecodingMode.Utf8,
		InvalidEncodingPolicy invalidEncodingPolicy = InvalidEncodingPolicy.PreserveBytes,
		int bufferSize = DefaultBufferSize
	) {
		ArgumentNullException.ThrowIfNull( input );
		if ( !input.CanRead ) {
			throw new ArgumentException(
				"The input stream must be readable.",
				nameof( input )
			);
		}
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( bufferSize ) );
		}
		if ( !Enum.IsDefined( decodingMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( decodingMode ) );
		}
		if ( !Enum.IsDefined( invalidEncodingPolicy ) ) {
			throw new ArgumentOutOfRangeException( nameof( invalidEncodingPolicy ) );
		}
		this.myInput = input;
		this.myBuffer = new byte[Math.Max( 4, bufferSize )];
		this.DecodingMode = decodingMode;
		this.InvalidEncodingPolicy = invalidEncodingPolicy;
	}

	/// <summary>Gets the absolute source-byte offset of the next unread unit.</summary>
	public long ByteOffset {
		get;
		private set;
	}

	/// <summary>Gets the configured unit-decoding mode.</summary>
	public TextDecodingMode DecodingMode {
		get;
	}

	/// <summary>Gets the configured malformed-input policy.</summary>
	public InvalidEncodingPolicy InvalidEncodingPolicy {
		get;
	}

	/// <summary>Reads the next text unit synchronously.</summary>
	/// <returns>The next unit, or <see langword="null"/> at end of stream.</returns>
	/// <exception cref="DecoderFallbackException">Malformed UTF-8 is encountered under the throw policy.</exception>
	public TextUnit? Read() {
		while ( true ) {
			if ( !this.EnsureAvailable( 1 ) ) {
				return null;
			}
			if ( this.DecodingMode == TextDecodingMode.Bytes ) {
				return this.ConsumeByte();
			}
			var available = this.myBufferEnd - this.myBufferStart;
			var status = Rune.DecodeFromUtf8(
				this.myBuffer.AsSpan( this.myBufferStart, available ),
				out var scalar,
				out var consumed
			);
			if ( status == OperationStatus.Done ) {
				return this.ConsumeScalar( scalar, consumed );
			}
			if ( (status == OperationStatus.NeedMoreData) && !this.myEndOfStream ) {
				this.EnsureAvailable( Math.Min( 4, available + 1 ) );
				continue;
			}
			return this.HandleInvalidByte();
		}
	}

	/// <summary>Reads the next text unit asynchronously.</summary>
	/// <param name="cancellationToken">A token that can cancel an asynchronous stream read.</param>
	/// <returns>The next unit, or <see langword="null"/> at end of stream.</returns>
	/// <exception cref="DecoderFallbackException">Malformed UTF-8 is encountered under the throw policy.</exception>
	/// <exception cref="OperationCanceledException">The operation is canceled.</exception>
	public async ValueTask<TextUnit?> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		while ( true ) {
			if ( !await this.EnsureAvailableAsync( 1, cancellationToken ).ConfigureAwait( false ) ) {
				return null;
			}
			if ( this.DecodingMode == TextDecodingMode.Bytes ) {
				return this.ConsumeByte();
			}
			var available = this.myBufferEnd - this.myBufferStart;
			var status = Rune.DecodeFromUtf8(
				this.myBuffer.AsSpan( this.myBufferStart, available ),
				out var scalar,
				out var consumed
			);
			if ( status == OperationStatus.Done ) {
				return this.ConsumeScalar( scalar, consumed );
			}
			if ( (status == OperationStatus.NeedMoreData) && !this.myEndOfStream ) {
				await this.EnsureAvailableAsync(
					Math.Min( 4, available + 1 ),
					cancellationToken
				).ConfigureAwait( false );
				continue;
			}
			return this.HandleInvalidByte();
		}
	}

	private TextUnit ConsumeByte() {
		var value = this.myBuffer[this.myBufferStart];
		this.Advance( 1 );
		return TextUnit.CreateByte( value );
	}

	private TextUnit ConsumeScalar( Rune scalar, int byteCount ) {
		var value = TextUnit.CreateScalar(
			scalar,
			this.myBuffer.AsSpan( this.myBufferStart, byteCount )
		);
		this.Advance( byteCount );
		return value;
	}

	private TextUnit HandleInvalidByte() {
		var value = this.myBuffer[this.myBufferStart];
		return this.InvalidEncodingPolicy switch {
			InvalidEncodingPolicy.PreserveBytes => this.ConsumeInvalidByte( value ),
			InvalidEncodingPolicy.Replace => this.ConsumeReplacement( value ),
			InvalidEncodingPolicy.Throw => throw new DecoderFallbackException(
				$"Invalid UTF-8 input at byte offset {this.ByteOffset}."
			),
			_ => throw new InvalidOperationException( "Unknown invalid-encoding policy." )
		};
	}

	private TextUnit ConsumeInvalidByte( byte value ) {
		this.Advance( 1 );
		return TextUnit.CreateInvalidByte( value );
	}

	private TextUnit ConsumeReplacement( byte value ) {
		this.Advance( 1 );
		return TextUnit.CreateReplacement( value );
	}

	private void Advance( int count ) {
		this.myBufferStart += count;
		this.ByteOffset = checked(this.ByteOffset + count);
	}

	private bool EnsureAvailable( int minimum ) {
		while ( ((this.myBufferEnd - this.myBufferStart) < minimum) && !this.myEndOfStream ) {
			this.CompactBuffer();
			var read = this.myInput.Read(
				this.myBuffer,
				this.myBufferEnd,
				this.myBuffer.Length - this.myBufferEnd
			);
			if ( read == 0 ) {
				this.myEndOfStream = true;
			} else {
				this.myBufferEnd += read;
			}
		}
		return (this.myBufferEnd - this.myBufferStart) >= minimum;
	}

	private async ValueTask<bool> EnsureAvailableAsync(
		int minimum,
		CancellationToken cancellationToken
	) {
		while ( ((this.myBufferEnd - this.myBufferStart) < minimum) && !this.myEndOfStream ) {
			this.CompactBuffer();
			var read = await this.myInput.ReadAsync(
				this.myBuffer.AsMemory(
					this.myBufferEnd,
					this.myBuffer.Length - this.myBufferEnd
				),
				cancellationToken
			).ConfigureAwait( false );
			if ( read == 0 ) {
				this.myEndOfStream = true;
			} else {
				this.myBufferEnd += read;
			}
		}
		return (this.myBufferEnd - this.myBufferStart) >= minimum;
	}

	private void CompactBuffer() {
		if ( this.myBufferStart == 0 ) {
			return;
		}
		var remaining = this.myBufferEnd - this.myBufferStart;
		if ( remaining > 0 ) {
			this.myBuffer.AsSpan( this.myBufferStart, remaining ).CopyTo( this.myBuffer );
		}
		this.myBufferStart = 0;
		this.myBufferEnd = remaining;
	}
}
