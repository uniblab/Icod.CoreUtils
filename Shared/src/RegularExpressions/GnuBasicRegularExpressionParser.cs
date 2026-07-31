using System.Buffers;
using System.Globalization;
using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>
/// Provides the shared GNU basic and Emacs regular-expression parser implementation.
/// </summary>
internal sealed class GnuBasicRegularExpressionParser {
	/// <summary>Gets the greatest interval bound accepted by the GNU regular-expression grammar.</summary>
	internal const int MaximumInterval = 32_767;

	private readonly string pattern;
	private readonly RegularExpressionOptions options;
	private readonly IRegularExpressionCharacterClassProvider characterClassProvider;
	private readonly CancellationToken cancellationToken;
	private readonly HashSet<int> closedCaptures = [];
	private int index;
	private int captureCount;
	private RegularExpressionDiagnostic? diagnostic;

	/// <summary>
	/// Initializes a new instance of the GnuBasicRegularExpressionParser class.
	/// </summary>
	internal GnuBasicRegularExpressionParser(
		string pattern,
		RegularExpressionOptions options,
		IRegularExpressionCharacterClassProvider characterClassProvider,
		CancellationToken cancellationToken
	) {
		this.pattern = pattern;
		this.options = options;
		this.characterClassProvider = characterClassProvider;
		this.cancellationToken = cancellationToken;
	}

	/// <summary>
	/// Performs the parse operation.
	/// </summary>
	internal GnuBasicParseResult Parse() {
		cancellationToken.ThrowIfCancellationRequested();
		var expression = ParseAlternation( false, 0 );
		if ( diagnostic is null && pattern.Length != index ) {
			if ( IsEscapedOperator( index, ')' ) ) {
				Fail(
					RegularExpressionDiagnosticCode.UnmatchedClosingSubexpression,
					"unmatched closing subexpression operator",
					index
				);
			}
		}
		return new(
			diagnostic is null ? expression : null,
			captureCount,
			diagnostic
		);
	}

	private RegexNode ParseAlternation( bool insideSubexpression, int nestingDepth ) {
		var alternatives = new List<RegexNode>();
		while ( true ) {
			alternatives.Add( ParseSequence( insideSubexpression, nestingDepth ) );
			if ( diagnostic is not null || !IsEscapedOperator( index, '|' ) ) {
				break;
			}
			index += 2;
			cancellationToken.ThrowIfCancellationRequested();
		}
		return 1 == alternatives.Count
			? alternatives[ 0 ]
			: new AlternationRegexNode( alternatives );
	}

	private RegexNode ParseSequence( bool insideSubexpression, int nestingDepth ) {
		var nodes = new List<RegexNode>();
		var atBranchStart = true;
		while (
			diagnostic is null
			&& pattern.Length > index
			&& !IsEscapedOperator( index, '|' )
			&& !( insideSubexpression && IsEscapedOperator( index, ')' ) )
		) {
			cancellationToken.ThrowIfCancellationRequested();
			ParsedAtom atom;
			if ( '^' == pattern[ index ] && atBranchStart ) {
				index++;
				atom = new( new AssertionRegexNode( RegexAssertionKind.BeginLine ), false );
			} else if ( '$' == pattern[ index ] && IsEndAnchor( insideSubexpression ) ) {
				index++;
				atom = new( new AssertionRegexNode( RegexAssertionKind.EndLine ), false );
			} else {
				atom = ParseAtom( nestingDepth );
			}
			if ( diagnostic is null ) {
				nodes.Add( ParseRepetition( atom ) );
			}
			atBranchStart = false;
		}
		return nodes.Count switch {
			0 => EmptyRegexNode.Instance,
			1 => nodes[ 0 ],
			_ => new SequenceRegexNode( nodes )
		};
	}

