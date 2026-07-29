namespace Icod.CoreUtils.Shared.CommandLine;

using System.Collections.ObjectModel;

/// <summary>
/// Describes one command-line parsing error.
/// </summary>
public sealed class OptionParseError {

	private readonly ReadOnlyCollection<string> myCandidates;

	/// <summary>Gets the zero-based source argument index.</summary>
	public int ArgumentIndex {
		get;
	}

	/// <summary>Gets candidate long names for an ambiguous abbreviation.</summary>
	public IReadOnlyList<string> Candidates {
		get {
			return this.myCandidates;
		}
	}

	/// <summary>Gets the error category.</summary>
	public OptionParseErrorKind Kind {
		get;
	}

	/// <summary>Gets the option spelling involved in the error.</summary>
	public string OptionName {
		get;
	}

	/// <summary>Gets the original source token.</summary>
	public string Token {
		get;
	}

	/// <summary>
	/// Initializes a new instance of the OptionParseError class.
	/// </summary>
	internal OptionParseError(
		OptionParseErrorKind kind,
		int argumentIndex,
		string token,
		string optionName,
		IEnumerable<string>? candidates = null
	) {
		this.Kind = kind;
		this.ArgumentIndex = argumentIndex;
		this.Token = token;
		this.OptionName = optionName;
		this.myCandidates = new List<string>(
			candidates ?? Array.Empty<string>()
		).AsReadOnly();
	}

}
