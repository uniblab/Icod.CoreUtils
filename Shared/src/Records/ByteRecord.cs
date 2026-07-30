namespace Icod.CoreUtils.Shared.Records;

/// <summary>Represents one independently owned byte record without its separator.</summary>
public sealed class ByteRecord {

	private readonly byte[] myContent;

	/// <summary>Initializes a byte record by copying its content.</summary>
	/// <param name="content">The record content, excluding its separator.</param>
	/// <param name="isTerminated">Whether a separator terminated the record.</param>
	public ByteRecord(
		ReadOnlySpan<byte> content,
		bool isTerminated
	) {
		this.myContent = content.ToArray();
		this.IsTerminated = isTerminated;
	}

	/// <summary>Gets the record content, excluding its separator.</summary>
	public ReadOnlyMemory<byte> Content => this.myContent;

	/// <summary>Gets whether a separator terminated the record.</summary>
	public bool IsTerminated { get; }

}
