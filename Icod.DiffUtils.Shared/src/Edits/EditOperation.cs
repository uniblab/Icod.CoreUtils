namespace Icod.DiffUtils.Shared.Edits;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Represents one aligned line operation.</summary>
/// <param name="Kind">The operation kind.</param>
/// <param name="OldIndex">The zero-based first-input line index, or <see langword="null"/>.</param>
/// <param name="NewIndex">The zero-based second-input line index, or <see langword="null"/>.</param>
/// <param name="Line">The source line associated with the operation.</param>
public sealed record EditOperation(
	EditOperationKind Kind,
	int? OldIndex,
	int? NewIndex,
	ComparisonLine Line
);
