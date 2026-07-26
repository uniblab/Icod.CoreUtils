namespace Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Identifies why a numeric operand could not be parsed.
/// </summary>
public enum QuantityParseErrorKind {
	/// <summary>No error occurred.</summary>
	None,
	/// <summary>The operand was empty.</summary>
	Empty,
	/// <summary>The numeric portion was invalid.</summary>
	InvalidNumber,
	/// <summary>The suffix was not recognized.</summary>
	InvalidSuffix,
	/// <summary>A leading plus sign was not allowed.</summary>
	PositiveSignNotAllowed,
	/// <summary>A leading minus sign was not allowed.</summary>
	NegativeSignNotAllowed,
	/// <summary>The value exceeded the requested destination range.</summary>
	Overflow
}
