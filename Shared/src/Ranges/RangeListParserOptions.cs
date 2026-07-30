namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Configures the command-neutral positional range-list grammar.</summary>
public sealed class RangeListParserOptions {

	/// <summary>Gets or sets the smallest accepted endpoint.</summary>
	public ulong MinimumValue { get; set; } = 1;

	/// <summary>Gets or sets the largest accepted explicit endpoint.</summary>
	/// <remarks>The default reserves <see cref="ulong.MaxValue"/> as an internal unbounded sentinel, matching the GNU positional-range model.</remarks>
	public ulong MaximumValue { get; set; } = ulong.MaxValue - 1;

	/// <summary>Gets or sets whether one bare hyphen means the entire domain.</summary>
	public bool AllowSingleDash { get; set; }

	/// <summary>Gets or sets whether forms such as <c>N-</c> are accepted.</summary>
	public bool AllowOpenEnded { get; set; } = true;

	/// <summary>Gets or sets whether forms such as <c>-M</c> are accepted.</summary>
	public bool AllowLeadingOpenRange { get; set; } = true;

	/// <summary>Gets or sets whether the parsed selection is complemented.</summary>
	public bool Complement { get; set; }

}
