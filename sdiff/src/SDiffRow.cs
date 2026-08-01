namespace Icod.DiffUtils.SDiff;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Represents one display row in an <c>sdiff</c> group.</summary>
/// <param name="Left">The optional left-side line.</param>
/// <param name="Marker">The side-by-side gutter marker.</param>
/// <param name="Right">The optional right-side line.</param>
/// <param name="IsCommon">Whether the row is common or ignored.</param>
internal sealed record SDiffRow(
	ComparisonLine? Left,
	char Marker,
	ComparisonLine? Right,
	bool IsCommon
);
