namespace Icod.DiffUtils.Diff3;

/// <summary>Selects the GNU <c>diff3</c> edit and conflict policy.</summary>
internal enum Diff3OutputMode {
	/// <summary>Write the default human-readable three-file report.</summary>
	Normal,
	/// <summary>Incorporate all unmerged third-file changes without markers.</summary>
	Ed,
	/// <summary>Incorporate unmerged changes and mark true overlaps.</summary>
	ShowOverlap,
	/// <summary>Incorporate all changes and mark every conflict.</summary>
	ShowAll,
	/// <summary>Incorporate only true overlaps without markers.</summary>
	OverlapOnly,
	/// <summary>Incorporate only true overlaps and surround them with markers.</summary>
	MarkedOverlapOnly,
	/// <summary>Incorporate only nonoverlapping third-file changes.</summary>
	EasyOnly
}
