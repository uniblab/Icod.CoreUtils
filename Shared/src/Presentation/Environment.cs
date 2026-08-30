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

namespace Icod.CoreUtils.Shared.Presentation;

/// <summary>
/// Supplies process environment values to CoreUtils presentation policy.
/// </summary>
public interface IEnvironmentVariableProvider {

	/// <summary>
	/// Gets one environment-variable value.
	/// </summary>
	/// <param name="name">The variable name.</param>
	/// <returns>The value, or <see langword="null"/> when the variable is absent.</returns>
	string? GetValue(
		string name
	);

}

/// <summary>
/// Reads environment variables from the current process.
/// </summary>
public sealed class SystemEnvironmentVariableProvider : IEnvironmentVariableProvider {

	/// <summary>
	/// Gets the reusable system environment provider.
	/// </summary>
	public static SystemEnvironmentVariableProvider Instance {
		get;
	} = new();

	private SystemEnvironmentVariableProvider() {
	}

	/// <inheritdoc/>
	public string? GetValue(
		string name
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			name
		);
		return Environment.GetEnvironmentVariable(
			name
		);
	}

}

/// <summary>
/// Captures process-environment inputs used by directory-listing and
/// <c>dircolors</c> presentation policy.
/// </summary>
public sealed class OutputEnvironmentSnapshot {

	/// <summary>
	/// Initializes an output-environment snapshot.
	/// </summary>
	public OutputEnvironmentSnapshot(
		string? term,
		string? colorTerm,
		string? columns,
		string? lines,
		string? shell,
		string? quotingStyle
	) {
		this.Term = Normalize(
			term
		);
		this.ColorTerm = Normalize(
			colorTerm
		);
		this.Columns = Normalize(
			columns
		);
		this.Lines = Normalize(
			lines
		);
		this.Shell = Normalize(
			shell
		);
		this.QuotingStyle = Normalize(
			quotingStyle
		);
	}

	/// <summary>Gets the normalized <c>TERM</c> value.</summary>
	public string? Term {
		get;
	}

	/// <summary>Gets the normalized <c>COLORTERM</c> value.</summary>
	public string? ColorTerm {
		get;
	}

	/// <summary>Gets the normalized <c>COLUMNS</c> value.</summary>
	public string? Columns {
		get;
	}

	/// <summary>Gets the normalized <c>LINES</c> value.</summary>
	public string? Lines {
		get;
	}

	/// <summary>Gets the normalized <c>SHELL</c> value.</summary>
	public string? Shell {
		get;
	}

	/// <summary>Gets the normalized <c>QUOTING_STYLE</c> value.</summary>
	public string? QuotingStyle {
		get;
	}

	/// <summary>
	/// Captures recognized values from an injectable environment provider.
	/// </summary>
	public static OutputEnvironmentSnapshot Capture(
		IEnvironmentVariableProvider provider
	) {
		ArgumentNullException.ThrowIfNull(
			provider
		);
		return new OutputEnvironmentSnapshot(
			provider.GetValue( "TERM" ),
			provider.GetValue( "COLORTERM" ),
			provider.GetValue( "COLUMNS" ),
			provider.GetValue( "LINES" ),
			provider.GetValue( "SHELL" ),
			provider.GetValue( "QUOTING_STYLE" )
		);
	}

	/// <summary>
	/// Parses a positive decimal dimension from an environment value.
	/// </summary>
	public static bool TryParsePositiveDimension(
		string? value,
		out int dimension
	) {
		return int.TryParse(
			value,
			System.Globalization.NumberStyles.None,
			System.Globalization.CultureInfo.InvariantCulture,
			out dimension
		) && ( 0 < dimension );
	}

	private static string? Normalize(
		string? value
	) {
		if ( string.IsNullOrWhiteSpace( value ) ) {
			return null;
		}
		return value.Trim();
	}

}
