namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Identifies a deterministic regular-expression compile or match failure.</summary>
public enum RegularExpressionDiagnosticCode {
	/// <summary>The pattern ends with an incomplete escape.</summary>
	TrailingEscape,
	/// <summary>A bracket expression is not terminated.</summary>
	UnterminatedBracketExpression,
	/// <summary>A parenthesized subexpression is not terminated.</summary>
	UnterminatedSubexpression,
	/// <summary>A closing subexpression operator has no matching opener.</summary>
	UnmatchedClosingSubexpression,
	/// <summary>A repetition operator appears in a context rejected by the selected GNU syntax profile.</summary>
	InvalidRepetitionOperator,
	/// <summary>An interval expression is malformed or outside the supported POSIX range.</summary>
	InvalidInterval,
	/// <summary>A back-reference names a subexpression that has not already been closed.</summary>
	InvalidBackReference,
	/// <summary>A bracket range has invalid endpoints or reverse collation order.</summary>
	InvalidRange,
	/// <summary>A POSIX character-class name is not recognized by the configured provider.</summary>
	InvalidCharacterClass,
	/// <summary>A collating element or equivalence class cannot be represented by the configured provider.</summary>
	UnsupportedCollatingElement,
	/// <summary>The configured subexpression or adjacent-repetition nesting limit was exceeded.</summary>
	NestingDepthExceeded,
	/// <summary>The requested UTF-16 start index is invalid or splits a surrogate pair.</summary>
	InvalidStartIndex,
	/// <summary>The requested source-byte start offset is invalid or splits a decoded UTF-8 unit.</summary>
	InvalidStartByteOffset,
	/// <summary>The configured match-state limit was exceeded.</summary>
	MatchResourceLimitExceeded
}
