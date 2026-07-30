namespace Icod.CoreUtils.NL;

/// <summary>Specifies how a generated line number is padded.</summary>
internal enum NlNumberFormat {
	/// <summary>Left-justifies the number with trailing spaces.</summary>
	Left,

	/// <summary>Right-justifies the number with leading spaces.</summary>
	Right,

	/// <summary>Right-justifies the number with leading zeroes after any sign.</summary>
	RightZero
}
