namespace Icod.DiffUtils.Shared.Edits;

/// <summary>Describes one contiguous group of insertions and deletions.</summary>
public sealed class DifferenceBlock {
	/// <summary>Initializes a difference block.</summary>
	public DifferenceBlock(
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

	/// <summary>Gets the first zero-based old-line position.</summary>
	public int OldStart { get; }
	/// <summary>Gets the number of deleted old lines.</summary>
	public int OldLength { get; }
	/// <summary>Gets the first zero-based new-line position.</summary>
	public int NewStart { get; }
	/// <summary>Gets the number of inserted new lines.</summary>
	public int NewLength { get; }
	/// <summary>Gets the edit operations in the block.</summary>
	public IReadOnlyList<EditOperation> Operations { get; }
}
