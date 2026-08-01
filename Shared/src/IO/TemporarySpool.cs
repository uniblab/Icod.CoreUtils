namespace Icod.CoreUtils.Shared.IO;

/// <summary>
/// Owns an asynchronous temporary read/write stream and reliably removes its backing file.
/// </summary>
public sealed class TemporarySpool : IDisposable, IAsyncDisposable {

	private bool myDisposed;

	/// <summary>Gets the temporary backing-file path.</summary>
	public string Path {
		get;
	}

	/// <summary>Gets the read/write spool stream.</summary>
	public FileStream Stream {
		get;
	}

	private TemporarySpool(
		string path,
		FileStream stream
	) {
		this.Path = path;
		this.Stream = stream;
	}

	/// <summary>
	/// Creates a temporary spool.
	/// </summary>
	/// <param name="directory">The directory in which to create the spool, or <see langword="null"/> for the process temporary directory.</param>
	/// <param name="bufferSize">The stream buffer size.</param>
	/// <param name="fileNamePrefix">The leaf-name prefix used to identify the owned spool.</param>
	/// <returns>The created temporary spool.</returns>
	public static TemporarySpool Create(
		string? directory = null,
		int bufferSize = StreamOperations.DefaultBufferSize,
		string fileNamePrefix = "icod-coreutils-"
	) {
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		ArgumentException.ThrowIfNullOrEmpty( fileNamePrefix );
		if ( 0 <= fileNamePrefix.IndexOfAny( System.IO.Path.GetInvalidFileNameChars() ) ) {
			throw new ArgumentException(
				"The temporary spool prefix must be a valid file-name prefix.",
				nameof( fileNamePrefix )
			);
		}
		directory ??= System.IO.Path.GetTempPath();
		Directory.CreateDirectory(
			directory
		);
		var path = System.IO.Path.Combine(
			directory,
			string.Concat(
				fileNamePrefix,
				System.IO.Path.GetRandomFileName(),
				".tmp"
			)
		);
		var stream = new FileStream(
			path,
			FileMode.CreateNew,
			FileAccess.ReadWrite,
			FileShare.Read,
			bufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
		);
		return new TemporarySpool(
			path,
			stream
		);
	}

	/// <summary>
	/// Flushes pending writes and rewinds the stream for reading.
	/// </summary>
	public async Task RewindAsync(
		CancellationToken cancellationToken = default
	) {
		this.ThrowIfDisposed();
		await this.Stream.FlushAsync(
			cancellationToken
		).ConfigureAwait( false );
		this.Stream.Seek(
			0,
			SeekOrigin.Begin
		);
	}

	/// <inheritdoc/>
	public void Dispose() {
		if ( this.myDisposed ) {
			return;
		}
		this.myDisposed = true;
		this.Stream.Dispose();
		this.DeleteBackingFile();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		if ( this.myDisposed ) {
			return;
		}
		this.myDisposed = true;
		await this.Stream.DisposeAsync().ConfigureAwait( false );
		this.DeleteBackingFile();
	}

	private void DeleteBackingFile() {
		try {
			if ( File.Exists( this.Path ) ) {
				File.Delete(
					this.Path
				);
			}
		} catch {
			// DeleteOnClose is the primary cleanup mechanism; cleanup must not mask the caller's result.
		}
	}

	private void ThrowIfDisposed() {
		ObjectDisposedException.ThrowIf(
			this.myDisposed,
			this
		);
	}

}
