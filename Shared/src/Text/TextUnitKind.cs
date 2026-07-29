namespace Icod.CoreUtils.Shared.Text;

/// <summary>Identifies how a <see cref="TextUnit"/> represents source input.</summary>
public enum TextUnitKind {
	/// <summary>The unit is one opaque source byte produced by byte-iteration mode.</summary>
	Byte,
	/// <summary>The unit is a valid decoded Unicode scalar.</summary>
	Scalar,
	/// <summary>The unit is one invalid source byte preserved without replacement.</summary>
	InvalidByte,
	/// <summary>The unit is a replacement scalar that retains the invalid source byte it replaces.</summary>
	Replacement
}
