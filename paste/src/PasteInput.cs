namespace Icod.CoreUtils.Paste;

using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Records;

/// <summary>Owns the record reader and optional file resource for one <c>paste</c> input.</summary>
internal sealed class PasteInput : IAsyncDisposable {
	private readonly DelimitedByteRecordSegmentReader myReader;
	private readonly InputSource mySource;

	/// <summary>Initializes an opened paste input.</summary>
	/// <param name="source">The opened binary source.</param>
	/// <param name="recordSeparator">The record-separator byte.</param>
	internal PasteInput( InputSource source, byte recordSeparator ) {
		this.mySource = source ?? throw new ArgumentNullException( nameof( source ) );
		this.myReader = new DelimitedByteRecordSegmentReader( source.BinaryStream!, recordSeparator );
	}

	/// <summary>Gets the user-facing source name.</summary>
	internal string DisplayName => this.mySource.DisplayName;

	/// <summary>Reads the next bounded segment.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The next segment, or <see langword="null"/> at end of input.</returns>
	internal async ValueTask<ByteRecordSegment?> ReadAsync( CancellationToken cancellationToken ) {
		try {
			return await this.myReader.ReadAsync( cancellationToken ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsInputException( exception ) ) {
			throw new PasteInputException( this.DisplayName, exception );
		}
	}

	private static bool IsInputException( Exception exception ) {
		return exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		this.myReader.Dispose();
		await this.mySource.DisposeAsync().ConfigureAwait( false );
	}
}
