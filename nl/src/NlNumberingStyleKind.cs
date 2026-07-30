namespace Icod.CoreUtils.NL;

/// <summary>Identifies a GNU <c>nl</c> section-numbering style.</summary>
internal enum NlNumberingStyleKind {
	/// <summary>Numbers every line, subject to blank-line grouping.</summary>
	All,

	/// <summary>Numbers nonempty lines.</summary>
	Nonempty,

	/// <summary>Numbers no lines.</summary>
	None,

	/// <summary>Numbers lines matching a GNU basic regular expression.</summary>
	Pattern
}
