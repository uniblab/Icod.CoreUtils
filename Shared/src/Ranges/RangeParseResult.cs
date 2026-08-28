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

/// <summary>Contains either a parsed range set or a structured parsing error.</summary>
public sealed class RangeParseResult {

	private RangeParseResult(
		RangeSet? value,
		RangeParseError? error
	) {
		this.Value = value;
		this.Error = error;
	}

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool IsSuccess => null != this.Value;

	/// <summary>Gets the parsed range set when parsing succeeded.</summary>
	public RangeSet? Value { get; }

	/// <summary>Gets the structured error when parsing failed.</summary>
	public RangeParseError? Error { get; }

	/// <summary>Creates a successful result.</summary>
	/// <param name="value">The parsed range set.</param>
	/// <returns>A successful parsing result.</returns>
	public static RangeParseResult Succeeded( RangeSet value ) {
		ArgumentNullException.ThrowIfNull( value );
		return new RangeParseResult( value, null );
	}

	/// <summary>Creates a failed result.</summary>
	/// <param name="error">The structured parsing error.</param>
	/// <returns>A failed parsing result.</returns>
	public static RangeParseResult Failed( RangeParseError error ) {
		ArgumentNullException.ThrowIfNull( error );
		return new RangeParseResult( null, error );
	}

}
