namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents one participating or nonparticipating regular-expression subexpression.</summary>
/// <param name="Success">Whether the subexpression participated in the selected match.</param>
/// <param name="Index">The zero-based UTF-16 input index, or -1 when the subexpression did not participate.</param>
/// <param name="Length">The UTF-16 capture length.</param>
/// <param name="Value">The captured text, or <see langword="null"/> when the subexpression did not participate.</param>
public sealed record RegularExpressionCapture(
	bool Success,
	int Index,
	int Length,
	string? Value
);
