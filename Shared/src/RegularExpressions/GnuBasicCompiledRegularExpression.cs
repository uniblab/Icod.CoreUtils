using System.Buffers;
using System.Globalization;
using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

internal sealed class GnuBasicCompiledRegularExpression : ICompiledRegularExpression {
	private readonly RegexNode expression;
	private readonly RegularExpressionOptions options;
	private readonly IRegularExpressionCharacterClassProvider characterClassProvider;

	internal GnuBasicCompiledRegularExpression(
		string pattern,
		RegexNode expression,
		int captureCount,
		RegularExpressionOptions options,
		IRegularExpressionCharacterClassProvider characterClassProvider
	) {
		Pattern = pattern;
		this.expression = expression;
		CaptureCount = captureCount;
		this.options = options;
		this.characterClassProvider = characterClassProvider;
	}

	/// <inheritdoc/>
	public string Pattern { get; }

	/// <inheritdoc/>
	public int CaptureCount { get; }

	/// <inheritdoc/>
	public RegularExpressionMatchResult Match(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( input );
		options ??= new RegularExpressionMatchOptions();
		cancellationToken.ThrowIfCancellationRequested();
		var decodedInput = RegexInput.Decode( input, cancellationToken );
		if ( !decodedInput.TryGetRuneIndex( options.StartIndex, out var firstStart ) ) {
			return RegularExpressionMatchResult.Failed(
				new(
					RegularExpressionDiagnosticCode.InvalidStartIndex,
					"start index is outside the input or splits a surrogate pair"
				)
			);
		}
		try {
			var context = new RegexMatchContext(
				decodedInput,
				this.options,
				characterClassProvider,
				cancellationToken
			);
			var finalStart = options.RequireMatchAtStart ? firstStart : decodedInput.Length;
			for ( var start = firstStart; finalStart >= start; start++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				RegexMatchState? best = null;
				var initial = new RegexMatchState( start, CaptureCount );
				foreach ( var candidate in expression.Match( context, initial ) ) {
					if ( null is best || candidate.Position > best.Position ) {
						best = candidate;
					}
				}
				if ( null is not best ) {
					return RegularExpressionMatchResult.Succeeded(
						CreatePublicMatch( decodedInput, start, best, cancellationToken )
					);
				}
			}
			return RegularExpressionMatchResult.Succeeded( null );
		} catch ( RegexMatchResourceLimitException ) {
			return RegularExpressionMatchResult.Failed(
				new(
					RegularExpressionDiagnosticCode.MatchResourceLimitExceeded,
					string.Concat(
						"regular-expression match exceeded the configured limit of ",
						this.options.MaximumMatchStates.ToString( CultureInfo.InvariantCulture ),
						" states"
					)
				)
			);
		}
	}

	/// <inheritdoc/>
	public ValueTask<RegularExpressionMatchResult> MatchAsync(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult( Match( input, options, cancellationToken ) );

	private RegularExpressionMatch CreatePublicMatch(
		RegexInput input,
		int start,
		RegexMatchState state,
		CancellationToken cancellationToken
	) {
		var captures = new RegularExpressionCapture[ CaptureCount ];
		for ( var captureIndex = 0; CaptureCount > captureIndex; captureIndex++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var capture = state.Captures[ captureIndex ];
			if ( null is capture ) {
				captures[ captureIndex ] = new( false, -1, 0, null );
				continue;
			}
			var utf16Start = input.GetUtf16Index( capture.Value.Start );
			var utf16End = input.GetUtf16Index( capture.Value.End );
			captures[ captureIndex ] = new(
				true,
				utf16Start,
				utf16End - utf16Start,
				input.Source[ utf16Start..utf16End ]
			);
		}
		var matchStart = input.GetUtf16Index( start );
		var matchEnd = input.GetUtf16Index( state.Position );
		return new(
			matchStart,
			matchEnd - matchStart,
			input.Source[ matchStart..matchEnd ],
			captures
		);
	}

}

internal readonly record struct RegexCaptureSpan( int Start, int End );

internal sealed class RegexMatchState {
	internal RegexMatchState( int position, int captureCount ) {
		Position = position;
		Captures = new RegexCaptureSpan?[ captureCount ];
	}

