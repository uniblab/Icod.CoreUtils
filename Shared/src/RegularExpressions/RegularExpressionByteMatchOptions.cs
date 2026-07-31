namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Controls where a byte-preserving regular-expression search begins.</summary>
public sealed record RegularExpressionByteMatchOptions {
	/// <summary>
	/// Gets or initializes the zero-based source-byte offset at which searching begins.
	/// In UTF-8 mode the offset must be on a decoded-unit boundary.
	/// </summary>
	public int StartByteOffset { get; init; }

	/// <summary>Gets or initializes whether a successful match must begin exactly at <see cref="StartByteOffset"/>.</summary>
	public bool RequireMatchAtStart { get; init; }
}
