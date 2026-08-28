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

/// <summary>Identifies a deterministic GNU tab-stop grammar failure.</summary>
public enum TabStopParseErrorCode {
	/// <summary>The specification contains a character outside digits, comma, blank, <c>/</c>, and <c>+</c>.</summary>
	InvalidCharacter,
	/// <summary>A decimal integer exceeds <see cref="ulong.MaxValue"/>.</summary>
	NumberOverflow,
	/// <summary>An unprefixed explicit tab stop is zero.</summary>
	Zero,
	/// <summary>Explicit tab stops are not strictly increasing.</summary>
	NotIncreasing,
	/// <summary>A recurring interval was effectively followed by another value of the same kind.</summary>
	ContinuationNotLast,
	/// <summary>Both absolute <c>/N</c> and relative <c>+N</c> continuations were supplied.</summary>
	MutuallyExclusiveContinuations,
	/// <summary>A <c>/</c> or <c>+</c> specifier occurs after digits in the same value.</summary>
	SpecifierNotAtStart
}
