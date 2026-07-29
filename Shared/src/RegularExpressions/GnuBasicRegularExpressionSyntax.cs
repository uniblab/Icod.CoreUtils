using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

internal sealed record GnuBasicParseResult(
	RegexNode? Expression,
	int CaptureCount,
	RegularExpressionDiagnostic? Diagnostic
);

internal abstract class RegexNode {
	internal abstract IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state );
}

internal sealed class EmptyRegexNode : RegexNode {
	internal static EmptyRegexNode Instance { get; } = new();

	private EmptyRegexNode() {
	}

	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.RegisterState();
		yield return state;
	}
}

internal sealed class LiteralRegexNode( Rune value ) : RegexNode {
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if (
			context.Input.Length > state.Position
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

internal sealed class DotRegexNode : RegexNode {
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( context.Input.Length <= state.Position ) {
			yield break;
		}
		var value = context.Input[ state.Position ];
		if ( 0 == value.Value || ( context.Options.NewLineSensitive && '\n' == value.Value ) ) {
			yield break;
		}
		context.RegisterState();
		yield return state.WithPosition( state.Position + 1 );
	}
}

internal enum RegexAssertionKind {
	BeginLine,
	EndLine,
	BeginInput,
	EndInput,
	WordBoundary,
	NotWordBoundary,
	BeginWord,
	EndWord
}

internal sealed class AssertionRegexNode( RegexAssertionKind kind ) : RegexNode {
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		var previousIsWord = 0 < state.Position
			&& context.CharacterClassProvider.IsWordCharacter( context.Input[ state.Position - 1 ] );
		var currentIsWord = context.Input.Length > state.Position
			&& context.CharacterClassProvider.IsWordCharacter( context.Input[ state.Position ] );
		var matches = kind switch {
			RegexAssertionKind.BeginLine => 0 == state.Position
				|| ( context.Options.NewLineSensitive && '\n' == context.Input[ state.Position - 1 ].Value ),
			RegexAssertionKind.EndLine => context.Input.Length == state.Position
				|| ( context.Options.NewLineSensitive && '\n' == context.Input[ state.Position ].Value ),
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

internal abstract class BracketExpressionTerm {
	internal abstract bool Matches( RegexMatchContext context, Rune value );
}

internal sealed class BracketLiteralTerm( Rune literal ) : BracketExpressionTerm {
	internal Rune Literal { get; } = literal;

	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		context.CharacterClassProvider.AreCharactersEqual( Literal, value, context.Options.IgnoreCase );
}

internal sealed class BracketRangeTerm( Rune start, Rune end ) : BracketExpressionTerm {
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		0 >= context.CharacterClassProvider.Compare( start, value, context.Options.IgnoreCase )
		&& 0 <= context.CharacterClassProvider.Compare( end, value, context.Options.IgnoreCase );
}

internal sealed class BracketCharacterClassTerm( string className ) : BracketExpressionTerm {
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		context.CharacterClassProvider.IsCharacterClass( value, className, context.Options.IgnoreCase );
}

internal sealed class BracketEquivalenceTerm( Rune equivalent ) : BracketExpressionTerm {
	internal override bool Matches( RegexMatchContext context, Rune value ) =>
		context.CharacterClassProvider.AreCollatingElementsEquivalent(
			equivalent,
			value,
			context.Options.IgnoreCase
		);
}

internal sealed class BracketNeverTerm : BracketExpressionTerm {
	internal static BracketNeverTerm Instance { get; } = new();

	private BracketNeverTerm() {
	}

	internal override bool Matches( RegexMatchContext context, Rune value ) => false;
}

internal sealed class CharacterClassRegexNode(
	string className,
	bool isNegated
) : RegexNode {
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( context.Input.Length <= state.Position ) {
			yield break;
		}
		var value = context.Input[ state.Position ];
		var matches = "word" == className
			? context.CharacterClassProvider.IsWordCharacter( value )
			: context.CharacterClassProvider.IsCharacterClass(
				value,
				className,
				context.Options.IgnoreCase
			);
		if ( isNegated ) {
			matches = !matches;
		}
		if ( matches ) {
			context.RegisterState();
			yield return state.WithPosition( state.Position + 1 );
		}
	}
}

internal sealed class BracketRegexNode(
	IReadOnlyList<BracketExpressionTerm> terms,
	bool isNegated
) : RegexNode {
	internal override IEnumerable<RegexMatchState> Match( RegexMatchContext context, RegexMatchState state ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		if ( context.Input.Length <= state.Position ) {
			yield break;
		}
		var value = context.Input[ state.Position ];
		var any = false;
		foreach ( var term in terms ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			if ( term.Matches( context, value ) ) {
				any = true;
				break;
			}
		}
		var matches = isNegated ? !any : any;
		if ( isNegated && context.Options.NewLineSensitive && '\n' == value.Value ) {
			matches = false;
		}
		if ( matches ) {
			context.RegisterState();
			yield return state.WithPosition( state.Position + 1 );
		}
	}
}

internal sealed class SequenceRegexNode : RegexNode {
	private readonly IReadOnlyList<RegexNode> nodes;

	internal SequenceRegexNode( IReadOnlyList<RegexNode> nodes ) {
		this.nodes = nodes;
	}

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

internal sealed class AlternationRegexNode : RegexNode {
	private readonly IReadOnlyList<RegexNode> alternatives;

	internal AlternationRegexNode( IReadOnlyList<RegexNode> alternatives ) {
		this.alternatives = alternatives;
	}

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

internal sealed class GroupRegexNode( int captureNumber, RegexNode expression ) : RegexNode {
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

internal sealed class BackReferenceRegexNode( int captureNumber ) : RegexNode {
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
			if (
				!context.CharacterClassProvider.AreCharactersEqual(
					context.Input[ captureSpan.Start + offset ],
					context.Input[ state.Position + offset ],
					context.Options.IgnoreCase
				)
			) {
				yield break;
			}
		}
		context.RegisterState();
		yield return state.WithPosition( state.Position + length );
	}
}

internal sealed class RepeatRegexNode : RegexNode {
	private readonly RegexNode expression;
	private readonly int minimum;
	private readonly int? maximum;

	internal RepeatRegexNode( RegexNode expression, int minimum, int? maximum ) {
		this.expression = expression;
		this.minimum = minimum;
		this.maximum = maximum;
	}

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
		internal RegexMatchState State { get; } = state;

		internal int Count { get; } = count;

		internal bool AddedToPath { get; } = addedToPath;

		internal bool IsInitialized { get; set; }

		internal IEnumerator<RegexMatchState>? Children { get; set; }
	}
}
