namespace Icod.DiffUtils.SDiff;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Represents one common or differing group in an aligned comparison.</summary>
internal sealed class SDiffGroup {
	/// <summary>Initializes a group.</summary>
	/// <param name="leftStart">The zero-based left starting line.</param>
	/// <param name="rightStart">The zero-based right starting line.</param>
	/// <param name="leftLines">The left-side lines.</param>
	/// <param name="rightLines">The right-side lines.</param>
	/// <param name="rows">The aligned display rows.</param>
	/// <param name="isDifferent">Whether the group is an effective difference.</param>
	public SDiffGroup(
		int leftStart,
		int rightStart,
		IReadOnlyList<ComparisonLine> leftLines,
		IReadOnlyList<ComparisonLine> rightLines,
		IReadOnlyList<SDiffRow> rows,
		bool isDifferent
	) {
		this.LeftStart = leftStart;
		this.RightStart = rightStart;
		this.LeftLines = leftLines;
		this.RightLines = rightLines;
		this.Rows = rows;
		this.IsDifferent = isDifferent;
	}

	/// <summary>Gets the zero-based first left line.</summary>
	public int LeftStart { get; }
	/// <summary>Gets the zero-based first right line.</summary>
	public int RightStart { get; }
	/// <summary>Gets the left-side lines.</summary>
	public IReadOnlyList<ComparisonLine> LeftLines { get; }
	/// <summary>Gets the right-side lines.</summary>
	public IReadOnlyList<ComparisonLine> RightLines { get; }
	/// <summary>Gets the display rows.</summary>
	public IReadOnlyList<SDiffRow> Rows { get; }
	/// <summary>Gets whether the group is an effective, nonignored difference.</summary>
	public bool IsDifferent { get; }
}
