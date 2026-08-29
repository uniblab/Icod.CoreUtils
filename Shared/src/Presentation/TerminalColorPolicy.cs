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
/// Specifies when terminal color should be emitted.
/// </summary>
public enum TerminalColorMode {
	/// <summary>Never emit terminal color sequences.</summary>
	Never,
	/// <summary>Emit color only when output is attached to a capable terminal.</summary>
	Auto,
	/// <summary>Emit color regardless of redirection.</summary>
	Always
}

/// <summary>
/// Describes color depth inferred from terminal and environment inputs.
/// </summary>
public enum TerminalColorCapability {
	/// <summary>No color capability is available.</summary>
	None,
	/// <summary>ANSI sixteen-color presentation is available.</summary>
	Ansi16,
	/// <summary>ANSI 256-color presentation is available.</summary>
	Ansi256,
	/// <summary>Twenty-four-bit true-color presentation is available.</summary>
	TrueColor
}

/// <summary>
/// Represents a resolved terminal-color decision.
/// </summary>
public readonly record struct TerminalColorDecision {

	/// <summary>
	/// Initializes a color decision.
	/// </summary>
	public TerminalColorDecision(
		bool useColor,
		TerminalColorCapability capability
	) {
		if ( !Enum.IsDefined(
			capability
		) ) {
			throw new ArgumentOutOfRangeException(
				nameof( capability ),
				capability,
				"Unknown terminal color capability."
			);
		}
		this.UseColor = useColor;
		this.Capability = capability;
	}

	/// <summary>Gets whether color should be emitted.</summary>
	public bool UseColor {
		get;
	}

	/// <summary>Gets the inferred color depth.</summary>
	public TerminalColorCapability Capability {
		get;
	}

}

/// <summary>
/// Resolves GNU-style never, auto, and always color policy from terminal and
/// environment observations.
/// </summary>
public static class TerminalColorPolicy {

	/// <summary>
	/// Resolves a color decision.
	/// </summary>
	public static TerminalColorDecision Resolve(
		TerminalColorMode mode,
		OutputPresentationSnapshot presentation
	) {
		ArgumentNullException.ThrowIfNull(
			presentation
		);
		var capability = InferCapability(
			presentation
		);

		return mode switch {
			TerminalColorMode.Never => new TerminalColorDecision(
				false,
				capability
			),
			TerminalColorMode.Auto => new TerminalColorDecision(
				presentation.IsTerminal
					&& ( TerminalColorCapability.None != capability ),
				capability
			),
			TerminalColorMode.Always => new TerminalColorDecision(
				true,
				TerminalColorCapability.None == capability
					? TerminalColorCapability.Ansi16
					: capability
			),
			_ => throw new ArgumentOutOfRangeException(
				nameof( mode ),
				mode,
				"Unknown terminal color mode."
			)
		};
	}

	/// <summary>
	/// Infers terminal color depth from <c>TERM</c>, <c>COLORTERM</c>, and
	/// attachment state.
	/// </summary>
	public static TerminalColorCapability InferCapability(
		OutputPresentationSnapshot presentation
	) {
		ArgumentNullException.ThrowIfNull(
			presentation
		);
		var environment = presentation.Environment;
		if ( string.Equals(
			environment.Term,
			"dumb",
			StringComparison.OrdinalIgnoreCase
		) ) {
			return TerminalColorCapability.None;
		}

		if (
			ContainsAny(
				environment.ColorTerm,
				"truecolor",
				"24bit"
			)
			|| ContainsAny(
				environment.Term,
				"truecolor",
				"24bit",
				"-direct"
			)
		) {
			return TerminalColorCapability.TrueColor;
		}

		if ( ContainsAny(
			environment.Term,
			"256color"
		) ) {
			return TerminalColorCapability.Ansi256;
		}

		return presentation.IsTerminal
			? TerminalColorCapability.Ansi16
			: TerminalColorCapability.None
		;
	}

	private static bool ContainsAny(
		string? value,
		params string[] candidates
	) {
		if ( value is null ) {
			return false;
		}
		foreach ( var candidate in candidates ) {
			if ( value.Contains(
				candidate,
				StringComparison.OrdinalIgnoreCase
			) ) {
				return true;
			}
		}
		return false;
	}

}