	private ParsedAtom ParseAtom( int nestingDepth ) {
		var sourceIndex = index;
		var current = pattern[ index ];
		if ( '.' == current ) {
			index++;
			return new( new DotRegexNode(), true );
		}
		if ( '[' == current ) {
			return new( ParseBracketExpression(), true );
		}
		if ( '\\' != current ) {
			var value = ReadPatternRune();
			return new( new LiteralRegexNode( value ), true );
		}
		if ( pattern.Length == index + 1 ) {
			Fail(
				RegularExpressionDiagnosticCode.TrailingEscape,
				"trailing backslash",
				index
			);
			return new( EmptyRegexNode.Instance, false );
		}
		var escaped = pattern[ index + 1 ];
		switch ( escaped ) {
			case '(':
				if ( options.MaximumNestingDepth <= nestingDepth ) {
					Fail(
						RegularExpressionDiagnosticCode.NestingDepthExceeded,
						string.Concat(
							"subexpression nesting exceeds the configured limit of ",
							options.MaximumNestingDepth.ToString( CultureInfo.InvariantCulture )
						),
						sourceIndex
					);
					return new( EmptyRegexNode.Instance, false );
				}
				index += 2;
				var captureNumber = ++captureCount;
				var expression = ParseAlternation( true, nestingDepth + 1 );
				if ( diagnostic is null ) {
					if ( !IsEscapedOperator( index, ')' ) ) {
						Fail(
							RegularExpressionDiagnosticCode.UnterminatedSubexpression,
							"unterminated subexpression",
							sourceIndex
						);
					} else {
						index += 2;
						closedCaptures.Add( captureNumber );
					}
				}
				return new( new GroupRegexNode( captureNumber, expression ), true );
			case ')':
				Fail(
					RegularExpressionDiagnosticCode.UnmatchedClosingSubexpression,
					"unmatched closing subexpression operator",
					sourceIndex
				);
				return new( EmptyRegexNode.Instance, false );
			case '|':
				index += 2;
				return new( new LiteralRegexNode( new Rune( escaped ) ), true );
			case '+':
			case '?':
				index += 2;
				return new( new LiteralRegexNode( new Rune( escaped ) ), true );
			case '{':
				if ( !options.AllowInvalidRepetitionOperators ) {
					Fail(
						RegularExpressionDiagnosticCode.InvalidRepetitionOperator,
						"interval operator has no preceding expression",
						sourceIndex
					);
					return new( EmptyRegexNode.Instance, false );
				}
				index += 2;
				return new( new LiteralRegexNode( new Rune( escaped ) ), true );
			case 'w':
				index += 2;
				return new( new CharacterClassRegexNode( "word", false ), true );
			case 'W':
				index += 2;
				return new( new CharacterClassRegexNode( "word", true ), true );
			case 's':
				index += 2;
				return new( new CharacterClassRegexNode( "space", false ), true );
			case 'S':
				index += 2;
				return new( new CharacterClassRegexNode( "space", true ), true );
			case '<':
				index += 2;
				return new( new AssertionRegexNode( RegexAssertionKind.BeginWord ), false );
			case '>':
				index += 2;
				return new( new AssertionRegexNode( RegexAssertionKind.EndWord ), false );
			case 'b':
				index += 2;
				return new( new AssertionRegexNode( RegexAssertionKind.WordBoundary ), false );
			case 'B':
				index += 2;
				return new( new AssertionRegexNode( RegexAssertionKind.NotWordBoundary ), false );
			case '`':
				index += 2;
				return new( new AssertionRegexNode( RegexAssertionKind.BeginInput ), false );
			case '\'':
				index += 2;
				return new( new AssertionRegexNode( RegexAssertionKind.EndInput ), false );
			default:
				if ( escaped is >= '1' and <= '9' ) {
					index += 2;
					var referencedCapture = escaped - '0';
					if ( !closedCaptures.Contains( referencedCapture ) ) {
						Fail(
							RegularExpressionDiagnosticCode.InvalidBackReference,
							string.Concat( "invalid back-reference \\", escaped ),
							sourceIndex
						);
					}
					return new( new BackReferenceRegexNode( referencedCapture ), true );
				}
				index++;
				var literal = ReadPatternRune();
				return new( new LiteralRegexNode( literal ), true );
		}
	}

	private RegexNode ParseRepetition( ParsedAtom atom ) {
		if ( diagnostic is not null || pattern.Length <= index || !atom.IsRepeatable ) {
			return atom.Node;
		}
		var node = atom.Node;
		var hasRepetition = false;
		var repetitionDepth = 0;
		while ( diagnostic is null && pattern.Length > index ) {
			int minimum;
			int? maximum;
			var repetitionIndex = index;
			if ( '*' == pattern[ index ] ) {
				if ( hasRepetition && !options.AllowInvalidRepetitionOperators ) {
					Fail(
						RegularExpressionDiagnosticCode.InvalidRepetitionOperator,
						"invalid adjacent repetition operator",
						index
					);
					break;
				}
				minimum = 0;
				maximum = null;
				index++;
			} else if ( IsPlusOrQuestionOperator( index, '+' ) ) {
				minimum = 1;
				maximum = null;
				index += RepetitionOperatorLength;
			} else if ( IsPlusOrQuestionOperator( index, '?' ) ) {
				minimum = 0;
				maximum = 1;
				index += RepetitionOperatorLength;
			} else if ( IsEscapedOperator( index, '{' ) ) {
				if ( hasRepetition && !options.AllowInvalidRepetitionOperators ) {
					Fail(
						RegularExpressionDiagnosticCode.InvalidRepetitionOperator,
						"invalid adjacent interval operator",
						index
					);
					break;
				}
				if ( !TryParseInterval( out minimum, out maximum ) ) {
					break;
				}
			} else {
				break;
			}
			if ( options.MaximumNestingDepth <= repetitionDepth ) {
				Fail(
					RegularExpressionDiagnosticCode.NestingDepthExceeded,
					string.Concat(
						"repetition nesting exceeds the configured limit of ",
						options.MaximumNestingDepth.ToString( CultureInfo.InvariantCulture )
					),
					repetitionIndex
				);
				break;
			}
			node = new RepeatRegexNode( node, minimum, maximum );
			hasRepetition = true;
			repetitionDepth++;
		}
		return node;
	}

