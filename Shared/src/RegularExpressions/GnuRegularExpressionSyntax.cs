namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Identifies the GNU regular-expression operator profile used during parsing and matching.</summary>
public enum GnuRegularExpressionSyntax {
	/// <summary>Uses GNU/POSIX basic syntax, including escaped plus and question-mark repetition operators.</summary>
	Basic,
	/// <summary>Uses the GNU Emacs syntax profile, including unescaped plus and question-mark repetition operators.</summary>
	Emacs
}
