namespace Icod.DiffUtils.Shared.Lines;

/// <summary>Controls line normalization before edit-script construction.</summary>
public sealed record LineComparisonPolicy {
	/// <summary>Gets whether alphabetic case is ignored.</summary>
	public bool IgnoreCase { get; init; }
	/// <summary>Gets whether every white-space character is ignored.</summary>
	public bool IgnoreAllSpace { get; init; }
	/// <summary>Gets whether runs of white space compare as one space.</summary>
	public bool IgnoreSpaceChange { get; init; }
	/// <summary>Gets whether trailing white space is ignored.</summary>
	public bool IgnoreTrailingSpace { get; init; }
	/// <summary>Gets whether tab characters compare by their expanded display columns.</summary>
	public bool IgnoreTabExpansion { get; init; }
	/// <summary>Gets whether a carriage return immediately before a line feed is stripped.</summary>
	public bool StripTrailingCarriageReturn { get; init; }
	/// <summary>Gets the tab-stop width used by tab-expansion comparison.</summary>
	public int TabSize { get; init; } = 8;
}