	private bool TryParseInterval( out int minimum, out int? maximum ) {
		var sourceIndex = index;
		index += 2;
		minimum = 0;
		maximum = null;
		var minimumStart = index;
		while ( pattern.Length > index && char.IsAsciiDigit( pattern[ index ] ) ) {
			index++;
		}
		var hasMinimum = minimumStart != index;
		if ( hasMinimum && !TryParseIntervalBound(
			pattern.AsSpan( minimumStart, index - minimumStart ),
			out minimum
		) ) {
			Fail( RegularExpressionDiagnosticCode.InvalidInterval, "invalid interval expression", sourceIndex );
			return false;
		}
		if ( pattern.Length > index && ',' == pattern[ index ] ) {
			index++;
			var maximumStart = index;
			while ( pattern.Length > index && char.IsAsciiDigit( pattern[ index ] ) ) {
				index++;
			}
			if ( maximumStart != index ) {
				if ( !TryParseIntervalBound(
					pattern.AsSpan( maximumStart, index - maximumStart ),
					out var parsedMaximum
				) ) {
					Fail( RegularExpressionDiagnosticCode.InvalidInterval, "invalid interval expression", sourceIndex );
					return false;
				}
				maximum = parsedMaximum;
			}
		} else if ( hasMinimum ) {
			maximum = minimum;
		} else {
			Fail( RegularExpressionDiagnosticCode.InvalidInterval, "invalid interval expression", sourceIndex );
			return false;
		}
		if ( !IsEscapedOperator( index, '}' ) ) {
			Fail( RegularExpressionDiagnosticCode.InvalidInterval, "unterminated interval expression", sourceIndex );
			return false;
		}
		index += 2;
		if (
			MaximumInterval < minimum
			|| ( maximum is int maximumValue
				&& ( MaximumInterval < maximumValue || minimum > maximumValue ) )
		) {
			Fail(
				RegularExpressionDiagnosticCode.InvalidInterval,
				string.Concat( "interval bounds must be between 0 and ", MaximumInterval ),
				sourceIndex
			);
			return false;
		}
		return true;
	}

	private RegexNode ParseBracketExpression() {
		var sourceIndex = index;
		index++;
		var isNegated = pattern.Length > index && '^' == pattern[ index ];
		if ( isNegated ) {
			index++;
		}
		var terms = new List<BracketExpressionTerm>();
		var first = true;
		while ( pattern.Length > index ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( ']' == pattern[ index ] && !first ) {
				index++;
				return new BracketRegexNode( terms, isNegated );
			}
			if (
				!first
				&& '-' == pattern[ index ]
				&& pattern.Length > index + 1
				&& ']' != pattern[ index + 1 ]
			) {
				Fail(
					RegularExpressionDiagnosticCode.InvalidRange,
					"hyphen is not a valid bracket-range endpoint",
					index
				);
				return EmptyRegexNode.Instance;
			}
			var element = ParseBracketElement();
			if ( diagnostic is null ) {
				if (
					element.RangeEndpoint is Rune rangeStart
					&& pattern.Length > index + 1
					&& '-' == pattern[ index ]
					&& ']' != pattern[ index + 1 ]
				) {
					index++;
					var endElement = ParseBracketElement();
					if ( endElement.RangeEndpoint is not Rune rangeEnd ) {
						Fail(
							RegularExpressionDiagnosticCode.InvalidRange,
							"a bracket range endpoint must be one collating element",
							index
						);
					} else if ( 0 < characterClassProvider.Compare(
						rangeStart,
						rangeEnd,
						options.IgnoreCase
					) ) {
						if ( options.AllowEmptyRanges ) {
							terms.Add( BracketNeverTerm.Instance );
						} else {
							Fail(
								RegularExpressionDiagnosticCode.InvalidRange,
								"bracket range has reverse collation order",
								sourceIndex
							);
						}
					} else {
						terms.Add( new BracketRangeTerm( rangeStart, rangeEnd ) );
					}
				} else {
					terms.Add( element.Term );
				}
			}
			first = false;
			if ( diagnostic is not null ) {
				return EmptyRegexNode.Instance;
			}
		}
		Fail(
			RegularExpressionDiagnosticCode.UnterminatedBracketExpression,
			"unterminated bracket expression",
			sourceIndex
		);
		return EmptyRegexNode.Instance;
	}

