namespace Icod.DiffUtils.Shared.Merge;

/// <summary>Classifies one connected three-way change region.</summary>
public enum ThreeWayChangeKind {
	/// <summary>Only the first descendant differs from the common ancestor.</summary>
	MineOnly,
	/// <summary>The descendants agree with each other but differ from the common ancestor.</summary>
	OlderOnly,
	/// <summary>Only the third input differs from the common ancestor.</summary>
	YoursOnly,
	/// <summary>Both descendants change an interacting ancestor range differently.</summary>
	Overlap
}
