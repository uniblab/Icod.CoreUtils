namespace Icod.CoreUtils.Cut;

/// <summary>Wraps one input stream so source failures remain distinguishable from output failures.</summary>
/// <remarks>The wrapper never owns or disposes the underlying stream.</remarks>
internal sealed class CutInputStream : Stream {
	private readonly string myDisplayName;
	private readonly Stream myStream;

	/// <summary>Initializes an input wrapper.</summary>
	/// <param name="stream">The underlying caller-owned input stream.</param>
	/// <param name="displayName">The user-facing source name.</param>
	internal CutInputStream( Stream stream, string displayName ) {
		this.myStream = stream ?? throw new ArgumentNullException( nameof( stream ) );
		this.myDisplayName = displayName ?? throw new ArgumentNullException( nameof( displayName ) );
	}

	/// <inheritdoc/>
	public override bool CanRead => this.myStream.CanRead;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position {
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Flush() { }

	/// <inheritdoc/>
	public override int Read( byte[] buffer, int offset, int count ) {
		try {
			return this.myStream.Read( buffer, offset, count );
		} catch ( Exception exception ) when ( IsInputException( exception ) ) {
			throw new CutInputException( this.myDisplayName, exception );
		}
	}

	/// <inheritdoc/>
	public override int Read( Span<byte> buffer ) {
		try {
			return this.myStream.Read( buffer );
		} catch ( Exception exception ) when ( IsInputException( exception ) ) {
			throw new CutInputException( this.myDisplayName, exception );
		}
	}

	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		try {
			return await this.myStream.ReadAsync( buffer, cancellationToken ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsInputException( exception ) ) {
			throw new CutInputException( this.myDisplayName, exception );
		}
	}

	/// <inheritdoc/>
	public override Task<int> ReadAsync(
		byte[] buffer,
		int offset,
		int count,
		CancellationToken cancellationToken
	) {
		return this.ReadArrayAsync( buffer, offset, count, cancellationToken );
	}

	private async Task<int> ReadArrayAsync(
		byte[] buffer,
		int offset,
		int count,
		CancellationToken cancellationToken
	) {
		try {
			return await this.myStream.ReadAsync( buffer, offset, count, cancellationToken ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsInputException( exception ) ) {
			throw new CutInputException( this.myDisplayName, exception );
		}
	}

	/// <inheritdoc/>
	public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength( long value ) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();

	private static bool IsInputException( Exception exception ) {
		return exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;
	}
}

/// <summary>Reports an input failure together with its user-facing source name.</summary>
internal sealed class CutInputException : IOException {
	/// <summary>Initializes an input exception.</summary>
	/// <param name="displayName">The user-facing source name.</param>
	/// <param name="innerException">The original input exception.</param>
	internal CutInputException( string displayName, Exception innerException )
		: base( innerException?.Message, innerException ) {
		this.DisplayName = displayName ?? throw new ArgumentNullException( nameof( displayName ) );
	}

	/// <summary>Gets the user-facing source name.</summary>
	internal string DisplayName { get; }
}
