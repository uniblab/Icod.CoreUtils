namespace Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Contains the result of parsing an integer quantity.
/// </summary>
public readonly record struct QuantityParseResult(
	bool IsSuccess,
	long Value,
	QuantityParseErrorKind ErrorKind,
	string Suffix
) {
	/// <summary>Creates a successful result.</summary>
	public static QuantityParseResult Success(
		long value,
		string suffix
	) {
		return new QuantityParseResult(
			true,
			value,
			QuantityParseErrorKind.None,
			suffix
		);
	}

	/// <summary>Creates a failed result.</summary>
	public static QuantityParseResult Failure(
		QuantityParseErrorKind errorKind,
		string suffix = ""
	) {
		return new QuantityParseResult(
			false,
			0,
			errorKind,
			suffix
		);
	}
}
