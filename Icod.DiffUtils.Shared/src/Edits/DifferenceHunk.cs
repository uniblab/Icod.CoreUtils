namespace Icod.DiffUtils.Shared.Edits;

/// <summary>Represents a context-expanded range of edit operations.</summary>
public sealed class DifferenceHunk {
	/// <summary>Initializes a difference hunk.</summary>
	public DifferenceHunk(
		int oldStart,
		int oldLength,
		int newStart,
		int newLength,
		IReadOnlyList<EditOperation> operations
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( oldStart );
		ArgumentOutOfRangeException.ThrowIfNegative( oldLength );
		ArgumentOutOfRangeException.ThrowIfNegative( newStart );
		ArgumentOutOfRangeException.ThrowIfNegative( newLength );
		ArgumentNullException.ThrowIfNull( operations );
		this.OldStart = oldStart;
		this.OldLength = oldLength;
		this.NewStart = newStart;
		this.NewLength = newLength;
		this.Operations = operations;
	}

	/// <summary>Gets the first zero-based old-line position represented by the hunk.</summary>
	public int OldStart { get; }
	/// <summary>Gets the number of old lines represented by the hunk.</summary>
	public int OldLength { get; }
	/// <summary>Gets the first zero-based new-line position represented by the hunk.</summary>
	public int NewStart { get; }
	/// <summary>Gets the number of new lines represented by the hunk.</summary>
	public int NewLength { get; }
	/// <summary>Gets the context-expanded operations.</summary>
	public IReadOnlyList<EditOperation> Operations { get; }
}