	private ParsedBracketElement ParseBracketElement() {
		var sourceIndex = index;
		if (
			pattern.Length > index + 1
			&& '[' == pattern[ index ]
			&& pattern[ index + 1 ] is ':' or '.' or '='
		) {
			var delimiter = pattern[ index + 1 ];
			var contentStart = index + 2;
			var closing = string.Concat( delimiter, "]" );
			var contentEnd = pattern.IndexOf( closing, contentStart, StringComparison.Ordinal );
			if ( 0 > contentEnd ) {
				Fail(
					RegularExpressionDiagnosticCode.UnterminatedBracketExpression,
					"unterminated bracket-expression construct",
					sourceIndex
				);
				return new( new BracketLiteralTerm( Rune.ReplacementChar ), null );
			}
			var content = pattern[ contentStart..contentEnd ];
			index = contentEnd + 2;
			if ( ':' == delimiter ) {
				var className = content;
				if ( !characterClassProvider.IsSupportedClass( className ) ) {
					Fail(
						RegularExpressionDiagnosticCode.InvalidCharacterClass,
						string.Concat( "invalid character class '", content, "'" ),
						sourceIndex
					);
				}
				return new( new BracketCharacterClassTerm( className ), null );
			}
			if ( !TryReadSingleRune( content, out var value ) ) {
				Fail(
					RegularExpressionDiagnosticCode.UnsupportedCollatingElement,
					"multi-scalar collating elements are not supported by the configured provider",
					sourceIndex
				);
				return new( new BracketLiteralTerm( Rune.ReplacementChar ), null );
			}
			return '=' == delimiter
				? new( new BracketEquivalenceTerm( value ), null )
				: new( new BracketLiteralTerm( value ), value );
		}
		Rune literal;
		if ( GnuRegularExpressionSyntax.Emacs == this.options.Syntax && '\\' == pattern[ index ] ) {
			literal = new Rune( '\\' );
			index++;
		} else {
			literal = ReadPatternRune();
		}
		return new( new BracketLiteralTerm( literal ), literal );
	}

	private static bool TryParseIntervalBound( ReadOnlySpan<char> value, out int result ) {
		var firstSignificant = 0;
		while ( value.Length > firstSignificant && '0' == value[ firstSignificant ] ) {
			firstSignificant++;
		}
		if ( value.Length == firstSignificant ) {
			result = 0;
			return true;
		}
		var significant = value[ firstSignificant.. ];
		return int.TryParse(
			significant,
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out result
		);
	}

	private bool IsEndAnchor( bool insideSubexpression ) {
		var next = index + 1;
		return pattern.Length == next
			|| IsEscapedOperator( next, '|' )
			|| ( insideSubexpression && IsEscapedOperator( next, ')' ) );
	}


	private int RepetitionOperatorLength => GnuRegularExpressionSyntax.Emacs == this.options.Syntax ? 1 : 2;

	private bool IsPlusOrQuestionOperator( int sourceIndex, char value ) =>
		GnuRegularExpressionSyntax.Emacs == this.options.Syntax
			? pattern.Length > sourceIndex && value == pattern[ sourceIndex ]
			: IsEscapedOperator( sourceIndex, value );

	private bool IsEscapedOperator( int sourceIndex, char value ) =>
		pattern.Length > sourceIndex + 1
		&& '\\' == pattern[ sourceIndex ]
		&& value == pattern[ sourceIndex + 1 ];


	private Rune ReadPatternRune() {
		var status = Rune.DecodeFromUtf16( pattern.AsSpan( index ), out var value, out var consumed );
		if ( OperationStatus.Done != status ) {
			value = Rune.ReplacementChar;
			consumed = 1;
		}
		index += consumed;
		return value;
	}

	private static bool TryReadSingleRune( string value, out Rune result ) {
		var status = Rune.DecodeFromUtf16( value.AsSpan(), out result, out var consumed );
		return OperationStatus.Done == status && value.Length == consumed;
	}

	private void Fail( RegularExpressionDiagnosticCode code, string message, int patternIndex ) {
		diagnostic ??= new( code, message, patternIndex );
	}

	private readonly record struct ParsedAtom( RegexNode Node, bool IsRepeatable );

	private readonly record struct ParsedBracketElement(
		BracketExpressionTerm Term,
		Rune? RangeEndpoint
	);
}
