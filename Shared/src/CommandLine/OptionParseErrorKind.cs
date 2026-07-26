namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Identifies a command-line parsing failure.
/// </summary>
public enum OptionParseErrorKind {
	/// <summary>An unknown short option was encountered.</summary>
	UnknownShortOption,
	/// <summary>An unknown long option was encountered.</summary>
	UnknownLongOption,
	/// <summary>A long-option abbreviation matched more than one option.</summary>
	AmbiguousLongOption,
	/// <summary>An option requiring a value did not receive one.</summary>
	MissingOptionValue,
	/// <summary>An option that accepts no value received one.</summary>
	UnexpectedOptionValue,
	/// <summary>An option that may occur only once was repeated.</summary>
	DuplicateOption
}
