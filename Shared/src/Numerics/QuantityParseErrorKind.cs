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
/// Identifies why a numeric operand could not be parsed.
/// </summary>
public enum QuantityParseErrorKind {
	/// <summary>No error occurred.</summary>
	None,
	/// <summary>The operand was empty.</summary>
	Empty,
	/// <summary>The numeric portion was invalid.</summary>
	InvalidNumber,
	/// <summary>The suffix was not recognized.</summary>
	InvalidSuffix,
	/// <summary>A leading plus sign was not allowed.</summary>
	PositiveSignNotAllowed,
	/// <summary>A leading minus sign was not allowed.</summary>
	NegativeSignNotAllowed,
	/// <summary>The value exceeded the requested destination range.</summary>
	Overflow
}
