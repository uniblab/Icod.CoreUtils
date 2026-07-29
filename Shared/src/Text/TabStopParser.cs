namespace Icod.CoreUtils.Shared.Text;

/// <summary>Parses the reusable GNU tab-stop grammar used by text-layout utilities.</summary>
public static class TabStopParser {
	/// <summary>Parses one tab-stop specification.</summary>
	/// <param name="specification">The comma- or blank-separated specification.</param>
	/// <returns>The controlled parse result.</returns>
	/// <exception cref="ArgumentNullException">The specification is <see langword="null"/>.</exception>
	public static TabStopParseResult Parse( string specification ) {
		ArgumentNullException.ThrowIfNull( specification );
		return Parse( new[] { specification } );
	}

	/// <summary>
	/// Parses one or more tab-stop specifications as one encounter-ordered list.
	/// </summary>
	/// <param name="specifications">The specifications supplied by repeated command options.</param>
	/// <returns>The controlled parse result.</returns>
	/// <exception cref="ArgumentNullException">The sequence or one of its specifications is <see langword="null"/>.</exception>
	public static TabStopParseResult Parse( IEnumerable<string> specifications ) {
		ArgumentNullException.ThrowIfNull( specifications );
		var explicitStops = new List<ulong>();
		ulong? absoluteInterval = null;
		ulong? relativeInterval = null;
		var specificationIndex = 0;
		foreach ( var specification in specifications ) {
			ArgumentNullException.ThrowIfNull( specification );
			var prefix = TabStopContinuationKind.None;
			var prefixIndex = -1;
			var haveValue = false;
			var numberStart = -1;
			ulong value = 0;
			for ( var characterIndex = 0; characterIndex <= specification.Length; characterIndex++ ) {
				var atEnd = characterIndex == specification.Length;
				var character = atEnd
					? '\0'
					: specification[characterIndex];
				if ( atEnd || IsSeparator( character ) ) {
					if ( haveValue ) {
						var commitError = CommitValue(
							value,
							prefix,
							explicitStops,
							ref absoluteInterval,
							ref relativeInterval,
							specificationIndex,
							prefixIndex >= 0 ? prefixIndex : numberStart,
							specification[numberStart..characterIndex]
						);
						if ( commitError is not null ) {
							return TabStopParseResult.Failed( commitError );
						}
						haveValue = false;
					}
					continue;
				}
				if ( character is '/' or '+' ) {
					if ( haveValue ) {
						return Fail(
							TabStopParseErrorCode.SpecifierNotAtStart,
							"A recurring-interval specifier must occur at the start of a number.",
							specificationIndex,
							characterIndex,
							specification[characterIndex..]
						);
					}
					prefix = character == '/'
						? TabStopContinuationKind.Absolute
						: TabStopContinuationKind.Relative;
					prefixIndex = characterIndex;
					continue;
				}
				if ( !char.IsAsciiDigit( character ) ) {
					return Fail(
						TabStopParseErrorCode.InvalidCharacter,
						"The tab-stop specification contains an invalid character.",
						specificationIndex,
						characterIndex,
						specification[characterIndex..]
					);
				}
				if ( !haveValue ) {
					haveValue = true;
					numberStart = characterIndex;
					value = 0;
				}
				var digit = (ulong)(character - '0');
				if ( value > ((ulong.MaxValue - digit) / 10) ) {
					var end = characterIndex + 1;
					while ( (end < specification.Length)
						&& char.IsAsciiDigit( specification[end] ) ) {
						end++;
					}
					return Fail(
						TabStopParseErrorCode.NumberOverflow,
						"A tab-stop value exceeds the supported range.",
						specificationIndex,
						numberStart,
						specification[numberStart..end]
					);
				}
				value = (value * 10) + digit;
			}
			specificationIndex++;
		}

		if ( (absoluteInterval is not null) && (relativeInterval is not null) ) {
			return Fail(
				TabStopParseErrorCode.MutuallyExclusiveContinuations,
				"Absolute and relative recurring tab intervals are mutually exclusive.",
				-1,
				-1,
				null
			);
		}
		if ( explicitStops.Count == 0 ) {
			if ( absoluteInterval is not null ) {
				return TabStopParseResult.Succeeded(
					TabStopSet.Every( absoluteInterval.Value )
				);
			}
			if ( relativeInterval is not null ) {
				return TabStopParseResult.Succeeded(
					TabStopSet.Every( relativeInterval.Value )
				);
			}
			return TabStopParseResult.Succeeded( TabStopSet.Default );
		}
		if ( (explicitStops.Count == 1)
			&& (absoluteInterval is null)
			&& (relativeInterval is null) ) {
			return TabStopParseResult.Succeeded(
				TabStopSet.Every( explicitStops[0] )
			);
		}
		var continuation = absoluteInterval is not null
			? TabStopContinuation.Absolute( absoluteInterval.Value )
			: relativeInterval is not null
				? TabStopContinuation.Relative( relativeInterval.Value )
				: TabStopContinuation.None;
		return TabStopParseResult.Succeeded(
			TabStopSet.Create(
				explicitStops,
				continuation
			)
		);
	}

	private static TabStopParseError? CommitValue(
		ulong value,
		TabStopContinuationKind prefix,
		List<ulong> explicitStops,
		ref ulong? absoluteInterval,
		ref ulong? relativeInterval,
		int specificationIndex,
		int characterIndex,
		string token
	) {
		if ( prefix == TabStopContinuationKind.Absolute ) {
			if ( absoluteInterval is not null ) {
				return new(
					TabStopParseErrorCode.ContinuationNotLast,
					"An absolute recurring interval is only allowed with the last value.",
					specificationIndex,
					characterIndex,
					token
				);
			}
			if ( value != 0 ) {
				absoluteInterval = value;
			}
			return null;
		}
		if ( prefix == TabStopContinuationKind.Relative ) {
			if ( relativeInterval is not null ) {
				return new(
					TabStopParseErrorCode.ContinuationNotLast,
					"A relative recurring interval is only allowed with the last value.",
					specificationIndex,
					characterIndex,
					token
				);
			}
			if ( value != 0 ) {
				relativeInterval = value;
			}
			return null;
		}
		if ( value == 0 ) {
			return new(
				TabStopParseErrorCode.Zero,
				"Explicit tab stops must be positive.",
				specificationIndex,
				characterIndex,
				token
			);
		}
		if ( (explicitStops.Count > 0) && (value <= explicitStops[^1]) ) {
			return new(
				TabStopParseErrorCode.NotIncreasing,
				"Explicit tab stops must be strictly increasing.",
				specificationIndex,
				characterIndex,
				token
			);
		}
		explicitStops.Add( value );
		return null;
	}

	private static bool IsSeparator( char value ) => value is ',' or ' ' or '\t';

	private static TabStopParseResult Fail(
		TabStopParseErrorCode code,
		string message,
		int specificationIndex,
		int characterIndex,
		string? token
	) => TabStopParseResult.Failed(
		new(
			code,
			message,
			specificationIndex,
			characterIndex,
			token
		)
	);
}
