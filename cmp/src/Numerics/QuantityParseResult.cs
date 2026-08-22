namespace Icod.DiffUtils.Cmp.Numerics;

/// <summary>Contains the result of parsing an integer quantity.</summary>
/// <param name="IsSuccess">Whether parsing succeeded.</param>
/// <param name="Value">The parsed value.</param>
/// <param name="ErrorKind">The error kind.</param>
/// <param name="Suffix">The parsed or rejected suffix.</param>
public readonly record struct QuantityParseResult(
	bool IsSuccess,
	long Value,
	QuantityParseErrorKind ErrorKind,
	string Suffix
) {
	/// <summary>Creates a successful result.</summary>
	public static QuantityParseResult Success( long value, string suffix ) {
		return new QuantityParseResult( true, value, QuantityParseErrorKind.None, suffix );
	}

	/// <summary>Creates a failed result.</summary>
	public static QuantityParseResult Failure( QuantityParseErrorKind errorKind, string suffix = "" ) {
		return new QuantityParseResult( false, 0, errorKind, suffix );
	}
}
