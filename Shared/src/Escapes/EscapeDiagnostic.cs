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

/// <summary>Describes one structured escape-parsing warning or error.</summary>
public sealed class EscapeDiagnostic {

	/// <summary>Initializes an escape diagnostic.</summary>
	/// <param name="code">The stable diagnostic category.</param>
	/// <param name="severity">Whether the condition is a warning or an error.</param>
	/// <param name="sourceOffset">The zero-based UTF-16 source offset.</param>
	/// <param name="sourceLength">The number of source code units covered by the diagnostic.</param>
	/// <param name="message">A command-neutral explanatory message.</param>
	public EscapeDiagnostic(
		EscapeDiagnosticCode code,
		EscapeDiagnosticSeverity severity,
		int sourceOffset,
		int sourceLength,
		string message
	) {
		if ( !Enum.IsDefined( code ) ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		if ( !Enum.IsDefined( severity ) ) {
			throw new ArgumentOutOfRangeException( nameof( severity ) );
		}
		if ( sourceOffset < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( sourceOffset ) );
		}
		if ( sourceLength < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( sourceLength ) );
		}
		ArgumentNullException.ThrowIfNull( message );
		this.Code = code;
		this.Severity = severity;
		this.SourceOffset = sourceOffset;
		this.SourceLength = sourceLength;
		this.Message = message;
	}

	/// <summary>Gets the stable diagnostic category.</summary>
	public EscapeDiagnosticCode Code { get; }

	/// <summary>Gets whether the condition is a warning or an error.</summary>
	public EscapeDiagnosticSeverity Severity { get; }

	/// <summary>Gets the zero-based UTF-16 source offset.</summary>
	public int SourceOffset { get; }

	/// <summary>Gets the number of source code units covered by the diagnostic.</summary>
	public int SourceLength { get; }

	/// <summary>Gets the command-neutral explanatory message.</summary>
	public string Message { get; }

}
