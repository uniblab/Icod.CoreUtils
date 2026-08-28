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

/// <summary>Identifies a stable escape-parsing diagnostic category.</summary>
public enum EscapeDiagnosticCode {
	/// <summary>The input ended immediately after an unescaped backslash.</summary>
	TrailingBackslash,
	/// <summary>A three-digit octal escape exceeded one byte and was shortened deterministically.</summary>
	AmbiguousOctalEscape,
	/// <summary>The managed input contained an invalid UTF-16 scalar sequence.</summary>
	InvalidUnicodeScalar
}
