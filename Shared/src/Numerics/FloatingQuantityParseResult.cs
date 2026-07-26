namespace Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Contains the result of parsing a floating-point quantity.
/// </summary>
public readonly record struct FloatingQuantityParseResult(
	bool IsSuccess,
	double Value,
	QuantityParseErrorKind ErrorKind,
	string Suffix
) {
	/// <summary>Creates a successful result.</summary>
	public static FloatingQuantityParseResult Success(
		double value,
		string suffix
	) {
		return new FloatingQuantityParseResult(
			true,
			value,
			QuantityParseErrorKind.None,
			suffix
		);
	}

	/// <summary>Creates a failed result.</summary>
	public static FloatingQuantityParseResult Failure(
		QuantityParseErrorKind errorKind,
		string suffix = ""
	) {
		return new FloatingQuantityParseResult(
			false,
			0.0,
			errorKind,
			suffix
		);
	}
}
