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

namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Provides command-neutral scanning of one backslash and its following source position.</summary>
internal static class EscapeSequenceScanner {

	/// <summary>Scans an escape beginning at the current backslash.</summary>
	/// <param name="value">The managed source string.</param>
	/// <param name="index">The backslash offset on entry and designator offset on return.</param>
	/// <param name="sequence">The scanned source offsets.</param>
	/// <returns><see langword="true"/> when the current character was a backslash.</returns>
	internal static bool TryRead(
		string value,
		ref int index,
		out EscapeSequence sequence
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( index < 0 || value.Length <= index ) {
			throw new ArgumentOutOfRangeException( nameof( index ) );
		}
		if ( '\\' != value[index] ) {
			sequence = default;
			return false;
		}
		var backslash = index;
		index++;
		sequence = new EscapeSequence(
			backslash,
			index,
			value.Length <= index
		);
		return true;
	}

}
