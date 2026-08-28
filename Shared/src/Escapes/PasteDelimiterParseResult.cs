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

using Icod.CommandFramework.Delimiters;

/// <summary>Contains either a GNU <c>paste</c> separator cycle or structured diagnostics.</summary>
public sealed class PasteDelimiterParseResult {

	private readonly IReadOnlyList<EscapeDiagnostic> myDiagnostics;

	/// <summary>Initializes a paste delimiter parsing result for the shared parser.</summary>
	/// <param name="value">The parsed cycle, or null after an error.</param>
	/// <param name="diagnostics">The diagnostics in source order.</param>
	internal PasteDelimiterParseResult(
		SeparatorCycle? value,
		IEnumerable<EscapeDiagnostic> diagnostics
	) {
		ArgumentNullException.ThrowIfNull( diagnostics );
		this.Value = value;
		this.myDiagnostics = Array.AsReadOnly( diagnostics.ToArray() );
	}

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool IsSuccess => null != this.Value;

	/// <summary>Gets the parsed separator cycle when successful.</summary>
	public SeparatorCycle? Value { get; }

	/// <summary>Gets stable warnings and errors in source order.</summary>
	public IReadOnlyList<EscapeDiagnostic> Diagnostics => this.myDiagnostics;

}
