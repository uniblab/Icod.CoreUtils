namespace Icod.CoreUtils.Shared.IO;

/// <summary>
/// Writes asynchronously delimited text records.
/// </summary>
public sealed class DelimitedRecordWriter {

	private readonly TextWriter myWriter;
	private readonly char[] mySeparator;

	/// <summary>
	/// Initializes a record writer.
	/// </summary>
	/// <param name="writer">Destination writer. Ownership remains with the caller.</param>
	/// <param name="separator">Record separator, commonly LF or NUL.</param>
	public DelimitedRecordWriter(
		TextWriter writer,
		char separator = '\n'
	) {
		this.myWriter = writer ?? throw new ArgumentNullException(
			nameof( writer )
		);
		this.mySeparator = new char[ 1 ] {
			separator
		};
	}

	/// <summary>
	/// Writes one record followed by the configured separator.
	/// </summary>
	public async ValueTask WriteAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		await this.myWriter.WriteAsync(
			( value ?? string.Empty ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
		await this.myWriter.WriteAsync(
			this.mySeparator.AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Flushes the destination writer.
	/// </summary>
	public Task FlushAsync(
		CancellationToken cancellationToken = default
	) {
		return this.myWriter.FlushAsync(
			cancellationToken
		);
	}

}
