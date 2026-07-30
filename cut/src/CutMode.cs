namespace Icod.CoreUtils.Cut;

/// <summary>Identifies the positional unit selected by <c>cut</c>.</summary>
internal enum CutMode {
	/// <summary>Select raw byte positions.</summary>
	Bytes,
	/// <summary>Select decoded character positions.</summary>
	Characters,
	/// <summary>Select field positions.</summary>
	Fields
}
