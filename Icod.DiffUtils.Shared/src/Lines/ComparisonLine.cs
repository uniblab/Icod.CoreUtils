namespace Icod.DiffUtils.Shared.Lines;

/// <summary>Represents one logical input line while retaining whether it ended with a line-feed byte.</summary>
/// <param name="Content">The decoded line content without the terminating line feed.</param>
/// <param name="HasLineTerminator">Whether the source line ended with a line-feed byte.</param>
public sealed record ComparisonLine(
	string Content,
	bool HasLineTerminator
);
