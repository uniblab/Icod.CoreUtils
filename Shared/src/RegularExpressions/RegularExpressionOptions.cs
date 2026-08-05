using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Controls GNU regular-expression compilation and matching.</summary>
public sealed record RegularExpressionOptions {
	/// <summary>
	/// Gets the GNU Coreutils <c>expr</c> compilation profile.
	/// It accepts empty reverse-collating ranges and the repetition contexts enabled by <c>expr</c>.
	/// </summary>
	public static RegularExpressionOptions GnuExprCompatibility => new() {
		AllowEmptyRanges = true,
		AllowInvalidRepetitionOperators = true
	};

	/// <summary>
	/// Gets the permissive GNU extended-expression profile used by search consumers such as GNU <c>grep -E</c>.
	/// It retains GNU invalid-duplicate compatibility while selecting unescaped ERE operators.
	/// </summary>
	public static RegularExpressionOptions GnuExtendedCompatibility => new() {
		Syntax = GnuRegularExpressionSyntax.Extended,
		AllowInvalidRepetitionOperators = true
	};

	/// <summary>
	/// Gets the GNU Emacs compilation profile used by GNU <c>ptx</c>.
	/// It uses unescaped plus and question-mark repetition operators and the permissive Gnulib repetition contexts of <c>RE_SYNTAX_EMACS</c>.
	/// </summary>
	public static RegularExpressionOptions GnuEmacsCompatibility => new() {
		Syntax = GnuRegularExpressionSyntax.Emacs,
		AllowEmptyRanges = true,
		AllowInvalidRepetitionOperators = true
	};
	/// <summary>Gets or initializes the GNU operator syntax profile.</summary>
	public GnuRegularExpressionSyntax Syntax { get; init; }

	/// <summary>Gets or initializes whether literal and back-reference comparisons ignore case.</summary>
	public bool IgnoreCase { get; init; }

	/// <summary>
	/// Gets or initializes whether the configured <see cref="LineSeparator"/> delimits logical lines for anchors, dot, and negated bracket expressions.
	/// The selected syntax profile may impose additional dot exclusions; GNU Emacs syntax is always line-sensitive for dot.
	/// </summary>
	public bool NewLineSensitive { get; init; }

	/// <summary>
	/// Gets or initializes the Unicode scalar that separates logical lines when <see cref="NewLineSensitive"/> is enabled.
	/// </summary>
	public Rune LineSeparator { get; init; } = new( '\n' );

	/// <summary>
	/// Gets or initializes whether the dot operator may match NUL when NUL is not the active line separator.
	/// GNU Basic and Extended syntax exclude NUL by default; byte-record consumers such as GNU Sed <c>--null-data</c> may opt in explicitly.
	/// </summary>
	public bool DotMatchesNull { get; init; }

	/// <summary>
	/// Gets or initializes whether otherwise-invalid adjacent repetition operators and interval operators without a preceding expression are accepted.
	/// GNU <c>expr</c> enables this compatibility behavior by clearing Gnulib's <c>RE_CONTEXT_INVALID_DUP</c> syntax bit.
	/// </summary>
	public bool AllowInvalidRepetitionOperators { get; init; }

	/// <summary>
	/// Gets or initializes whether reverse-collating bracket ranges are accepted as empty ranges.
	/// GNU <c>expr</c> enables this compatibility behavior; strict POSIX basic syntax does not.
	/// </summary>
	public bool AllowEmptyRanges { get; init; }

	/// <summary>
	/// Gets or initializes the maximum permitted parenthesized-subexpression and adjacent-repetition nesting depth.
	/// </summary>
	public int MaximumNestingDepth { get; init; } = 256;

	/// <summary>
	/// Gets or initializes the maximum number of internal match states that one search may create.
	/// A value of <see cref="int.MaxValue"/> disables the practical limit.
	/// </summary>
	public int MaximumMatchStates { get; init; } = int.MaxValue;
}
