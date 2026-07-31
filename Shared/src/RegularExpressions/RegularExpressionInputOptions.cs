namespace Icod.CoreUtils.Shared.RegularExpressions;

using Icod.CoreUtils.Shared.Text;

/// <summary>Controls how authoritative source bytes are exposed to regular-expression matching.</summary>
public sealed record RegularExpressionInputOptions {
	/// <summary>
	/// Gets or initializes whether the matcher receives byte-valued units or decoded UTF-8 Unicode scalars.
	/// </summary>
	public TextDecodingMode DecodingMode { get; init; } = TextDecodingMode.Utf8;

	/// <summary>Gets or initializes the policy applied to malformed UTF-8 source bytes.</summary>
	public InvalidEncodingPolicy InvalidEncodingPolicy { get; init; } = global::Icod.CoreUtils.Shared.Text.InvalidEncodingPolicy.PreserveBytes;
}
