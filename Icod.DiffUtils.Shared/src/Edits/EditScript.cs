namespace Icod.DiffUtils.Shared.Edits;

/// <summary>Contains the aligned operations and contiguous changed blocks for a two-way comparison.</summary>
public sealed class EditScript {
	/// <summary>Initializes an edit script.</summary>
	public EditScript(
		IReadOnlyList<EditOperation> operations,
		IReadOnlyList<DifferenceBlock> differences
	) {
		ArgumentNullException.ThrowIfNull( operations );
		ArgumentNullException.ThrowIfNull( differences );
		this.Operations = operations;
		this.Differences = differences;
	}

	/// <summary>Gets whether the inputs differ.</summary>
	public bool HasDifferences => 0 < this.Differences.Count;
	/// <summary>Gets all aligned operations.</summary>
	public IReadOnlyList<EditOperation> Operations { get; }
	/// <summary>Gets contiguous difference blocks.</summary>
	public IReadOnlyList<DifferenceBlock> Differences { get; }
}
