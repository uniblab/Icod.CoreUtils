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

namespace Icod.CoreUtils.Shared.BinaryFormatting;

/// <summary>
/// Provides common block-width and field-layout calculations for binary display commands.
/// </summary>
public static class BinaryLineLayout {
	/// <summary>
	/// Computes the least common multiple of all supplied positive values.
	/// </summary>
	public static int LeastCommonMultiple(
		IEnumerable<int> values
	) {
		ArgumentNullException.ThrowIfNull( values );
		var result = 1;
		var any = false;
		foreach ( var value in values ) {
			if ( 0 >= value ) {
				throw new ArgumentOutOfRangeException(
					nameof( values ),
					"All values must be positive."
				);
			}
			any = true;
			result = checked( result / GreatestCommonDivisor( result, value ) * value );
		}
		return any ? result : 1;
	}

	/// <summary>
	/// Resolves and validates an input line width against the displayed value sizes.
	/// </summary>
	public static bool TryResolveWidth(
		IEnumerable<BinaryFormatSpecification> specifications,
		int? requestedWidth,
		bool widthOptionPresent,
		out int width,
		out string? error
	) {
		ArgumentNullException.ThrowIfNull( specifications );
		error = null;
		var formats = specifications.ToArray();
		var multiple = LeastCommonMultiple( formats.Select( format => format.Size ) );
		width = requestedWidth ?? ( widthOptionPresent ? 32 : 16 );
		if ( 0 >= width ) {
			error = "the output width must be greater than zero";
			return false;
		}
		if ( 0 != width % multiple ) {
			var requested = width;
			width = multiple;
			error = string.Concat(
				"warning: invalid width ",
				requested.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				"; using ",
				multiple.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				" instead"
			);
		}
		return true;
	}

	/// <summary>
	/// Distributes extra leading padding as evenly as possible among fields.
	/// </summary>
	public static IReadOnlyList<int> DistributeLeadingPadding(
		int fieldCount,
		int extraColumns
	) {
		if ( 0 > fieldCount ) {
			throw new ArgumentOutOfRangeException( nameof( fieldCount ) );
		}
		if ( 0 > extraColumns ) {
			throw new ArgumentOutOfRangeException( nameof( extraColumns ) );
		}
		if ( 0 == fieldCount ) {
			return Array.Empty<int>();
		}
		var output = new int[ fieldCount ];
		for ( var index = 0; index < fieldCount; index++ ) {
			var before = index * extraColumns / fieldCount;
			var after = ( index + 1 ) * extraColumns / fieldCount;
			output[ index ] = after - before;
		}
		return Array.AsReadOnly( output );
	}

	private static int GreatestCommonDivisor(
		int left,
		int right
	) {
		while ( 0 != right ) {
			var remainder = left % right;
			left = right;
			right = remainder;
		}
		return Math.Abs( left );
	}
}
