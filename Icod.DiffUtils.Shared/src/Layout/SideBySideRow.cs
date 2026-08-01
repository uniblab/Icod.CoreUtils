namespace Icod.DiffUtils.Shared.Layout;

/// <summary>Represents one logical side-by-side output row.</summary>
/// <param name="Left">The optional left-side text.</param>
/// <param name="Marker">The gutter marker.</param>
/// <param name="Right">The optional right-side text.</param>
/// <param name="IsCommon">Whether the row represents a common line.</param>
public sealed record SideBySideRow(
	string? Left,
	char Marker,
	string? Right,
	bool IsCommon
);
