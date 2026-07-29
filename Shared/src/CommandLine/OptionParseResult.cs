namespace Icod.CoreUtils.Shared.CommandLine;

using System.Collections.ObjectModel;

/// <summary>
/// Contains parsed options, operands, ordered items, and errors.
/// </summary>
public sealed class OptionParseResult {

	private readonly ReadOnlyCollection<OptionParseError> myErrors;
	private readonly ReadOnlyCollection<OptionParseItem> myItems;
	private readonly ReadOnlyCollection<string> myOperands;
	private readonly ReadOnlyCollection<OptionOccurrence> myOptions;

	/// <summary>Gets parsing errors.</summary>
	public IReadOnlyList<OptionParseError> Errors {
		get {
			return this.myErrors;
		}
	}

	/// <summary>Gets whether parsing completed without errors.</summary>
	public bool IsSuccess {
		get {
			return 0 == this.myErrors.Count;
		}
	}

	/// <summary>Gets options and operands in encounter order.</summary>
	public IReadOnlyList<OptionParseItem> Items {
		get {
			return this.myItems;
		}
	}

	/// <summary>Gets operands in encounter order.</summary>
	public IReadOnlyList<string> Operands {
		get {
			return this.myOperands;
		}
	}

	/// <summary>Gets option occurrences in encounter order.</summary>
	public IReadOnlyList<OptionOccurrence> Options {
		get {
			return this.myOptions;
		}

	}

	/// <summary>
	/// Initializes a new instance of the OptionParseResult class.
	/// </summary>
	internal OptionParseResult(
		IEnumerable<OptionOccurrence> options,
		IEnumerable<string> operands,
		IEnumerable<OptionParseItem> items,
		IEnumerable<OptionParseError> errors
	) {
		this.myOptions = new List<OptionOccurrence>( options ).AsReadOnly();
		this.myOperands = new List<string>( operands ).AsReadOnly();
		this.myItems = new List<OptionParseItem>( items ).AsReadOnly();
		this.myErrors = new List<OptionParseError>( errors ).AsReadOnly();
	}

	/// <summary>
	/// Gets all occurrences having the supplied logical key.
	/// </summary>
	public IEnumerable<OptionOccurrence> GetOccurrences(
		string key
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			key
		);
		return this.myOptions.Where(
			occurrence => string.Equals(
				occurrence.Definition.Key,
				key,
				StringComparison.Ordinal
			)
		);
	}

	/// <summary>
	/// Gets whether at least one occurrence has the supplied logical key.
	/// </summary>
	public bool HasOption(
		string key
	) {
		return this.GetOccurrences(
			key
		).Any();
	}

	/// <summary>
	/// Gets the value from the last occurrence having the supplied key.
	/// </summary>
	public string? GetLastValue(
		string key
	) {
		return this.GetOccurrences(
			key
		).LastOrDefault()?.Value;
	}

}
