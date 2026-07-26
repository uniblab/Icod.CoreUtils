namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Parses GNU/POSIX-style short and long options from a command line.
/// </summary>
public sealed class OptionParser {

	private sealed record ParserToken(
		string Value,
		string OriginalValue,
		int ArgumentIndex,
		bool CanRewrite
	);

	private readonly Dictionary<string, OptionDefinition> myDefinitionsByKey;
	private readonly Dictionary<string, OptionDefinition> myLongDefinitions;
	private readonly OptionParserSettings mySettings;
	private readonly Dictionary<char, OptionDefinition> myShortDefinitions;

	/// <summary>
	/// Initializes a parser with the supplied option definitions.
	/// </summary>
	public OptionParser(
		IEnumerable<OptionDefinition> definitions,
		OptionParserSettings? settings = null
	) {
		ArgumentNullException.ThrowIfNull(
			definitions
		);

		settings ??= new OptionParserSettings();
		this.mySettings = new OptionParserSettings {
			AllowLongOptionAbbreviations = settings.AllowLongOptionAbbreviations,
			Ordering = settings.Ordering
		};
		foreach ( var rule in settings.TokenRewriteRules ) {
			ArgumentNullException.ThrowIfNull(
				rule
			);
			this.mySettings.TokenRewriteRules.Add(
				rule
			);
		}
		this.myDefinitionsByKey = new Dictionary<string, OptionDefinition>(
			StringComparer.Ordinal
		);
		this.myShortDefinitions = new Dictionary<char, OptionDefinition>();
		this.myLongDefinitions = new Dictionary<string, OptionDefinition>(
			StringComparer.Ordinal
		);

		foreach ( var definition in definitions ) {
			ArgumentNullException.ThrowIfNull(
				definition
			);
			if (
				!this.myDefinitionsByKey.TryAdd(
					definition.Key,
					definition
				)
			) {
				throw new ArgumentException(
					$"Duplicate option key '{definition.Key}'.",
					nameof( definitions )
				);
			}
			if (
				definition.ShortName.HasValue
				&& !this.myShortDefinitions.TryAdd(
					definition.ShortName.Value,
					definition
				)
			) {
				throw new ArgumentException(
					$"Duplicate short option '-{definition.ShortName.Value}'.",
					nameof( definitions )
				);
			}
			foreach ( var longName in definition.LongNames ) {
				if (
					!this.myLongDefinitions.TryAdd(
						longName,
						definition
					)
				) {
					throw new ArgumentException(
						$"Duplicate long option '--{longName}'.",
						nameof( definitions )
					);
				}
			}
		}
	}

	/// <summary>
	/// Parses the supplied arguments.
	/// </summary>
	public OptionParseResult Parse(
		IReadOnlyList<string>? arguments
	) {
		arguments ??= Array.Empty<string>();

		var tokens = CreateTokens(
			arguments
		);
		var options = new List<OptionOccurrence>();
		var operands = new List<string>();
		var items = new List<OptionParseItem>();
		var errors = new List<OptionParseError>();
		var occurrenceCounts = new Dictionary<string, int>(
			StringComparer.Ordinal
		);
		var parsingOptions = true;

		for (
			var index = 0;
			index < tokens.Count;
			index++
		) {
			var token = tokens[ index ];
			if ( parsingOptions && "--" == token.Value ) {
				parsingOptions = false;
				continue;
			}

			if (
				parsingOptions
				&& token.CanRewrite
			) {
				var rewritten = this.RewriteToken(
					token.Value
				);
				if ( null != rewritten ) {
					tokens.RemoveAt(
						index
					);
					for (
						var replacementIndex = rewritten.Count - 1;
						0 <= replacementIndex;
						replacementIndex--
					) {
						tokens.Insert(
							index,
							new ParserToken(
								rewritten[ replacementIndex ] ?? string.Empty,
								token.OriginalValue,
								token.ArgumentIndex,
								false
							)
						);
					}
					if ( 0 == rewritten.Count ) {
						index--;
						continue;
					}
					token = tokens[ index ];
				}
			}

			if ( parsingOptions && "--" == token.Value ) {
				parsingOptions = false;
				continue;
			}

			if (
				parsingOptions
				&& token.Value.StartsWith(
					"--",
					StringComparison.Ordinal
				)
				&& 2 < token.Value.Length
			) {
				this.ParseLongOption(
					tokens,
					ref index,
					token,
					options,
					items,
					errors,
					occurrenceCounts
				);
				continue;
			}

			if (
				parsingOptions
				&& token.Value.StartsWith(
					"-",
					StringComparison.Ordinal
				)
				&& "-" != token.Value
			) {
				this.ParseShortOptions(
					tokens,
					ref index,
					token,
					options,
					items,
					errors,
					occurrenceCounts
				);
				continue;
			}

			operands.Add(
				token.Value
			);
			items.Add(
				OptionParseItem.FromOperand(
					token.Value,
					token.ArgumentIndex
				)
			);
			if ( OptionOrdering.RequireOrder == this.mySettings.Ordering ) {
				parsingOptions = false;
			}
		}

		return new OptionParseResult(
			options,
			operands,
			items,
			errors
		);
	}

