using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>
/// Represents gnu basic parse result.
/// </summary>
/// <param name="Expression">The expression value.</param>
/// <param name="CaptureCount">The capture count value.</param>
/// <param name="Diagnostic">The diagnostic value.</param>
internal sealed record GnuBasicParseResult(
	RegexNode? Expression,
	int CaptureCount,
	RegularExpressionDiagnostic? Diagnostic
);

/// <summary>
/// Provides the regex node implementation.
/// </summary>
internal abstract class RegexNode {
	/// <summary>
	/// Performs the match operation.
	/// </summary>
	internal abstract IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state );
}

/// <summary>
/// Provides the empty regex node implementation.
/// </summary>
internal sealed class EmptyRegexNode : RegexNode {
	/// <summary>
	/// Performs the new operation.
	/// </summary>
	internal static EmptyRegexNode Instance { get; } = new();

	private EmptyRegexNode() {
	}

	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.RegisterState();
		yield return state;
	}
}

/// <summary>
/// Provides the literal regex node implementation.
/// </summary>
/// <param name="value">The value value.</param>
internal sealed class LiteralRegexNode( Rune value ) : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if (
			context.Input.Length > state.Position
			&& !context.Input.IsOpaque( state.Position )
			&& context.CharacterClassProvider.AreCharactersEqual(
				value,
				context.Input[ state.Position ],
				context.Options.IgnoreCase
			)
		) {
			context.RegisterState();
			yield return state.WithPosition( state.Position + 1 );
		}
	}
}

/// <summary>
/// Provides the dot regex node implementation.
/// </summary>
internal sealed class DotRegexNode : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( context.Input.Length <= state.Position ) {
			yield break;
		}
		var value = context.Input[ state.Position ];
		var lineSeparator = context.Options.LineSeparator.Value;
		if (
			(
				GnuRegularExpressionSyntax.Emacs != context.Options.Syntax
				&& !context.Options.DotMatchesNull
				&& 0 == value.Value
			)
			|| ( lineSeparator == value.Value
				&& ( GnuRegularExpressionSyntax.Emacs == context.Options.Syntax
					|| context.Options.NewLineSensitive ) )
		) {
			yield break;
		}
		context.RegisterState();
		yield return state.WithPosition( state.Position + 1 );
	}
}

/// <summary>
/// Identifies the available regex assertion kind values.
/// </summary>
internal enum RegexAssertionKind {
	/// <summary>
	/// Specifies begin line.
	/// </summary>
	BeginLine,
	/// <summary>
	/// Specifies end line.
	/// </summary>
	EndLine,
	/// <summary>
	/// Specifies begin input.
	/// </summary>
	BeginInput,
	/// <summary>
	/// Specifies end input.
	/// </summary>
	EndInput,
	/// <summary>
	/// Specifies word boundary.
	/// </summary>
	WordBoundary,
	/// <summary>
	/// Specifies not word boundary.
	/// </summary>
	NotWordBoundary,
	/// <summary>
	/// Specifies begin word.
	/// </summary>
	BeginWord,
	/// <summary>
	/// Specifies end word.
	/// </summary>
	EndWord
}

