namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Controls one search of a compiled regular expression.</summary>
public sealed record RegularExpressionMatchOptions {
	/// <summary>Gets or initializes the zero-based UTF-16 index at which searching begins.</summary>
	public int StartIndex { get; init; }

	/// <summary>Gets or initializes whether matching is attempted only at <see cref="StartIndex"/>.</summary>
	public bool RequireMatchAtStart { get; init; }
}
