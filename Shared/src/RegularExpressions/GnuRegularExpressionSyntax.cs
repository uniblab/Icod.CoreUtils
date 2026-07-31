namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Identifies the GNU regular-expression operator profile used during parsing and matching.</summary>
public enum GnuRegularExpressionSyntax {
	/// <summary>Uses GNU/POSIX basic syntax, including escaped grouping, alternation, plus, question-mark, and interval operators.</summary>
	Basic = 0,
	/// <summary>Uses the GNU Emacs syntax profile, including unescaped plus and question-mark repetition operators.</summary>
	Emacs = 1,
	/// <summary>Uses GNU/POSIX extended syntax, including unescaped grouping, alternation, plus, question-mark, and interval operators.</summary>
	Extended = 2
}