/// <summary>
/// Provides the assertion regex node implementation.
/// </summary>
/// <param name="kind">The kind value.</param>
internal sealed class AssertionRegexNode( RegexAssertionKind kind ) : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		var previousIsWord = 0 < state.Position
			&& !context.Input.IsOpaque( state.Position - 1 )
			&& context.CharacterClassProvider.IsWordCharacter( context.Input[ state.Position - 1 ] );
		var currentIsWord = context.Input.Length > state.Position
			&& !context.Input.IsOpaque( state.Position )
			&& context.CharacterClassProvider.IsWordCharacter( context.Input[ state.Position ] );
		var matches = kind switch {
			RegexAssertionKind.BeginLine => 0 == state.Position
				|| ( context.Options.NewLineSensitive
				&& context.Options.LineSeparator.Value == context.Input[ state.Position - 1 ].Value ),
			RegexAssertionKind.EndLine => context.Input.Length == state.Position
				|| ( context.Options.NewLineSensitive
				&& context.Options.LineSeparator.Value == context.Input[ state.Position ].Value ),
			RegexAssertionKind.BeginInput => 0 == state.Position,
			RegexAssertionKind.EndInput => context.Input.Length == state.Position,
			RegexAssertionKind.WordBoundary => previousIsWord != currentIsWord,
			RegexAssertionKind.NotWordBoundary => previousIsWord == currentIsWord,
			RegexAssertionKind.BeginWord => !previousIsWord && currentIsWord,
			RegexAssertionKind.EndWord => previousIsWord && !currentIsWord,
			_ => false
		};
		if ( matches ) {
			context.RegisterState();
			yield return state;
		}
	}
}

/// <summary>
/// Provides the bracket expression term implementation.
/// </summary>
internal abstract class BracketExpressionTerm {
	/// <summary>
	/// Matches es.
	/// </summary>
	internal abstract bool Matches( RegexMatchContext context, Rune value );
}

/// <summary>
/// Provides the bracket literal term implementation.
/// </summary>
/// <param name="literal">The literal value.</param>
internal sealed class BracketLiteralTerm( Rune literal ) : BracketExpressionTerm {
	/// <summary>
	/// Gets the literal value.
	/// </summary>
	internal Rune Literal { get; } = literal;

	/// <inheritdoc/>
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		context.CharacterClassProvider.AreCharactersEqual( Literal, value, context.Options.IgnoreCase );
}

/// <summary>
/// Provides the bracket range term implementation.
/// </summary>
/// <param name="start">The start value.</param>
/// <param name="end">The end value.</param>
internal sealed class BracketRangeTerm( Rune start, Rune end ) : BracketExpressionTerm {
	/// <inheritdoc/>
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		0 >= context.CharacterClassProvider.Compare( start, value, context.Options.IgnoreCase )
		&& 0 <= context.CharacterClassProvider.Compare( end, value, context.Options.IgnoreCase );
}

/// <summary>
/// Provides the bracket character class term implementation.
/// </summary>
/// <param name="className">The class name value.</param>
internal sealed class BracketCharacterClassTerm( string className ) : BracketExpressionTerm {
	/// <inheritdoc/>
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		context.CharacterClassProvider.IsCharacterClass( value, className, context.Options.IgnoreCase );
}

/// <summary>
/// Provides the bracket equivalence term implementation.
/// </summary>
/// <param name="equivalent">The equivalent value.</param>
internal sealed class BracketEquivalenceTerm( Rune equivalent ) : BracketExpressionTerm {
	/// <inheritdoc/>
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		context.CharacterClassProvider.AreCollatingElementsEquivalent(
			equivalent,
			value,
			context.Options.IgnoreCase
		);
}

/// <summary>
/// Provides the bracket never term implementation.
/// </summary>
internal sealed class BracketNeverTerm : BracketExpressionTerm {
	/// <summary>
	/// Performs the new operation.
	/// </summary>
	internal static BracketNeverTerm Instance { get; } = new();

	private BracketNeverTerm() {
	}

	/// <inheritdoc/>
	internal override bool Matches( RegexMatchContext context, Rune value ) => false;
}

/// <summary>
/// Provides the character class regex node implementation.
/// </summary>
/// <param name="className">The class name value.</param>
/// <param name="isNegated">The is negated value.</param>
internal sealed class CharacterClassRegexNode(
	string className,
	bool isNegated
) : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( context.Input.Length <= state.Position ) {
			yield break;
		}
		var value = context.Input[ state.Position ];
		var matches = !context.Input.IsOpaque( state.Position )
			&& ( "word" == className
				? context.CharacterClassProvider.IsWordCharacter( value )
				: context.CharacterClassProvider.IsCharacterClass(
					value,
					className,
					context.Options.IgnoreCase
				) );
		if ( isNegated ) {
			matches = !matches;
		}
		if ( matches ) {
			context.RegisterState();
			yield return state.WithPosition( state.Position + 1 );
		}
	}
}

