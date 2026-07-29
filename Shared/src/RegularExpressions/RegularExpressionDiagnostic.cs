namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Describes a controlled regular-expression failure.</summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The human-readable diagnostic text.</param>
/// <param name="PatternIndex">The zero-based UTF-16 pattern index, or <see langword="null"/> for match-time diagnostics.</param>
public sealed record RegularExpressionDiagnostic(
	RegularExpressionDiagnosticCode Code,
	string Message,
	int? PatternIndex = null
);
