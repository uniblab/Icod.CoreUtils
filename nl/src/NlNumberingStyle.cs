namespace Icod.CoreUtils.NL;

using Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents a validated section-numbering style.</summary>
internal sealed class NlNumberingStyle {
	/// <summary>Gets the style that numbers every line.</summary>
	internal static NlNumberingStyle All { get; } = new( NlNumberingStyleKind.All, null, null );

	/// <summary>Gets the style that numbers nonempty lines.</summary>
	internal static NlNumberingStyle Nonempty { get; } = new( NlNumberingStyleKind.Nonempty, null, null );

	/// <summary>Gets the style that numbers no lines.</summary>
	internal static NlNumberingStyle None { get; } = new( NlNumberingStyleKind.None, null, null );

	private NlNumberingStyle(
		NlNumberingStyleKind kind,
		string? pattern,
		ICompiledRegularExpression? expression
	) {
		this.Kind = kind;
		this.Pattern = pattern;
		this.Expression = expression;
	}

	/// <summary>Gets the compiled expression for a pattern style.</summary>
	internal ICompiledRegularExpression? Expression { get; }

	/// <summary>Gets the style kind.</summary>
	internal NlNumberingStyleKind Kind { get; }

	/// <summary>Gets the original pattern for a pattern style.</summary>
	internal string? Pattern { get; }

	/// <summary>Creates a pattern-numbering style.</summary>
	/// <param name="pattern">The original GNU basic regular expression.</param>
	/// <param name="expression">The compiled expression.</param>
	/// <returns>The pattern style.</returns>
	internal static NlNumberingStyle CreatePattern(
		string pattern,
		ICompiledRegularExpression expression
	) {
		ArgumentNullException.ThrowIfNull( pattern );
		ArgumentNullException.ThrowIfNull( expression );
		return new NlNumberingStyle( NlNumberingStyleKind.Pattern, pattern, expression );
	}
}