/// <summary>
/// Provides the bracket regex node implementation.
/// </summary>
/// <param name="terms">The terms value.</param>
/// <param name="isNegated">The is negated value.</param>
internal sealed class BracketRegexNode(
	IReadOnlyList<BracketExpressionTerm> terms,
	bool isNegated
) : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( context.Input.Length <= state.Position ) {
			yield break;
		}
		var value = context.Input[ state.Position ];
		var any = false;
		if ( !context.Input.IsOpaque( state.Position ) ) {
			foreach ( var term in terms ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				if ( term.Matches( context, value ) ) {
					any = true;
					break;
				}
			}
		}
		var matches = isNegated ? !any : any;
		if ( isNegated
			&& context.Options.NewLineSensitive
			&& context.Options.LineSeparator.Value == value.Value ) {
			matches = false;
		}
		if ( matches ) {
			context.RegisterState();
			yield return state.WithPosition( state.Position + 1 );
		}
	}
}

/// <summary>
/// Provides the sequence regex node implementation.
/// </summary>
internal sealed class SequenceRegexNode : RegexNode {
	private readonly IReadOnlyList<RegexNode> nodes;

	/// <summary>
	/// Initializes a new instance of the SequenceRegexNode class.
	/// </summary>
	internal SequenceRegexNode( IReadOnlyList<RegexNode> nodes ) {
		this.nodes = nodes;
	}

	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		var current = new List<RegexMatchState> { state };
		foreach ( var node in nodes ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var next = new List<RegexMatchState>();
			var seen = new HashSet<RegexMatchState>( RegexMatchStateComparer.Instance );
			foreach ( var candidate in current ) {
				foreach ( var result in node.Match( context, candidate ) ) {
					if ( seen.Add( result ) ) {
						next.Add( result );
					}
				}
			}
			current = next;
			if ( 0 == current.Count ) {
				yield break;
			}
		}
		foreach ( var result in current ) {
			context.RegisterState();
			yield return result;
		}
	}
}

/// <summary>
/// Provides the alternation regex node implementation.
/// </summary>
internal sealed class AlternationRegexNode : RegexNode {
	private readonly IReadOnlyList<RegexNode> alternatives;

	/// <summary>
	/// Initializes a new instance of the AlternationRegexNode class.
	/// </summary>
	internal AlternationRegexNode( IReadOnlyList<RegexNode> alternatives ) {
		this.alternatives = alternatives;
	}

	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		var results = new HashSet<RegexMatchState>( RegexMatchStateComparer.Instance );
		foreach ( var alternative in alternatives ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			foreach ( var result in alternative.Match( context, state ) ) {
				if ( results.Add( result ) ) {
					yield return result;
				}
			}
		}
	}
}

/// <summary>
/// Provides the group regex node implementation.
/// </summary>
/// <param name="captureNumber">The capture number value.</param>
/// <param name="expression">The expression value.</param>
internal sealed class GroupRegexNode( int captureNumber, RegexNode expression ) : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		var start = state.Position;
		var results = new HashSet<RegexMatchState>( RegexMatchStateComparer.Instance );
		foreach ( var result in expression.Match( context, state ) ) {
			var captured = result.WithCapture( captureNumber, new RegexCaptureSpan( start, result.Position ) );
			if ( results.Add( captured ) ) {
				context.RegisterState();
				yield return captured;
			}
		}
	}
}

