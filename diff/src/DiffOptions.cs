namespace Icod.DiffUtils.Diff;

using Icod.CommandFramework.RegularExpressions;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Contains validated command options and compiled matching policies.</summary>
internal sealed class DiffOptions {
	/// <summary>Gets or sets the selected output style.</summary>
	public DiffOutputStyle OutputStyle { get; set; }
	/// <summary>Gets or sets the number of context lines.</summary>
	public int ContextLines { get; set; } = 3;
	/// <summary>Gets or sets whether identical files are reported.</summary>
	public bool ReportIdenticalFiles { get; set; }
	/// <summary>Gets or sets whether directories are traversed recursively.</summary>
	public bool Recursive { get; set; }
	/// <summary>Gets or sets whether symbolic links are compared as links.</summary>
	public bool NoDereference { get; set; }
	/// <summary>Gets or sets whether absent files on either side are treated as empty.</summary>
	public bool NewFile { get; set; }
	/// <summary>Gets or sets whether only an absent first file is treated as empty.</summary>
	public bool UnidirectionalNewFile { get; set; }
	/// <summary>Gets or sets whether directory entry names ignore case.</summary>
	public bool IgnoreFileNameCase { get; set; }
	/// <summary>Gets or sets whether all files are forced through the text difference engine.</summary>
	public bool TreatAsText { get; set; }
	/// <summary>Gets or sets whether changed groups consisting only of blank lines are ignored.</summary>
	public bool IgnoreBlankLines { get; set; }
	/// <summary>Gets or sets whether tabs are expanded in output.</summary>
	public bool ExpandTabs { get; set; }
	/// <summary>Gets or sets whether an initial tab is emitted before normal/context line prefixes.</summary>
	public bool InitialTab { get; set; }
	/// <summary>Gets or sets whether whitespace before empty output lines is suppressed.</summary>
	public bool SuppressBlankEmpty { get; set; }
	/// <summary>Gets or sets the tab-stop width.</summary>
	public int TabSize { get; set; } = 8;
	/// <summary>Gets or sets the side-by-side total width.</summary>
	public int Width { get; set; } = 130;
	/// <summary>Gets or sets whether common side-by-side lines show only the left column.</summary>
	public bool LeftColumn { get; set; }
	/// <summary>Gets or sets whether common side-by-side lines are omitted.</summary>
	public bool SuppressCommonLines { get; set; }
	/// <summary>Gets or sets the preprocessor symbol used by ifdef output.</summary>
	public string? IfDefName { get; set; }
	/// <summary>Gets or sets the first fixed comparison source used by <c>--from-file</c>.</summary>
	public string? FromFile { get; set; }
	/// <summary>Gets or sets the second fixed comparison source used by <c>--to-file</c>.</summary>
	public string? ToFile { get; set; }
	/// <summary>Gets or sets the directory entry at which comparison begins.</summary>
	public string? StartingFile { get; set; }
	/// <summary>Gets the repeated alternate header labels.</summary>
	public List<string> Labels { get; } = new();
	/// <summary>Gets the directory exclusion patterns.</summary>
	public List<string> ExcludePatterns { get; } = new();
	/// <summary>Gets the compiled line patterns used by <c>-I</c>.</summary>
	public List<ICompiledRegularExpression> IgnoredLinePatterns { get; } = new();
	/// <summary>Gets or sets the function-context expression.</summary>
	public ICompiledRegularExpression? FunctionExpression { get; set; }
	/// <summary>Gets or sets whether the built-in C-function context matcher is enabled.</summary>
	public bool ShowCFunction { get; set; }
	/// <summary>Gets or sets the line normalization policy.</summary>
	public LineComparisonPolicy ComparisonPolicy { get; set; } = new();
}
