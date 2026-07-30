namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Identifies a stable escape-parsing diagnostic category.</summary>
public enum EscapeDiagnosticCode {
	/// <summary>The input ended immediately after an unescaped backslash.</summary>
	TrailingBackslash,
	/// <summary>A three-digit octal escape exceeded one byte and was shortened deterministically.</summary>
	AmbiguousOctalEscape,
	/// <summary>The managed input contained an invalid UTF-16 scalar sequence.</summary>
	InvalidUnicodeScalar
}