/// <summary>
/// Provides the back reference regex node implementation.
/// </summary>
/// <param name="captureNumber">The capture number value.</param>
internal sealed class BackReferenceRegexNode( int captureNumber ) : RegexNode {
	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		var capture = state.Captures[ captureNumber - 1 ];
		if ( capture is not RegexCaptureSpan captureSpan ) {
			yield break;
		}
		var length = captureSpan.End - captureSpan.Start;
		if ( length > context.Input.Length - state.Position ) {
			yield break;
		}
		for ( var offset = 0; length > offset; offset++ ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var capturePosition = captureSpan.Start + offset;
			var inputPosition = state.Position + offset;
			var captureIsOpaque = context.Input.IsOpaque( capturePosition );
			var inputIsOpaque = context.Input.IsOpaque( inputPosition );
			if (
				captureIsOpaque != inputIsOpaque
				|| ( captureIsOpaque
					? context.Input[ capturePosition ].Value != context.Input[ inputPosition ].Value
					: !context.CharacterClassProvider.AreCharactersEqual(
						context.Input[ capturePosition ],
						context.Input[ inputPosition ],
						context.Options.IgnoreCase
					) )
			) {
				yield break;
			}
		}
		context.RegisterState();
		yield return state.WithPosition( state.Position + length );
	}
}

/// <summary>
/// Provides the repeat regex node implementation.
/// </summary>
internal sealed class RepeatRegexNode : RegexNode {
	private readonly RegexNode expression;
	private readonly int minimum;
	private readonly int? maximum;

	/// <summary>
	/// Initializes a new instance of the RepeatRegexNode class.
	/// </summary>
	internal RepeatRegexNode( RegexNode expression, int minimum, int? maximum ) {
		this.expression = expression;
		this.minimum = minimum;
		this.maximum = maximum;
	}

	/// <inheritdoc/>
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		var yielded = new HashSet<RegexMatchState>( RegexMatchStateComparer.Instance );
		var pathStates = new HashSet<RegexMatchState>( RegexMatchStateComparer.Instance ) { state };
		var stack = new Stack<RepeatTraversalFrame>();
		stack.Push( new( state, 0, true ) );
		try {
			while ( 0 < stack.Count ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var frame = stack.Peek();
				if ( !frame.IsInitialized ) {
					frame.IsInitialized = true;
					if ( maximum is not int maximumValue || frame.Count < maximumValue ) {
						frame.Children = expression.Match( context, frame.State ).GetEnumerator();
					}
				}

				var descended = false;
				var children = frame.Children;
				if ( children is not null ) {
					while ( children.MoveNext() ) {
						context.CancellationToken.ThrowIfCancellationRequested();
						var child = children.Current;
						var childCount = frame.Count + 1;
						if (
							child.Position == frame.State.Position
							&& frame.Count >= minimum
							&& 0 != frame.Count
						) {
							continue;
						}
						var addedToPath = pathStates.Add( child );
						if ( !addedToPath && childCount > minimum ) {
							continue;
						}
						stack.Push( new( child, childCount, addedToPath ) );
						descended = true;
						break;
					}
				}
				if ( descended ) {
					continue;
				}

				frame.Children?.Dispose();
				stack.Pop();
				if ( frame.AddedToPath ) {
					pathStates.Remove( frame.State );
				}
				if ( frame.Count >= minimum && yielded.Add( frame.State ) ) {
					context.RegisterState();
					yield return frame.State;
				}
			}
		} finally {
			while ( 0 < stack.Count ) {
				stack.Pop().Children?.Dispose();
			}
		}
	}

	private sealed class RepeatTraversalFrame(
		RegexMatchState state,
		int count,
		bool addedToPath
	) {
		/// <summary>
		/// Gets the state value.
		/// </summary>
		internal RegexMatchState State { get; } = state;

		/// <summary>
		/// Gets the count value.
		/// </summary>
		internal int Count { get; } = count;

		/// <summary>
		/// Gets the added to path value.
		/// </summary>
		internal bool AddedToPath { get; } = addedToPath;

		/// <summary>
		/// Gets or sets the is initialized value.
		/// </summary>
		internal bool IsInitialized { get; set; }

		/// <summary>
		/// Gets or sets the children value.
		/// </summary>
		internal IEnumerator<RegexMatchState>? Children { get; set; }
	}
}
