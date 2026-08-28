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

namespace Icod.CoreUtils.Shared.Ranges;

using System.Globalization;

/// <summary>Parses GNU-style comma-, ASCII-space-, or horizontal-tab-separated unsigned positional ranges.</summary>
public static class RangeListParser {

	/// <summary>Parses and normalizes a positional range list.</summary>
	/// <param name="value">The list containing <c>N</c>, <c>N-</c>, <c>N-M</c>, or <c>-M</c> forms.</param>
	/// <param name="options">Optional grammar and domain settings.</param>
	/// <returns>A structured success or failure result.</returns>
	public static RangeParseResult Parse(
		string value,
		RangeListParserOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( value );
		options ??= new RangeListParserOptions();
		if ( options.MaximumValue < options.MinimumValue ) {
			throw new ArgumentException(
				"The maximum endpoint cannot precede the minimum endpoint.",
				nameof( options )
			);
		}
		if ( 0 == value.Length ) {
			return Failure(
				RangeParseErrorCode.EmptyList,
				0,
				String.Empty,
				"The range list is empty."
			);
		}
		var ranges = new List<InclusiveRange>();
		var index = 0;
		while ( index < value.Length ) {
			if ( IsSeparator( value[index] ) ) {
				return Failure(
					RangeParseErrorCode.ExpectedNumber,
					index,
					value[index].ToString(),
					"A range endpoint was expected."
				);
			}
			var tokenStart = index;
			while ( index < value.Length && !IsSeparator( value[index] ) ) {
				index++;
			}
			var token = value[tokenStart..index];
			var parsed = ParseToken(
				token,
				tokenStart,
				options
			);
			if ( parsed.Error is { } error ) {
				return RangeParseResult.Failed( error );
			}
			ranges.Add( parsed.Range!.Value );
			if ( index < value.Length ) {
				index++;
				if ( index == value.Length || IsSeparator( value[index] ) ) {
					return Failure(
						RangeParseErrorCode.ExpectedNumber,
						index,
						index < value.Length ? value[index].ToString() : String.Empty,
						"A range endpoint was expected."
					);
				}
			}
		}
		var set = new RangeSet( ranges );
		if ( options.Complement ) {
			set = set.Complement( options.MinimumValue );
		}
		return RangeParseResult.Succeeded( set );
	}

	private static (InclusiveRange? Range, RangeParseError? Error) ParseToken(
		string token,
		int tokenStart,
		RangeListParserOptions options
	) {
		var firstDash = token.IndexOf( '-' );
		if ( 0 > firstDash ) {
			var number = ParseNumber( token, tokenStart, options );
			return null == number.Error
				? (new InclusiveRange( number.Value, number.Value ), null)
				: (null, number.Error)
			;
		}
		if ( firstDash != token.LastIndexOf( '-' ) ) {
			return (
				null,
				new RangeParseError(
					RangeParseErrorCode.MultipleDashes,
					tokenStart + token.LastIndexOf( '-' ),
					token,
					"A range may contain only one hyphen."
				)
			);
		}
		var left = token[..firstDash];
		var right = token[( firstDash + 1 )..];
		if ( 0 == left.Length && 0 == right.Length ) {
			if ( !options.AllowSingleDash ) {
				return (
					null,
					new RangeParseError(
						RangeParseErrorCode.MissingEndpoint,
						tokenStart,
						token,
						"The range has no endpoint."
					)
				);
			}
			return (new InclusiveRange( options.MinimumValue, null ), null);
		}
		if ( 0 == left.Length ) {
			if ( !options.AllowLeadingOpenRange ) {
				return (
					null,
					new RangeParseError(
						RangeParseErrorCode.LeadingOpenRangeNotAllowed,
						tokenStart,
						token,
						"Leading-open ranges are disabled."
					)
				);
			}
			var end = ParseNumber( right, tokenStart + 1, options );
			return null == end.Error
				? (new InclusiveRange( options.MinimumValue, end.Value ), null)
				: (null, end.Error)
			;
		}
		var start = ParseNumber( left, tokenStart, options );
		if ( null != start.Error ) {
			return (null, start.Error);
		}
		if ( 0 == right.Length ) {
			if ( !options.AllowOpenEnded ) {
				return (
					null,
					new RangeParseError(
						RangeParseErrorCode.OpenEndedNotAllowed,
						tokenStart + firstDash,
						token,
						"Open-ended ranges are disabled."
					)
				);
			}
			return (new InclusiveRange( start.Value, null ), null);
		}
		var finish = ParseNumber(
			right,
			tokenStart + firstDash + 1,
			options
		);
		if ( null != finish.Error ) {
			return (null, finish.Error);
		}
		if ( finish.Value < start.Value ) {
			return (
				null,
				new RangeParseError(
					RangeParseErrorCode.DecreasingRange,
					tokenStart,
					token,
					"The upper endpoint precedes the lower endpoint."
				)
			);
		}
		return (new InclusiveRange( start.Value, finish.Value ), null);
	}

	private static (ulong Value, RangeParseError? Error) ParseNumber(
		string token,
		int tokenStart,
		RangeListParserOptions options
	) {
		if ( 0 == token.Length ) {
			return (
				0,
				new RangeParseError(
					RangeParseErrorCode.ExpectedNumber,
					tokenStart,
					token,
					"A numeric endpoint was expected."
				)
			);
		}
		ulong number = 0;
		for ( var index = 0; index < token.Length; index++ ) {
			var current = token[index];
			if ( current < '0' || '9' < current ) {
				return (
					0,
					new RangeParseError(
						RangeParseErrorCode.UnexpectedCharacter,
						tokenStart + index,
						current.ToString(),
						"Only ASCII decimal digits are accepted in endpoints."
					)
				);
			}
			var digit = (ulong)( current - '0' );
			if ( number > ( ulong.MaxValue - digit ) / 10 ) {
				return (
					0,
					new RangeParseError(
						RangeParseErrorCode.NumberOverflow,
						tokenStart,
						token,
						"The numeric endpoint overflowed an unsigned 64-bit value."
					)
				);
			}
			number = number * 10 + digit;
		}
		if ( number < options.MinimumValue ) {
			return (
				0,
				new RangeParseError(
					RangeParseErrorCode.ValueBelowMinimum,
					tokenStart,
					token,
					String.Concat(
						"The endpoint is below the configured minimum ",
						options.MinimumValue.ToString( CultureInfo.InvariantCulture ),
						"."
					)
				)
			);
		}
		if ( options.MaximumValue < number ) {
			return (
				0,
				new RangeParseError(
					RangeParseErrorCode.ValueAboveMaximum,
					tokenStart,
					token,
					String.Concat(
						"The endpoint exceeds the configured maximum ",
						options.MaximumValue.ToString( CultureInfo.InvariantCulture ),
						"."
					)
				)
			);
		}
		return (number, null);
	}

	private static RangeParseResult Failure(
		RangeParseErrorCode code,
		int index,
		string token,
		string message
	) => RangeParseResult.Failed(
		new RangeParseError(
			code,
			index,
			token,
			message
		)
	);

	private static bool IsSeparator( char value ) => ',' == value || ' ' == value || '\t' == value;

}
