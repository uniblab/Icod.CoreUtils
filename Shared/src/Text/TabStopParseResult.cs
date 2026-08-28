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

namespace Icod.CoreUtils.Shared.Text;

using Icod.CommandFramework.Text;

/// <summary>Represents the controlled result of parsing GNU-style tab stops.</summary>
public sealed class TabStopParseResult {
	private TabStopParseResult(
		TabStopSet? tabStops,
		TabStopParseError? error
	) {
		this.TabStops = tabStops;
		this.Error = error;
	}

	/// <summary>Gets the parse error when parsing failed.</summary>
	public TabStopParseError? Error {
		get;
	}

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool IsSuccess => this.TabStops is not null;

	/// <summary>Gets the parsed tab-stop model when parsing succeeded.</summary>
	public TabStopSet? TabStops {
		get;
	}

	/// <summary>Creates a successful parse result.</summary>
	/// <param name="tabStops">The parsed tab-stop model.</param>
	/// <returns>The successful result.</returns>
	internal static TabStopParseResult Succeeded( TabStopSet tabStops ) {
		ArgumentNullException.ThrowIfNull( tabStops );
		return new( tabStops, null );
	}

	/// <summary>Creates a failed parse result.</summary>
	/// <param name="error">The deterministic parse error.</param>
	/// <returns>The failed result.</returns>
	internal static TabStopParseResult Failed( TabStopParseError error ) {
		ArgumentNullException.ThrowIfNull( error );
		return new( null, error );
	}
}
