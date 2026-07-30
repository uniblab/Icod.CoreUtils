namespace Icod.CoreUtils.Shared.Records;

/// <summary>Writes byte-record content and separators without choosing termination policy for the caller.</summary>
/// <remarks>The caller retains ownership of the destination stream.</remarks>
public sealed class DelimitedByteRecordWriter {

	private readonly byte[] mySeparatorBytes;
	private readonly Stream myStream;

	/// <summary>Initializes a byte-record writer.</summary>
	/// <param name="stream">The writable destination stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record separator.</param>
	public DelimitedByteRecordWriter(
		Stream stream,
		RecordSeparator separator
	) {
		ArgumentNullException.ThrowIfNull( stream );
		if ( !stream.CanWrite ) {
			throw new ArgumentException(
				"The destination stream must be writable.",
				nameof( stream )
			);
		}
		this.myStream = stream;
		this.mySeparatorBytes = new[] { separator.Value };
	}

	/// <summary>Initializes a byte-record writer using a separator byte.</summary>
	/// <param name="stream">The writable destination stream. Ownership remains with the caller.</param>
	/// <param name="separator">The one-byte record separator.</param>
	public DelimitedByteRecordWriter(
		Stream stream,
		byte separator = (byte)'\n'
	) : this(
		stream,
		new RecordSeparator( separator )
	) {
	}

	/// <summary>Writes record content without a separator.</summary>
	/// <param name="content">The content bytes.</param>
	/// <param name="cancellationToken">A token that may cancel the write.</param>
	public async ValueTask WriteContentAsync(
		ReadOnlyMemory<byte> content,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( content.IsEmpty ) {
			return;
		}
		await this.myStream.WriteAsync(
			content,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>Writes one configured separator byte.</summary>
	/// <param name="cancellationToken">A token that may cancel the write.</param>
	public async ValueTask WriteSeparatorAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.myStream.WriteAsync(
			this.mySeparatorBytes.AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>Writes a complete record with caller-selected termination.</summary>
	/// <param name="content">The content bytes, excluding the separator.</param>
	/// <param name="terminate">Whether to append the configured separator.</param>
	/// <param name="cancellationToken">A token that may cancel the write.</param>
	public async ValueTask WriteRecordAsync(
		ReadOnlyMemory<byte> content,
		bool terminate,
		CancellationToken cancellationToken = default
	) {
		await this.WriteContentAsync(
			content,
			cancellationToken
		).ConfigureAwait( false );
		if ( terminate ) {
			await this.WriteSeparatorAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	/// <summary>Flushes the caller-owned destination stream.</summary>
	/// <param name="cancellationToken">A token that may cancel the flush.</param>
	public async ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.myStream.FlushAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

}
