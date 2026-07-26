namespace Icod.CoreUtils.Shared.CommandLine;

using System.Collections.ObjectModel;

/// <summary>
/// Describes one logical option and all spellings accepted for it.
/// </summary>
public sealed class OptionDefinition {

	private readonly ReadOnlyCollection<string> myLongNames;

	/// <summary>
	/// Gets whether this option may occur more than once without producing a parse error.
	/// </summary>
	public bool AllowMultiple {
		get;
	}

	/// <summary>
	/// Gets the stable logical key used to query parse results.
	/// </summary>
	public string Key {
		get;
	}

	/// <summary>
	/// Gets the accepted long names without leading hyphens.
	/// </summary>
	public IReadOnlyList<string> LongNames {
		get {
			return this.myLongNames;
		}
	}

	/// <summary>
	/// Gets whether an optional value may be supplied as the following token.
	/// </summary>
	public bool OptionalValueMayBeSeparate {
		get;
	}

	/// <summary>
	/// Gets the accepted short name, or <see langword="null"/> when none exists.
	/// </summary>
	public char? ShortName {
		get;
	}

	/// <summary>
	/// Gets whether and how the option accepts a value.
	/// </summary>
	public OptionValueArity ValueArity {
		get;
	}

	/// <summary>
	/// Initializes a new option definition.
	/// </summary>
	/// <param name="key">Stable logical key for the option.</param>
	/// <param name="shortName">Optional one-character short name.</param>
	/// <param name="longNames">Optional long names without leading hyphens.</param>
	/// <param name="valueArity">Whether the option accepts a value.</param>
	/// <param name="allowMultiple">Whether repeated occurrences are valid.</param>
	/// <param name="optionalValueMayBeSeparate">Whether an optional value may be supplied in the next token.</param>
	public OptionDefinition(
		string key,
		char? shortName = null,
		IEnumerable<string>? longNames = null,
		OptionValueArity valueArity = OptionValueArity.None,
		bool allowMultiple = true,
		bool optionalValueMayBeSeparate = false
	) {
		if ( string.IsNullOrWhiteSpace( key ) ) {
			throw new ArgumentException(
				"An option key is required.",
				nameof( key )
			);
		}
		if (
			shortName.HasValue
			&& (
				'-' == shortName.Value
				|| '\0' == shortName.Value
				|| char.IsWhiteSpace( shortName.Value )
			)
		) {
			throw new ArgumentOutOfRangeException(
				nameof( shortName ),
				"A short option must be a visible character other than '-'."
			);
		}

		var names = new List<string>();
		if ( null != longNames ) {
			foreach ( var name in longNames ) {
				if (
					string.IsNullOrWhiteSpace( name )
					|| name.StartsWith(
						"-",
						StringComparison.Ordinal
					)
					|| name.Any(
						character => char.IsWhiteSpace( character ) || '=' == character
					)
				) {
					throw new ArgumentException(
						$"Invalid long option name '{name}'.",
						nameof( longNames )
					);
				}
				if (
					names.Contains(
						name,
						StringComparer.Ordinal
					)
				) {
					throw new ArgumentException(
						$"Duplicate long option name '{name}'.",
						nameof( longNames )
					);
				}
				names.Add(
					name
				);
			}
		}

		if (
			!shortName.HasValue
			&& 0 == names.Count
		) {
			throw new ArgumentException(
				"An option must have a short name, at least one long name, or both.",
				nameof( longNames )
			);
		}
		if (
			OptionValueArity.Optional != valueArity
			&& optionalValueMayBeSeparate
		) {
			throw new ArgumentException(
				"Only options with optional values may enable separate optional values.",
				nameof( optionalValueMayBeSeparate )
			);
		}

		this.Key = key;
		this.ShortName = shortName;
		this.myLongNames = names.AsReadOnly();
		this.ValueArity = valueArity;
		this.AllowMultiple = allowMultiple;
		this.OptionalValueMayBeSeparate = optionalValueMayBeSeparate;
	}

}