	private RegexMatchState( int position, RegexCaptureSpan?[] captures ) {
		Position = position;
		Captures = captures;
	}

	internal int Position { get; }

	internal RegexCaptureSpan?[] Captures { get; }

	internal RegexMatchState WithPosition( int position ) => new( position, Captures );

	internal RegexMatchState WithCapture( int captureNumber, RegexCaptureSpan capture ) {
		var captures = (RegexCaptureSpan?[])Captures.Clone();
		captures[ captureNumber - 1 ] = capture;
		return new( Position, captures );
	}
}

internal sealed class RegexMatchStateComparer : IEqualityComparer<RegexMatchState> {
	internal static RegexMatchStateComparer Instance { get; } = new();

	private RegexMatchStateComparer() {
	}

	/// <inheritdoc/>
	public bool Equals( RegexMatchState? left, RegexMatchState? right ) {
		if ( ReferenceEquals( left, right ) ) {
			return true;
		}
		if ( null is left || null is right || left.Position != right.Position ) {
			return false;
		}
		if ( left.Captures.Length != right.Captures.Length ) {
			return false;
		}
		for ( var index = 0; left.Captures.Length > index; index++ ) {
			if ( left.Captures[ index ] != right.Captures[ index ] ) {
				return false;
			}
		}
		return true;
	}

	/// <inheritdoc/>
	public int GetHashCode( RegexMatchState value ) {
		var hash = new HashCode();
		hash.Add( value.Position );
		foreach ( var capture in value.Captures ) {
			hash.Add( capture );
		}
		return hash.ToHashCode();
	}
}

internal sealed class RegexMatchContext {
	private long stateCount;

	internal RegexMatchContext(
		RegexInput input,
		RegularExpressionOptions options,
		IRegularExpressionCharacterClassProvider characterClassProvider,
		CancellationToken cancellationToken
	) {
		Input = input;
		Options = options;
		CharacterClassProvider = characterClassProvider;
		CancellationToken = cancellationToken;
	}

	internal RegexInput Input { get; }

	internal RegularExpressionOptions Options { get; }

	internal IRegularExpressionCharacterClassProvider CharacterClassProvider { get; }

	internal CancellationToken CancellationToken { get; }

	internal void RegisterState() {
		CancellationToken.ThrowIfCancellationRequested();
		stateCount++;
		if ( Options.MaximumMatchStates < stateCount ) {
			throw new RegexMatchResourceLimitException();
		}
	}
}

internal sealed class RegexInput {
	private readonly Rune[] runes;
	private readonly int[] utf16Indices;

	private RegexInput( string source, Rune[] runes, int[] utf16Indices ) {
		Source = source;
		this.runes = runes;
		this.utf16Indices = utf16Indices;
	}

	internal string Source { get; }

	internal int Length => runes.Length;

	internal Rune this[ int index ] => runes[ index ];

	internal int GetUtf16Index( int runeIndex ) => utf16Indices[ runeIndex ];

	internal bool TryGetRuneIndex( int utf16Index, out int runeIndex ) {
		if ( 0 > utf16Index || Source.Length < utf16Index ) {
			runeIndex = -1;
			return false;
		}
		var result = Array.BinarySearch( utf16Indices, utf16Index );
		if ( 0 > result ) {
			runeIndex = -1;
			return false;
		}
		runeIndex = result;
		return true;
	}

	internal static RegexInput Decode( string source, CancellationToken cancellationToken ) {
		var runes = new List<Rune>( source.Length );
		var indices = new List<int>( source.Length + 1 );
		var utf16Index = 0;
		while ( source.Length > utf16Index ) {
			cancellationToken.ThrowIfCancellationRequested();
			indices.Add( utf16Index );
			var status = Rune.DecodeFromUtf16(
				source.AsSpan( utf16Index ),
				out var value,
				out var consumed
			);
			if ( OperationStatus.Done != status ) {
				value = Rune.ReplacementChar;
				consumed = 1;
			}
			runes.Add( value );
			utf16Index += consumed;
		}
		indices.Add( source.Length );
		return new( source, [ .. runes ], [ .. indices ] );
	}
}

internal sealed class RegexMatchResourceLimitException : Exception {
}