	private static List<ParserToken> CreateTokens(
		IReadOnlyList<string> arguments
	) {
		var output = new List<ParserToken>(
			arguments.Count
		);
		for (
			var index = 0;
			index < arguments.Count;
			index++
		) {
			var value = arguments[ index ] ?? string.Empty;
			output.Add(
				new ParserToken(
					value,
					value,
					index,
					true
				)
			);
		}
		return output;
	}

	private IReadOnlyList<string>? RewriteToken(
		string value
	) {
		foreach ( var rule in this.mySettings.TokenRewriteRules ) {
			var rewritten = rule.Rewrite(
				value
			);
			if ( null != rewritten ) {
				return rewritten;
			}
		}
		return null;
	}

	private void ParseLongOption(
		IReadOnlyList<ParserToken> tokens,
		ref int index,
		ParserToken token,
		ICollection<OptionOccurrence> options,
		ICollection<OptionParseItem> items,
		ICollection<OptionParseError> errors,
		IDictionary<string, int> occurrenceCounts
	) {
		var optionText = token.Value.Substring(
			2
		);
		var equalsIndex = optionText.IndexOf(
			'='
		);
		var name = 0 <= equalsIndex
			? optionText.Substring( 0, equalsIndex )
			: optionText
		;
		var hasAttachedValue = 0 <= equalsIndex;
		var attachedValue = hasAttachedValue
			? optionText.Substring( equalsIndex + 1 )
			: null
		;

		var definition = this.ResolveLongDefinition(
			name,
			out var candidates
		);
		if ( null == definition ) {
			errors.Add(
				new OptionParseError(
					0 < candidates.Count
						? OptionParseErrorKind.AmbiguousLongOption
						: OptionParseErrorKind.UnknownLongOption,
					token.ArgumentIndex,
					token.OriginalValue,
					$"--{name}",
					candidates
				)
			);
			return;
		}

		string? value = null;
		switch ( definition.ValueArity ) {
			case OptionValueArity.None:
				if ( hasAttachedValue ) {
					errors.Add(
						new OptionParseError(
							OptionParseErrorKind.UnexpectedOptionValue,
							token.ArgumentIndex,
							token.OriginalValue,
							$"--{name}"
						)
					);
					return;
				}
				break;

			case OptionValueArity.Required:
				if ( hasAttachedValue ) {
					value = attachedValue;
				} else if ( index + 1 < tokens.Count ) {
					index++;
					value = tokens[ index ].Value;
				} else {
					errors.Add(
						new OptionParseError(
							OptionParseErrorKind.MissingOptionValue,
							token.ArgumentIndex,
							token.OriginalValue,
							$"--{name}"
						)
					);
					return;
				}
				break;

			case OptionValueArity.Optional:
				if ( hasAttachedValue ) {
					value = attachedValue;
				} else if (
					definition.OptionalValueMayBeSeparate
					&& index + 1 < tokens.Count
					&& CanUseAsSeparateOptionalValue( tokens[ index + 1 ].Value )
				) {
					index++;
					value = tokens[ index ].Value;
				}
				break;
		}

		this.AddOccurrence(
			definition,
			$"--{name}",
			value,
			token,
			options,
			items,
			errors,
			occurrenceCounts
		);
	}

