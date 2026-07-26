namespace Icod.CoreUtils.Shared.IO;

using System.Buffers;
using System.Text;

/// <summary>
/// Provides a write-only byte stream over either a binary stream or a fallback
/// text writer.
/// </summary>
/// <remarks>
/// When a binary stream is available, bytes are forwarded without modification.
/// The text-writer fallback decodes bytes incrementally with the configured
/// encoding and is intended for dependency injection and tests. The class does
/// not own either supplied destination.
/// </remarks>
public sealed class ByteOutputStream : Stream {

	private readonly Stream? myBinaryStream;
	private bool myCompleted;
	private readonly Decoder myDecoder;
	private bool myDisposed;
	private readonly Encoding myEncoding;
	private readonly TextWriter myTextWriter;

	/// <summary>
	/// Initializes a byte output stream.
	/// </summary>
	/// <param name="textWriter">Fallback text destination.</param>
	/// <param name="binaryStream">Preferred byte-preserving destination.</param>
	/// <param name="fallbackEncoding">Encoding used only by the text fallback.</param>
	public ByteOutputStream(
		TextWriter textWriter,
		Stream? binaryStream = null,
		Encoding? fallbackEncoding = null
	) {
		this.myTextWriter = textWriter ?? throw new ArgumentNullException(
			nameof( textWriter )
		);
		if (
			null != binaryStream
			&& !binaryStream.CanWrite
		) {
			throw new ArgumentException(
				"The binary destination must be writable.",
				nameof( binaryStream )
			);
		}
		this.myBinaryStream = binaryStream;
		this.myEncoding = fallbackEncoding ?? new UTF8Encoding(
			encoderShouldEmitUTF8Identifier: false,
			throwOnInvalidBytes: false
		);
		this.myDecoder = this.myEncoding.GetDecoder();
	}

	/// <inheritdoc/>
	public override bool CanRead {
		get {
			return false;
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
			return !this.myDisposed && !this.myCompleted;
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

	/// <summary>
	/// Completes fallback decoding and flushes the destination.
	/// </summary>
	public async Task CompleteAsync(
		CancellationToken cancellationToken = default
	) {
		this.ThrowIfDisposed();
		if ( this.myCompleted ) {
			return;
		}

		if ( null == this.myBinaryStream ) {
			await this.FlushDecoderAsync(
				flush: true,
				cancellationToken
			).ConfigureAwait( false );
		}
		this.myCompleted = true;
		await this.FlushDestinationAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Encodes and writes text as UTF-8-compatible output bytes.
	/// </summary>
	public async ValueTask WriteTextAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			value
		);
		var bytes = this.myEncoding.GetBytes(
			value
		);
		await this.WriteAsync(
			bytes.AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <inheritdoc/>
	public override void Flush() {
		this.ThrowIfUnavailable();
		if ( null != this.myBinaryStream ) {
			this.myBinaryStream.Flush();
		} else {
			this.myTextWriter.Flush();
		}
	}

	/// <inheritdoc/>
	public override Task FlushAsync(
		CancellationToken cancellationToken
	) {
		this.ThrowIfUnavailable();
		return this.FlushDestinationAsync(
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public override int Read(
		byte[] buffer,
		int offset,
		int count
	) {
		throw new NotSupportedException();
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
		ArgumentNullException.ThrowIfNull(
			buffer
		);
		this.ThrowIfUnavailable();
		if ( null != this.myBinaryStream ) {
			this.myBinaryStream.Write(
				buffer,
				offset,
				count
			);
			return;
		}

		var characters = ArrayPool<char>.Shared.Rent(
			this.myEncoding.GetMaxCharCount(
				count
			)
		);
		try {
			this.myDecoder.Convert(
				buffer,
				offset,
				count,
				characters,
				0,
				characters.Length,
				flush: false,
				out _,
				out var charactersUsed,
				out _
			);
			if ( 0 < charactersUsed ) {
				this.myTextWriter.Write(
					characters,
					0,
					charactersUsed
				);
			}
		} finally {
			ArrayPool<char>.Shared.Return(
				characters
			);
		}
	}

	/// <inheritdoc/>
	public override async Task WriteAsync(
		byte[] buffer,
		int offset,
		int count,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull(
			buffer
		);
		await this.WriteAsync(
			buffer.AsMemory(
				offset,
				count
			),
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		this.ThrowIfUnavailable();
		cancellationToken.ThrowIfCancellationRequested();
		if ( buffer.IsEmpty ) {
			return;
		}
		if ( null != this.myBinaryStream ) {
			await this.myBinaryStream.WriteAsync(
				buffer,
				cancellationToken
			).ConfigureAwait( false );
			return;
		}

		var characters = ArrayPool<char>.Shared.Rent(
			this.myEncoding.GetMaxCharCount(
				buffer.Length
			)
		);
		try {
			this.myDecoder.Convert(
				buffer.Span,
				characters.AsSpan(),
				flush: false,
				out _,
				out var charactersUsed,
				out _
			);
			if ( 0 < charactersUsed ) {
				await this.myTextWriter.WriteAsync(
					characters.AsMemory(
						0,
						charactersUsed
					),
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<char>.Shared.Return(
				characters
			);
		}
	}

	/// <inheritdoc/>
	protected override void Dispose(
		bool disposing
	) {
		this.myDisposed = true;
		base.Dispose(
			disposing
		);
	}

	private async Task FlushDecoderAsync(
		bool flush,
		CancellationToken cancellationToken
	) {
		var characters = ArrayPool<char>.Shared.Rent(
			this.myEncoding.GetMaxCharCount(
				0
			)
		);
		try {
			this.myDecoder.Convert(
				ReadOnlySpan<byte>.Empty,
				characters.AsSpan(),
				flush,
				out _,
				out var charactersUsed,
				out _
			);
			if ( 0 < charactersUsed ) {
				await this.myTextWriter.WriteAsync(
					characters.AsMemory(
						0,
						charactersUsed
					),
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<char>.Shared.Return(
				characters
			);
		}
	}

	private Task FlushDestinationAsync(
		CancellationToken cancellationToken
	) {
		if ( null != this.myBinaryStream ) {
			return this.myBinaryStream.FlushAsync(
				cancellationToken
			);
		}
		return this.myTextWriter.FlushAsync(
			cancellationToken
		);
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf(
			this.myDisposed,
			this
		);
	}

	private void ThrowIfUnavailable() {
		this.ThrowIfDisposed();
		if ( this.myCompleted ) {
			throw new InvalidOperationException(
				"The output stream has already been completed."
			);
		}
	}

}
