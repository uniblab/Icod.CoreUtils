namespace Icod.DiffUtils.SDiff;

using Icod.CommandFramework.RegularExpressions;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Contains validated <c>sdiff</c> options and comparison policies.</summary>
internal sealed class SDiffOptions {
	/// <summary>Gets or sets the optional transactional merge-output path.</summary>
	public string? OutputPath { get; set; }
	/// <summary>Gets or sets whether all inputs are treated as text.</summary>
	public bool TreatAsText { get; set; }
	/// <summary>Gets or sets whether changed groups consisting only of blank lines are ignored.</summary>
	public bool IgnoreBlankLines { get; set; }
	/// <summary>Gets or sets the maximum output width in display columns.</summary>
	public int Width { get; set; } = 130;
	/// <summary>Gets or sets whether common lines display only their left side.</summary>
	public bool LeftColumn { get; set; }
	/// <summary>Gets or sets whether common lines are omitted from the side-by-side display.</summary>
	public bool SuppressCommonLines { get; set; }
	/// <summary>Gets or sets whether tabs are expanded in displayed output.</summary>
	public bool ExpandTabs { get; set; }
	/// <summary>Gets or sets the tab-stop interval.</summary>
	public int TabSize { get; set; } = 8;
	/// <summary>Gets the two file operands.</summary>
	public string[] Operands { get; set; } = Array.Empty<string>();
	/// <summary>Gets the compiled expressions used by <c>-I</c>.</summary>
	public List<ICompiledRegularExpression> IgnoredLinePatterns { get; } = new();
	/// <summary>Gets or sets the line-comparison policy.</summary>
	public LineComparisonPolicy ComparisonPolicy { get; set; } = new();
}
