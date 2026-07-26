namespace Icod.CoreUtils.Shared.IO;

using System.Buffers;
using System.Text;

/// <summary>
/// Exposes a <see cref="TextReader"/> as a forward-only asynchronous byte
/// stream by incrementally encoding characters.
/// </summary>
/// <remarks>
/// The adapter exists for dependency injection and tests where a raw standard
/// input stream is unavailable. It does not materialize the complete input.
/// </remarks>
public sealed class TextReaderStream : Stream {

	private readonly byte[] myByteBuffer;
	private int myByteCount;
	private int myByteIndex;
	private readonly char[] myCharacterBuffer;
	private bool myDisposed;
	private readonly Encoder myEncoder;
	private bool myEncoderCompleted;
	private readonly bool myLeaveOpen;
	private bool myReaderCompleted;
	private readonly TextReader myReader;

	/// <summary>
	/// Initializes a text-reader byte stream.
	/// </summary>
	public TextReaderStream(
		TextReader reader,
		Encoding? encoding = null,
		int characterBufferSize = 4096,
		bool leaveOpen = true
	) {
		this.myReader = reader ?? throw new ArgumentNullException(
			nameof( reader )
		);
		if ( characterBufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( characterBufferSize )
			);
		}

		var selectedEncoding = encoding ?? new UTF8Encoding(
			encoderShouldEmitUTF8Identifier: false,
			throwOnInvalidBytes: false
		);
		this.myEncoder = selectedEncoding.GetEncoder();
		this.myCharacterBuffer = ArrayPool<char>.Shared.Rent(
			characterBufferSize
		);
		this.myByteBuffer = ArrayPool<byte>.Shared.Rent(
			selectedEncoding.GetMaxByteCount(
				characterBufferSize
			)
		);
		this.myLeaveOpen = leaveOpen;
	}

	/// <inheritdoc/>
	public override bool CanRead {
		get {
			return !this.myDisposed;
		}
	}

	/// <inheritdoc/>
	public override bool CanSeek {
		get {
			return false;
		}
	}

	/// <inheritdoc/>
	public override bool CanWrite {
		get {
			return false;
		}
	}

	/// <inheritdoc/>
	public override long Length {
		get {
			throw new NotSupportedException();
		}
	}

	/// <inheritdoc/>
	public override long Position {
		get {
			throw new NotSupportedException();
		}
		set {
			throw new NotSupportedException();
		}
	}

	/// <inheritdoc/>
	public override void Flush() {
		this.ThrowIfDisposed();
	}

	/// <inheritdoc/>
	public override int Read(
		byte[] buffer,
		int offset,
		int count
	) {
		ArgumentNullException.ThrowIfNull(
			buffer
		);
		return this.ReadAsync(
			buffer.AsMemory(
				offset,
				count
			)
		).AsTask().GetAwaiter().GetResult();
	}

	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		this.ThrowIfDisposed();
		cancellationToken.ThrowIfCancellationRequested();
		if ( buffer.IsEmpty ) {
			return 0;
		}

		while ( true ) {
			if ( this.myByteIndex < this.myByteCount ) {
				var count = Math.Min(
					buffer.Length,
					this.myByteCount - this.myByteIndex
				);
				this.myByteBuffer.AsMemory(
					this.myByteIndex,
					count
				).CopyTo(
					buffer.Slice(
						0,
						count
					)
				);
				this.myByteIndex += count;
				return count;
			}

			if ( this.myEncoderCompleted ) {
				return 0;
			}

			await this.FillByteBufferAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	/// <inheritdoc/>
	public override long Seek(
		long offset,
		SeekOrigin origin
	) {
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void SetLength(
		long value
	) {
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Write(
		byte[] buffer,
		int offset,
		int count
	) {
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	protected override void Dispose(
		bool disposing
	) {
		if ( this.myDisposed ) {
			return;
		}
		this.myDisposed = true;
		ArrayPool<char>.Shared.Return(
			this.myCharacterBuffer
		);
		ArrayPool<byte>.Shared.Return(
			this.myByteBuffer
		);
		if (
			disposing
			&& !this.myLeaveOpen
		) {
			this.myReader.Dispose();
		}
		base.Dispose(
			disposing
		);
	}

	private async Task FillByteBufferAsync(
		CancellationToken cancellationToken
	) {
		this.myByteIndex = 0;
		this.myByteCount = 0;

		if ( !this.myReaderCompleted ) {
			var characterCount = await this.myReader.ReadAsync(
				this.myCharacterBuffer.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 < characterCount ) {
				this.myEncoder.Convert(
					this.myCharacterBuffer.AsSpan(
						0,
						characterCount
					),
					this.myByteBuffer.AsSpan(),
					flush: false,
					out var charactersUsed,
					out var bytesUsed,
					out _
				);
				if ( charactersUsed != characterCount ) {
					throw new InvalidOperationException(
						"The internal encoding buffer was unexpectedly too small."
					);
				}
				this.myByteCount = bytesUsed;
				return;
			}
			this.myReaderCompleted = true;
		}

		this.myEncoder.Convert(
			ReadOnlySpan<char>.Empty,
			this.myByteBuffer.AsSpan(),
			flush: true,
			out _,
			out this.myByteCount,
			out this.myEncoderCompleted
		);
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf(
			this.myDisposed,
			this
		);
	}

}
