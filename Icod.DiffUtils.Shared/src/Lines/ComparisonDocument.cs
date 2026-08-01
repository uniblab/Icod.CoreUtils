namespace Icod.DiffUtils.Shared.Lines;

/// <summary>Contains authoritative bytes, decoded lines, and binary-file classification.</summary>
public sealed class ComparisonDocument {
	/// <summary>Initializes a comparison document.</summary>
	public ComparisonDocument(
		ReadOnlyMemory<byte> bytes,
		IReadOnlyList<ComparisonLine> lines,
		bool containsNullByte
	) {
		ArgumentNullException.ThrowIfNull( lines );
		this.Bytes = bytes;
		this.Lines = lines;
		this.ContainsNullByte = containsNullByte;
	}

	/// <summary>Gets the authoritative source bytes.</summary>
	public ReadOnlyMemory<byte> Bytes { get; }
	/// <summary>Gets whether the source contained a NUL byte.</summary>
	public bool ContainsNullByte { get; }
	/// <summary>Gets the logical input lines.</summary>
	public IReadOnlyList<ComparisonLine> Lines { get; }
}
