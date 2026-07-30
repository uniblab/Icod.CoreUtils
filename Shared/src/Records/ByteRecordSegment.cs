namespace Icod.CoreUtils.Shared.Records;

/// <summary>Represents one independently owned segment of a byte record.</summary>
public sealed class ByteRecordSegment {

	private readonly byte[] myData;

	/// <summary>Initializes a byte-record segment by copying its data.</summary>
	/// <param name="data">The segment bytes, excluding any record separator.</param>
	/// <param name="endsRecord">Whether this segment completes a record.</param>
	/// <param name="isTerminated">Whether a separator completed the record.</param>
	public ByteRecordSegment(
		ReadOnlySpan<byte> data,
		bool endsRecord,
		bool isTerminated
	) {
		if ( isTerminated && !endsRecord ) {
			throw new ArgumentException(
				"A terminated segment must end its record.",
				nameof( isTerminated )
			);
		}
		this.myData = data.ToArray();
		this.EndsRecord = endsRecord;
		this.IsTerminated = isTerminated;
	}

	/// <summary>Gets the segment bytes, excluding any record separator.</summary>
	public ReadOnlyMemory<byte> Data => this.myData;

	/// <summary>Gets whether this segment completes its record.</summary>
	public bool EndsRecord { get; }

	/// <summary>Gets whether a separator completed the record.</summary>
	public bool IsTerminated { get; }

}
