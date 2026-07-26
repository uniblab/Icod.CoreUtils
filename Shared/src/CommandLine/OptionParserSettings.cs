namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Configures command-line parsing behavior.
/// </summary>
public sealed class OptionParserSettings {

	/// <summary>
	/// Gets or sets whether unique long-option abbreviations are accepted.
	/// </summary>
	public bool AllowLongOptionAbbreviations {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets how operands affect recognition of later options.
	/// </summary>
	public OptionOrdering Ordering {
		get;
		set;
	} = OptionOrdering.RequireOrder;

	/// <summary>
	/// Gets legacy-token rewrite rules applied before parsing.
	/// </summary>
	public IList<OptionTokenRewriteRule> TokenRewriteRules {
		get;
	} = new List<OptionTokenRewriteRule>();

}