	private void ParseShortOptions(
		IReadOnlyList<ParserToken> tokens,
		ref int index,
		ParserToken token,
		ICollection<OptionOccurrence> options,
		ICollection<OptionParseItem> items,
		ICollection<OptionParseError> errors,
		IDictionary<string, int> occurrenceCounts
	) {
		for (
			var characterIndex = 1;
			characterIndex < token.Value.Length;
			characterIndex++
		) {
			var shortName = token.Value[ characterIndex ];
			if (
				!this.myShortDefinitions.TryGetValue(
					shortName,
					out var definition
				)
			) {
				errors.Add(
					new OptionParseError(
						OptionParseErrorKind.UnknownShortOption,
						token.ArgumentIndex,
						token.OriginalValue,
						shortName.ToString()
					)
				);
				continue;
			}

			string? value = null;
			var hasAttachedValue = characterIndex + 1 < token.Value.Length;
			switch ( definition.ValueArity ) {
				case OptionValueArity.None:
					break;

				case OptionValueArity.Required:
					if ( hasAttachedValue ) {
						value = token.Value.Substring(
							characterIndex + 1
						);
						characterIndex = token.Value.Length;
					} else if ( index + 1 < tokens.Count ) {
						index++;
						value = tokens[ index ].Value;
					} else {
						errors.Add(
							new OptionParseError(
								OptionParseErrorKind.MissingOptionValue,
								token.ArgumentIndex,
								token.OriginalValue,
								shortName.ToString()
							)
						);
						return;
					}
					break;

				case OptionValueArity.Optional:
					if ( hasAttachedValue ) {
						value = token.Value.Substring(
							characterIndex + 1
						);
						characterIndex = token.Value.Length;
					} else if (
						definition.OptionalValueMayBeSeparate
						&& index + 1 < tokens.Count
						&& CanUseAsSeparateOptionalValue( tokens[ index + 1 ].Value )
					) {
						index++;
						value = tokens[ index ].Value;
					}
					break;
			}

			this.AddOccurrence(
				definition,
				$"-{shortName}",
				value,
				token,
				options,
				items,
				errors,
				occurrenceCounts
			);
			if ( OptionValueArity.None != definition.ValueArity ) {
				return;
			}
		}
	}

	private OptionDefinition? ResolveLongDefinition(
		string name,
		out IReadOnlyList<string> candidates
	) {
		if ( 0 == name.Length ) {
			candidates = Array.Empty<string>();
			return null;
		}
		if (
			this.myLongDefinitions.TryGetValue(
				name,
				out var exact
			)
		) {
			candidates = Array.Empty<string>();
			return exact;
		}
		if ( !this.mySettings.AllowLongOptionAbbreviations ) {
			candidates = Array.Empty<string>();
			return null;
		}

		var matches = this.myLongDefinitions
			.Where(
				pair => pair.Key.StartsWith(
					name,
					StringComparison.Ordinal
				)
			)
			.ToArray();
		var definitions = matches
			.Select(
				pair => pair.Value
			)
			.Distinct()
			.ToArray();
		if ( 1 == definitions.Length ) {
			candidates = Array.Empty<string>();
			return definitions[ 0 ];
		}

		candidates = matches
			.Select(
				pair => pair.Key
			)
			.OrderBy(
				candidate => candidate,
				StringComparer.Ordinal
			)
			.ToArray();
		return null;
	}

	private void AddOccurrence(
		OptionDefinition definition,
		string spelling,
		string? value,
		ParserToken token,
		ICollection<OptionOccurrence> options,
		ICollection<OptionParseItem> items,
		ICollection<OptionParseError> errors,
		IDictionary<string, int> occurrenceCounts
	) {
		occurrenceCounts.TryGetValue(
			definition.Key,
			out var count
		);
		count++;
		occurrenceCounts[ definition.Key ] = count;
		if (
			1 < count
			&& !definition.AllowMultiple
		) {
			errors.Add(
				new OptionParseError(
					OptionParseErrorKind.DuplicateOption,
					token.ArgumentIndex,
					token.OriginalValue,
					spelling
				)
			);
		}

		var occurrence = new OptionOccurrence(
			definition,
			spelling,
			value,
			token.ArgumentIndex,
			token.OriginalValue
		);
		options.Add(
			occurrence
		);
		items.Add(
			OptionParseItem.FromOption(
				occurrence
			)
		);
	}

	private static bool CanUseAsSeparateOptionalValue(
		string token
	) {
		return (
			"-" == token
			|| !token.StartsWith(
				"-",
				StringComparison.Ordinal
			)
		);
	}

}
