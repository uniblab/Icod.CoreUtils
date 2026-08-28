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

/// <summary>Represents one parsed byte and whether a backslash escape produced it.</summary>
public readonly struct EscapedByte {

	/// <summary>Initializes an escaped-byte result.</summary>
	/// <param name="value">The resulting byte.</param>
	/// <param name="wasEscaped">Whether an escape introduced the byte.</param>
	/// <param name="sourceOffset">The zero-based UTF-16 source offset of the byte or its backslash.</param>
	public EscapedByte(
		byte value,
		bool wasEscaped,
		int sourceOffset
	) {
		if ( sourceOffset < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( sourceOffset ) );
		}
		this.Value = value;
		this.WasEscaped = wasEscaped;
		this.SourceOffset = sourceOffset;
	}

	/// <summary>Gets the resulting byte.</summary>
	public byte Value { get; }

	/// <summary>Gets whether an escape introduced the byte.</summary>
	public bool WasEscaped { get; }

	/// <summary>Gets the zero-based UTF-16 source offset of the byte or its backslash.</summary>
	public int SourceOffset { get; }

}
