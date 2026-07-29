namespace Icod.CoreUtils.Fold;

/// <summary>Identifies the quantity measured by <c>fold</c>.</summary>
internal enum FoldCountingMode {
	/// <summary>Counts terminal display columns.</summary>
	DisplayColumns,
	/// <summary>Counts exact source bytes.</summary>
	Bytes,
	/// <summary>Counts decoded scalar or preserved invalid-byte units.</summary>
	Characters
}
