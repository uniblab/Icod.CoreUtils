namespace Icod.CoreUtils.Shared.Terminal;

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
/// Describes the color depth inferred from terminal and environment inputs.
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
	/// <param name="useColor">Whether the caller should emit color.</param>
	/// <param name="capability">The inferred color depth.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="capability"/> is not defined.
	/// </exception>
	public TerminalColorDecision(
		bool useColor,
		TerminalColorCapability capability
	) {
		if ( !Enum.IsDefined( capability ) ) {
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

	/// <summary>Gets the inferred terminal color depth.</summary>
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
	/// <param name="mode">The requested color mode.</param>
	/// <param name="presentation">The terminal presentation snapshot.</param>
	/// <returns>The resolved color decision.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="presentation"/> is <see langword="null"/>.
	/// </exception>
	public static TerminalColorDecision Resolve(
		TerminalColorMode mode,
		TerminalPresentationSnapshot presentation
	) {
		ArgumentNullException.ThrowIfNull( presentation );
		var capability = InferCapability( presentation );

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
	/// <param name="presentation">The terminal presentation snapshot.</param>
	/// <returns>The inferred color capability.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="presentation"/> is <see langword="null"/>.
	/// </exception>
	public static TerminalColorCapability InferCapability(
		TerminalPresentationSnapshot presentation
	) {
		ArgumentNullException.ThrowIfNull( presentation );
		var environment = presentation.Environment;
		if ( string.Equals(
			environment.Term,
			"dumb",
			StringComparison.OrdinalIgnoreCase
		) ) {
			return TerminalColorCapability.None;
		}

		if ( ContainsAny(
			environment.ColorTerm,
			"truecolor",
			"24bit"
		) || ContainsAny(
			environment.Term,
			"truecolor",
			"24bit",
			"-direct"
		) ) {
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
			: TerminalColorCapability.None;
	}

	private static bool ContainsAny(
		string? value,
		params string[] candidates
	) {
		if ( null is value ) {
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
