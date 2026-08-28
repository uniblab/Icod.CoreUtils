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

/// <summary>Describes membership and range-boundary state at one position.</summary>
public readonly struct RangeMatch {

	/// <summary>Initializes a range match.</summary>
	/// <param name="isSelected">Whether the position is selected.</param>
	/// <param name="isRangeStart">Whether the position begins a normalized range.</param>
	public RangeMatch(
		bool isSelected,
		bool isRangeStart
	) {
		if ( isRangeStart && !isSelected ) {
			throw new ArgumentException(
				"A range start must also be selected.",
				nameof( isRangeStart )
			);
		}
		this.IsSelected = isSelected;
		this.IsRangeStart = isRangeStart;
	}

	/// <summary>Gets whether the position is selected.</summary>
	public bool IsSelected { get; }

	/// <summary>Gets whether the position begins a normalized range.</summary>
	public bool IsRangeStart { get; }

}
