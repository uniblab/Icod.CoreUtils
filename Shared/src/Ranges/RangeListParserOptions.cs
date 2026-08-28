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

/// <summary>Configures the command-neutral positional range-list grammar.</summary>
public sealed class RangeListParserOptions {

	/// <summary>Gets or sets the smallest accepted endpoint.</summary>
	public ulong MinimumValue { get; set; } = 1;

	/// <summary>Gets or sets the largest accepted explicit endpoint.</summary>
	/// <remarks>The default reserves <see cref="ulong.MaxValue"/> as an internal unbounded sentinel, matching the GNU positional-range model.</remarks>
	public ulong MaximumValue { get; set; } = ulong.MaxValue - 1;

	/// <summary>Gets or sets whether one bare hyphen means the entire domain.</summary>
	public bool AllowSingleDash { get; set; }

	/// <summary>Gets or sets whether forms such as <c>N-</c> are accepted.</summary>
	public bool AllowOpenEnded { get; set; } = true;

	/// <summary>Gets or sets whether forms such as <c>-M</c> are accepted.</summary>
	public bool AllowLeadingOpenRange { get; set; } = true;

	/// <summary>Gets or sets whether the parsed selection is complemented.</summary>
	public bool Complement { get; set; }

}
