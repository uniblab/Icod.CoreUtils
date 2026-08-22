namespace Icod.DiffUtils.Cmp.Numerics;

using System.Numerics;

/// <summary>Parses culture-independent integer quantities using C-style radix detection.</summary>
public static class RadixQuantityParser {
	/// <summary>Parses a 64-bit quantity using an exact suffix table.</summary>
	public static QuantityParseResult ParseInt64(
		string? text,
		NumericSuffixTable? suffixes = null,
		bool allowLeadingPlus = true,
		bool allowLeadingMinus = false,
		OverflowBehavior overflowBehavior = OverflowBehavior.Reject
	) {
		if ( string.IsNullOrEmpty( text ) ) {
			return QuantityParseResult.Failure( QuantityParseErrorKind.Empty );
		}
		suffixes ??= NumericSuffixTable.None;
		var index = 0;
		var negative = false;
		if ( '+' == text[index] ) {
			if ( !allowLeadingPlus ) {
				return QuantityParseResult.Failure( QuantityParseErrorKind.PositiveSignNotAllowed );
			}
			index++;
		} else if ( '-' == text[index] ) {
			if ( !allowLeadingMinus ) {
				return QuantityParseResult.Failure( QuantityParseErrorKind.NegativeSignNotAllowed );
			}
			negative = true;
			index++;
		}
		if ( text.Length <= index ) {
			return QuantityParseResult.Failure( QuantityParseErrorKind.InvalidNumber );
		}

		var radix = 10;
		if ( '0' == text[index] ) {
			if ( index + 1 < text.Length && ( 'x' == text[index + 1] || 'X' == text[index + 1] ) ) {
				radix = 16;
				index += 2;
			} else {
				radix = 8;
			}
		}

		var numberStart = index;
		if ( 8 == radix && '0' == text[index] ) {
			index++;
		}
		while ( index < text.Length && TryGetDigit( text[index], out var parsedDigit ) && parsedDigit < radix ) {
			index++;
		}
		if ( numberStart == index ) {
			return QuantityParseResult.Failure( QuantityParseErrorKind.InvalidNumber );
		}

		BigInteger number = BigInteger.Zero;
		for ( var position = numberStart; position < index; position++ ) {
			_ = TryGetDigit( text[position], out var valueDigit );
			number = number * radix + valueDigit;
		}
		var suffix = text[index..];
		if ( !suffixes.TryGetMultiplier( suffix, out var multiplier ) ) {
			return QuantityParseResult.Failure( QuantityParseErrorKind.InvalidSuffix, suffix );
		}
		var value = number * multiplier;
		if ( negative ) {
			value = -value;
		}
		if ( value < long.MinValue || long.MaxValue < value ) {
			if ( OverflowBehavior.Reject == overflowBehavior ) {
				return QuantityParseResult.Failure( QuantityParseErrorKind.Overflow, suffix );
			}
			return QuantityParseResult.Success( value < long.MinValue ? long.MinValue : long.MaxValue, suffix );
		}
		return QuantityParseResult.Success( (long)value, suffix );
	}

	private static bool TryGetDigit( char character, out int digit ) {
		if ( '0' <= character && character <= '9' ) {
			digit = character - '0';
			return true;
		}
		if ( 'a' <= character && character <= 'f' ) {
			digit = character - 'a' + 10;
			return true;
		}
		if ( 'A' <= character && character <= 'F' ) {
			digit = character - 'A' + 10;
			return true;
		}
		digit = 0;
		return false;
	}
}
