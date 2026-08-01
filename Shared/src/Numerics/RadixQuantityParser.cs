namespace Icod.CoreUtils.Shared.Numerics;

using System.Numerics;

/// <summary>
/// Parses culture-independent integer quantities whose numeric portion may use
/// decimal, leading-zero octal, or <c>0x</c>-prefixed hexadecimal notation.
/// </summary>
public static class RadixQuantityParser {

	/// <summary>
	/// Parses a signed or unsigned 64-bit integer quantity using C-style base
	/// detection and an exact suffix table.
	/// </summary>
	/// <param name="text">The complete numeric operand.</param>
	/// <param name="suffixes">The accepted exact suffix multipliers.</param>
	/// <param name="allowLeadingPlus">Whether a leading plus sign is accepted.</param>
	/// <param name="allowLeadingMinus">Whether a leading minus sign is accepted.</param>
	/// <param name="overflowBehavior">How values outside the 64-bit range are handled.</param>
	/// <returns>The parsed value or a structured parse failure.</returns>
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
			if (
				index + 1 < text.Length
				&& ( 'x' == text[index + 1] || 'X' == text[index + 1] )
			) {
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
			return QuantityParseResult.Success(
				value < long.MinValue ? long.MinValue : long.MaxValue,
				suffix
			);
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
