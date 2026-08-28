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

/// <summary>Describes one backslash and its following managed source position.</summary>
internal readonly struct EscapeSequence {

	/// <summary>Initializes an escape-sequence scan result.</summary>
	/// <param name="backslashOffset">The source offset of the backslash.</param>
	/// <param name="designatorOffset">The source offset after the backslash, or the input length for a trailing backslash.</param>
	/// <param name="isTrailing">Whether no source character follows the backslash.</param>
	internal EscapeSequence(
		int backslashOffset,
		int designatorOffset,
		bool isTrailing
	) {
		this.BackslashOffset = backslashOffset;
		this.DesignatorOffset = designatorOffset;
		this.IsTrailing = isTrailing;
	}

	/// <summary>Gets the source offset of the backslash.</summary>
	internal int BackslashOffset { get; }

	/// <summary>Gets the source offset of the escape designator, or the source length for a trailing backslash.</summary>
	internal int DesignatorOffset { get; }

	/// <summary>Gets whether no source character follows the backslash.</summary>
	internal bool IsTrailing { get; }

}
