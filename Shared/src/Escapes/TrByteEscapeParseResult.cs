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

/// <summary>Contains the low-level escaped bytes and diagnostics used by GNU <c>tr</c> set parsing.</summary>
public sealed class TrByteEscapeParseResult {

	private readonly IReadOnlyList<EscapedByte> myBytes;
	private readonly IReadOnlyList<EscapeDiagnostic> myDiagnostics;

	/// <summary>Initializes a tr byte-escape parsing result for the shared parser.</summary>
	/// <param name="bytes">The parsed bytes in source order.</param>
	/// <param name="diagnostics">The diagnostics in source order.</param>
	internal TrByteEscapeParseResult(
		IEnumerable<EscapedByte> bytes,
		IEnumerable<EscapeDiagnostic> diagnostics
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		ArgumentNullException.ThrowIfNull( diagnostics );
		this.myBytes = Array.AsReadOnly( bytes.ToArray() );
		this.myDiagnostics = Array.AsReadOnly( diagnostics.ToArray() );
	}

	/// <summary>Gets whether no error-severity diagnostic occurred.</summary>
	public bool IsSuccess => !this.myDiagnostics.Any( value => EscapeDiagnosticSeverity.Error == value.Severity );

	/// <summary>Gets parsed bytes in source order.</summary>
	public IReadOnlyList<EscapedByte> Bytes => this.myBytes;

	/// <summary>Gets stable warnings and errors in source order.</summary>
	public IReadOnlyList<EscapeDiagnostic> Diagnostics => this.myDiagnostics;

}
