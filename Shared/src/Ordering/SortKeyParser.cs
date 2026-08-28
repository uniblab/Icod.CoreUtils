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

namespace Icod.CoreUtils.Shared.Ordering;

using System.Globalization;
using System.Text;

/// <summary>Parses GNU field-and-character sort-key syntax without imposing command-specific comparison semantics.</summary>
public static class SortKeyParser {
	/// <summary>Contains the comparison option letters recognized by GNU Coreutils sort keys.</summary>
	public const string DefaultAllowedOptions = "bdfghiMnRrV";

	/// <summary>Parses one GNU <c>F[.C][OPTS][,F[.C][OPTS]]</c> key specification.</summary>
	/// <param name="specification">The key specification without the leading <c>-k</c>.</param>
	/// <param name="allowedOptions">The command-selected set of accepted option letters.</param>
	/// <returns>The complete parse result.</returns>
	public static SortKeyParseResult Parse(
		string specification,
		string allowedOptions = DefaultAllowedOptions
	) {
		ArgumentNullException.ThrowIfNull( specification );
		ArgumentNullException.ThrowIfNull( allowedOptions );
		if ( 0 == specification.Length ) {
			return SortKeyParseResult.Failed(
				SortKeyParseErrorCode.EmptySpecification,
				0,
				"sort key is empty"
			);
		}
		var comma = specification.IndexOf( ',' );
		if ( ( 0 <= comma ) && ( 0 <= specification.IndexOf( ',', comma + 1 ) ) ) {
			return SortKeyParseResult.Failed(
				SortKeyParseErrorCode.MultipleEndpointSeparators,
				specification.IndexOf( ',', comma + 1 ),
				"sort key contains more than one endpoint separator"
			);
		}
		if ( comma == specification.Length - 1 ) {
			return SortKeyParseResult.Failed(
				SortKeyParseErrorCode.MissingEndPosition,
				comma,
				"sort key is missing its end position"
			);
		}
		var options = new StringBuilder();
		var startLength = 0 > comma ? specification.Length : comma;
		var startResult = ParsePosition(
			specification.AsSpan( 0, startLength ),
			0,
			isStart: true,
			allowedOptions,
			options
		);
		if ( null != startResult.Error ) {
			return startResult.Error;
		}
		SortKeyPosition? end = null;
		if ( 0 <= comma ) {
			var endResult = ParsePosition(
				specification.AsSpan( comma + 1 ),
				comma + 1,
				isStart: false,
				allowedOptions,
				options
			);
			if ( null != endResult.Error ) {
				return endResult.Error;
			}
			end = endResult.Position;
		}
		return SortKeyParseResult.Succeeded(
			new SortKeyDefinition( startResult.Position!, end, options.ToString() )
		);
	}

	/// <summary>Attempts to parse one GNU sort-key specification.</summary>
	/// <param name="specification">The key specification without the leading <c>-k</c>.</param>
	/// <param name="definition">Receives the parsed definition.</param>
	/// <param name="error">Receives the complete parse error.</param>
	/// <param name="allowedOptions">The command-selected set of accepted option letters.</param>
	/// <returns><see langword="true"/> when parsing succeeded.</returns>
	public static bool TryParse(
		string specification,
		out SortKeyDefinition? definition,
		out SortKeyParseResult? error,
		string allowedOptions = DefaultAllowedOptions
	) {
		var result = Parse( specification, allowedOptions );
		definition = result.Definition;
		error = result.IsSuccess ? null : result;
		return result.IsSuccess;
	}

	private static PositionParseResult ParsePosition(
		ReadOnlySpan<char> source,
		int sourceOffset,
		bool isStart,
		string allowedOptions,
		StringBuilder normalizedOptions
	) {
		var index = 0;
		while ( ( index < source.Length ) && char.IsAsciiDigit( source[ index ] ) ) {
			index++;
		}
		if ( 0 == index ) {
			return PositionParseResult.FromError(
				SortKeyParseErrorCode.MissingFieldNumber,
				sourceOffset,
				"sort key is missing a field number"
			);
		}
		if ( !int.TryParse(
			source[..index],
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out var fieldNumber
		) || ( 0 >= fieldNumber ) ) {
			return PositionParseResult.FromError(
				SortKeyParseErrorCode.InvalidNumber,
				sourceOffset,
				"sort-key field number is invalid"
			);
		}
		int? characterOffset = null;
		if ( ( index < source.Length ) && ( '.' == source[ index ] ) ) {
			index++;
			var characterStart = index;
			while ( ( index < source.Length ) && char.IsAsciiDigit( source[ index ] ) ) {
				index++;
			}
			if ( characterStart == index ) {
				return PositionParseResult.FromError(
					SortKeyParseErrorCode.InvalidNumber,
					sourceOffset + characterStart,
					"sort-key character offset is missing"
				);
			}
			if ( !int.TryParse(
				source[characterStart..index],
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var parsedCharacter
			) ) {
				return PositionParseResult.FromError(
					SortKeyParseErrorCode.InvalidNumber,
					sourceOffset + characterStart,
					"sort-key character offset is invalid"
				);
			}
			if ( isStart && ( 0 == parsedCharacter ) ) {
				return PositionParseResult.FromError(
					SortKeyParseErrorCode.InvalidStartCharacterOffset,
					sourceOffset + characterStart,
					"sort-key start character offset must be positive"
				);
			}
			characterOffset = parsedCharacter;
		}
		var skipLeadingBlanks = false;
		while ( index < source.Length ) {
			var option = source[ index ];
			if ( 0 > allowedOptions.IndexOf( option ) ) {
				return PositionParseResult.FromError(
					SortKeyParseErrorCode.UnknownOption,
					sourceOffset + index,
					string.Concat( "unsupported sort-key option: ", option )
				);
			}
			if ( 'b' == option ) {
				skipLeadingBlanks = true;
			} else if ( 0 > normalizedOptions.ToString().IndexOf( option ) ) {
				normalizedOptions.Append( option );
			}
			index++;
		}
		return PositionParseResult.FromPosition(
			new SortKeyPosition( fieldNumber, characterOffset, skipLeadingBlanks )
		);
	}

	private sealed class PositionParseResult {
		private PositionParseResult(
			SortKeyPosition? position,
			SortKeyParseResult? error
		) {
			this.Position = position;
			this.Error = error;
		}

		/// <summary>Gets the parsed position, when parsing succeeded.</summary>
		public SortKeyPosition? Position { get; }

		/// <summary>Gets the controlled parse error, when parsing failed.</summary>
		public SortKeyParseResult? Error { get; }

		/// <summary>Creates a successful private position result.</summary>
		/// <param name="position">The parsed position.</param>
		/// <returns>The successful result.</returns>
		public static PositionParseResult FromPosition( SortKeyPosition position ) {
			return new PositionParseResult( position, null );
		}

		/// <summary>Creates an unsuccessful private position result.</summary>
		/// <param name="code">The deterministic parse-error code.</param>
		/// <param name="offset">The zero-based source offset.</param>
		/// <param name="message">The controlled diagnostic.</param>
		/// <returns>The unsuccessful result.</returns>
		public static PositionParseResult FromError(
			SortKeyParseErrorCode code,
			int offset,
			string message
		) {
			return new PositionParseResult(
				null,
				SortKeyParseResult.Failed( code, offset, message )
			);
		}
	}
}
