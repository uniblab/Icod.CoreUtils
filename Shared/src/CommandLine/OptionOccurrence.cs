namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Represents one option occurrence in the command line.
/// </summary>
public sealed class OptionOccurrence {

	/// <summary>Gets the zero-based source argument index.</summary>
	public int ArgumentIndex {
		get;
	}

	/// <summary>Gets the matched option definition.</summary>
	public OptionDefinition Definition {
		get;
	}

	/// <summary>Gets the original, unrevised source token.</summary>
	public string OriginalToken {
		get;
	}

	/// <summary>Gets the spelling that matched, such as <c>-n</c> or <c>--lines</c>.</summary>
	public string Spelling {
		get;
	}

	/// <summary>Gets the option value, or <see langword="null"/> when none was supplied.</summary>
	public string? Value {
		get;
	}

	/// <summary>
	/// Initializes a new instance of the OptionOccurrence class.
	/// </summary>
	internal OptionOccurrence(
		OptionDefinition definition,
		string spelling,
		string? value,
		int argumentIndex,
		string originalToken
	) {
		this.Definition = definition;
		this.Spelling = spelling;
		this.Value = value;
		this.ArgumentIndex = argumentIndex;
		this.OriginalToken = originalToken;
	}

}
