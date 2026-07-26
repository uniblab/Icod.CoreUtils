namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Identifies the kind of item preserved in an <see cref="OptionParseResult"/>.
/// </summary>
public enum OptionParseItemKind {
	/// <summary>The item is a parsed option occurrence.</summary>
	Option,
	/// <summary>The item is an operand.</summary>
	Operand
}
