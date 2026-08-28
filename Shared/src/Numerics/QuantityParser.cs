/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Numerics;

using System.Globalization;
using System.Numerics;

/// <summary>
/// Parses culture-independent numeric operands and exact suffixes.
/// </summary>
public static class QuantityParser {

	/// <summary>
	/// Parses a signed or unsigned 64-bit integer quantity.
	/// </summary>
	public static QuantityParseResult ParseInt64(
		string? text,
		NumericSuffixTable? suffixes = null,
		bool allowLeadingPlus = true,
		bool allowLeadingMinus = false,
		OverflowBehavior overflowBehavior = OverflowBehavior.Reject
	) {
		if ( string.IsNullOrEmpty( text ) ) {
			return QuantityParseResult.Failure(
				QuantityParseErrorKind.Empty
			);
		}
		suffixes ??= NumericSuffixTable.None;

		var index = 0;
		var negative = false;
		if ( '+' == text[ index ] ) {
			if ( !allowLeadingPlus ) {
				return QuantityParseResult.Failure(
					QuantityParseErrorKind.PositiveSignNotAllowed
				);
			}
			index++;
		} else if ( '-' == text[ index ] ) {
			if ( !allowLeadingMinus ) {
				return QuantityParseResult.Failure(
					QuantityParseErrorKind.NegativeSignNotAllowed
				);
			}
			negative = true;
			index++;
		}

		var numberStart = index;
		while (
			index < text.Length
			&& char.IsDigit( text[ index ] )
		) {
			index++;
		}
		if ( numberStart == index ) {
			return QuantityParseResult.Failure(
				QuantityParseErrorKind.InvalidNumber
			);
		}

		if (
			!BigInteger.TryParse(
				text.Substring(
					numberStart,
					index - numberStart
				),
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var number
			)
		) {
			return QuantityParseResult.Failure(
				QuantityParseErrorKind.InvalidNumber
			);
		}

		var suffix = text.Substring(
			index
		);
		if (
			!suffixes.TryGetMultiplier(
				suffix,
				out var multiplier
			)
		) {
			return QuantityParseResult.Failure(
				QuantityParseErrorKind.InvalidSuffix,
				suffix
			);
		}

		var value = number * multiplier;
		if ( negative ) {
			value = -value;
		}
		if (
			value < long.MinValue
			|| long.MaxValue < value
		) {
			if ( OverflowBehavior.Reject == overflowBehavior ) {
				return QuantityParseResult.Failure(
					QuantityParseErrorKind.Overflow,
					suffix
				);
			}
			return QuantityParseResult.Success(
				value < long.MinValue
					? long.MinValue
					: long.MaxValue,
				suffix
			);
		}

		return QuantityParseResult.Success(
			(long)value,
			suffix
		);
	}

	/// <summary>
	/// Parses a finite, culture-independent floating-point quantity.
	/// </summary>
	public static FloatingQuantityParseResult ParseDouble(
		string? text,
		FloatingSuffixTable? suffixes = null,
		bool allowLeadingPlus = true,
		bool allowLeadingMinus = true
	) {
		if ( string.IsNullOrEmpty( text ) ) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.Empty
			);
		}
		suffixes ??= new FloatingSuffixTable(
			new Dictionary<string, double>( StringComparer.Ordinal ) {
				[ string.Empty ] = 1.0
			}
		);

		if (
			'+' == text[ 0 ]
			&& !allowLeadingPlus
		) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.PositiveSignNotAllowed
			);
		}
		if (
			'-' == text[ 0 ]
			&& !allowLeadingMinus
		) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.NegativeSignNotAllowed
			);
		}

		var index = 0;
		if (
			'+' == text[ index ]
			|| '-' == text[ index ]
		) {
			index++;
		}

		var sawDigit = false;
		var sawDecimalPoint = false;
		while ( index < text.Length ) {
			var character = text[ index ];
			if ( '0' <= character && character <= '9' ) {
				sawDigit = true;
				index++;
			} else if (
				'.' == character
				&& !sawDecimalPoint
			) {
				sawDecimalPoint = true;
				index++;
			} else {
				break;
			}
		}
		if ( !sawDigit ) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.InvalidNumber
			);
		}

		if (
			index < text.Length
			&& ( 'e' == text[ index ] || 'E' == text[ index ] )
		) {
			var exponentMarker = index;
			index++;
			if (
				index < text.Length
				&& ( '+' == text[ index ] || '-' == text[ index ] )
			) {
				index++;
			}
			var exponentStart = index;
			while (
				index < text.Length
				&& '0' <= text[ index ]
				&& text[ index ] <= '9'
			) {
				index++;
			}
			if ( exponentStart == index ) {
				index = exponentMarker;
			}
		}

		if (
			!double.TryParse(
				text.Substring(
					0,
					index
				),
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out var number
			)
			|| !double.IsFinite( number )
		) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.InvalidNumber
			);
		}

		var suffix = text.Substring(
			index
		);
		if (
			!suffixes.TryGetMultiplier(
				suffix,
				out var multiplier
			)
		) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.InvalidSuffix,
				suffix
			);
		}

		var value = number * multiplier;
		if ( !double.IsFinite( value ) ) {
			return FloatingQuantityParseResult.Failure(
				QuantityParseErrorKind.Overflow,
				suffix
			);
		}
		return FloatingQuantityParseResult.Success(
			value,
			suffix
		);
	}

}
