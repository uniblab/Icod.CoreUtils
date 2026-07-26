namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Describes whether an option accepts a value.
/// </summary>
public enum OptionValueArity {
	/// <summary>The option does not accept a value.</summary>
	None,
	/// <summary>The option requires a value.</summary>
	Required,
	/// <summary>The option accepts an optional value.</summary>
	Optional
}
