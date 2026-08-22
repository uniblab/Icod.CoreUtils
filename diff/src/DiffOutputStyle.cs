namespace Icod.DiffUtils.Diff;

/// <summary>Identifies the selected GNU <c>diff</c> output format.</summary>
internal enum DiffOutputStyle {
	/// <summary>Traditional change commands with old and new lines.</summary>
	Normal,
	/// <summary>Only a one-line difference summary.</summary>
	Brief,
	/// <summary>Context format.</summary>
	Context,
	/// <summary>Unified context format.</summary>
	Unified,
	/// <summary>Reverse-order <c>ed</c> commands.</summary>
	Ed,
	/// <summary>Forward-order <c>ed</c>-like commands.</summary>
	ForwardEd,
	/// <summary>RCS command format.</summary>
	Rcs,
	/// <summary>Two-column presentation.</summary>
	SideBySide,
	/// <summary>C-preprocessor conditional merge format.</summary>
	IfDef
}
