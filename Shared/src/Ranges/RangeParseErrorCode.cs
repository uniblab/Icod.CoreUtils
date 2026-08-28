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

/// <summary>Identifies a deterministic positional range-list parsing failure.</summary>
public enum RangeParseErrorCode {
	/// <summary>The range list was empty.</summary>
	EmptyList,
	/// <summary>A numeric endpoint was expected.</summary>
	ExpectedNumber,
	/// <summary>A parsed endpoint was below the configured minimum.</summary>
	ValueBelowMinimum,
	/// <summary>A parsed endpoint exceeded the configured maximum.</summary>
	ValueAboveMaximum,
	/// <summary>A numeric endpoint overflowed <see cref="ulong"/>.</summary>
	NumberOverflow,
	/// <summary>A range contained more than one hyphen.</summary>
	MultipleDashes,
	/// <summary>An endpoint was omitted where the grammar requires one.</summary>
	MissingEndpoint,
	/// <summary>The upper endpoint preceded the lower endpoint.</summary>
	DecreasingRange,
	/// <summary>An unexpected character occurred in the list.</summary>
	UnexpectedCharacter,
	/// <summary>An open-ended range was disabled by the parser profile.</summary>
	OpenEndedNotAllowed,
	/// <summary>A leading-open range was disabled by the parser profile.</summary>
	LeadingOpenRangeNotAllowed
}
