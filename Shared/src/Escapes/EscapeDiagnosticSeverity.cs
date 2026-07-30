namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Identifies whether an escape diagnostic permits a usable result.</summary>
public enum EscapeDiagnosticSeverity {
	/// <summary>The parser produced a usable result but detected ambiguous or discouraged syntax.</summary>
	Warning,
	/// <summary>The parser could not produce a valid result.</summary>
	Error
}
