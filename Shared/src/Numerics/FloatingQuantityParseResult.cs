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

namespace Icod.CoreUtils.Shared.Numerics;

/// <summary>
/// Contains the result of parsing a floating-point quantity.
/// </summary>
/// <param name="IsSuccess">The is success value.</param>
/// <param name="Value">The value value.</param>
/// <param name="ErrorKind">The error kind value.</param>
/// <param name="Suffix">The suffix value.</param>
public readonly record struct FloatingQuantityParseResult(
	bool IsSuccess,
	double Value,
	QuantityParseErrorKind ErrorKind,
	string Suffix
) {
	/// <summary>Creates a successful result.</summary>
	public static FloatingQuantityParseResult Success(
		double value,
		string suffix
	) {
		return new FloatingQuantityParseResult(
			true,
			value,
			QuantityParseErrorKind.None,
			suffix
		);
	}

	/// <summary>Creates a failed result.</summary>
	public static FloatingQuantityParseResult Failure(
		QuantityParseErrorKind errorKind,
		string suffix = ""
	) {
		return new FloatingQuantityParseResult(
			false,
			0.0,
			errorKind,
			suffix
		);
	}
}
