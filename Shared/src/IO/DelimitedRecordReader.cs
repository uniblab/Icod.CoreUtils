namespace Icod.CoreUtils.Shared.IO;

using System.Text;

/// <summary>
/// Reads asynchronously delimited text records from a forward-only <see cref="TextReader"/>.
/// </summary>
public sealed class DelimitedRecordReader {

	private readonly char[] myBuffer;
	private int myCount;
	private bool myEndOfInput;
	private int myIndex;
	private readonly TextReader myReader;
	private readonly char mySeparator;
	private readonly bool myTrimCarriageReturn;

	/// <summary>
	/// Initializes a record reader.
	/// </summary>
	/// <param name="reader">Source text reader. Ownership remains with the caller.</param>
	/// <param name="separator">Record separator, commonly LF or NUL.</param>
	/// <param name="bufferSize">Reusable input buffer size.</param>
	/// <param name="trimCarriageReturn">Whether a CR immediately before the separator is removed.</param>
	public DelimitedRecordReader(
		TextReader reader,
		char separator = '\n',
		int bufferSize = 4096,
		bool? trimCarriageReturn = null
	) {
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		this.myReader = reader ?? throw new ArgumentNullException(
			nameof( reader )
		);
		this.mySeparator = separator;
		this.myBuffer = new char[ bufferSize ];
		this.myTrimCarriageReturn = trimCarriageReturn ?? '\n' == separator;
	}

	/// <summary>
	/// Reads the next record, or <see langword="null"/> at end of input.
	/// </summary>
	public async ValueTask<string?> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( this.myEndOfInput ) {
			return null;
		}

		StringBuilder? builder = null;
		while ( true ) {
			if ( this.myCount <= this.myIndex ) {
				this.myCount = await this.myReader.ReadAsync(
					this.myBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				this.myIndex = 0;
				if ( 0 == this.myCount ) {
					this.myEndOfInput = true;
					if ( null == builder ) {
						return null;
					}
					return builder.ToString();
				}
			}

			var start = this.myIndex;
			while (
				this.myIndex < this.myCount
				&& this.mySeparator != this.myBuffer[ this.myIndex ]
			) {
				this.myIndex++;
			}

			if (
				this.myIndex < this.myCount
				&& this.mySeparator == this.myBuffer[ this.myIndex ]
			) {
				var length = this.myIndex - start;
				this.myIndex++;
				if ( null == builder ) {
					if (
						this.myTrimCarriageReturn
						&& 0 < length
						&& '\r' == this.myBuffer[ start + length - 1 ]
					) {
						length--;
					}
					return new string(
						this.myBuffer,
						start,
						length
					);
				}

				builder.Append(
					this.myBuffer,
					start,
					length
				);
				if (
					this.myTrimCarriageReturn
					&& 0 < builder.Length
					&& '\r' == builder[ builder.Length - 1 ]
				) {
					builder.Length--;
				}
				return builder.ToString();
			}

			builder ??= new StringBuilder();
			builder.Append(
				this.myBuffer,
				start,
				this.myIndex - start
			);
		}
	}

}
