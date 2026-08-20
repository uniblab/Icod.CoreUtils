using System.Buffers;
using System.Globalization;
using System.Text;
using Icod.CommandFramework.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>
/// Provides the shared managed GNU compiled regular-expression implementation.
/// </summary>
internal sealed class GnuBasicCompiledRegularExpression : ICompiledRegularExpression {
	private readonly RegexNode expression;
	private readonly RegularExpressionOptions options;
	private readonly IRegularExpressionCharacterClassProvider characterClassProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="GnuBasicCompiledRegularExpression"/> class.
	/// </summary>
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
		if ( !decodedInput.TryGetUnitIndex( options.StartIndex, out var firstStart ) ) {
			return RegularExpressionMatchResult.Failed(
				new(
					RegularExpressionDiagnosticCode.InvalidStartIndex,
					"start index is outside the input or splits a surrogate pair"
				)
			);
		}
		var search = Search(
			decodedInput,
			firstStart,
			options.RequireMatchAtStart,
			cancellationToken
		);
		if ( search.Diagnostic is RegularExpressionDiagnostic diagnostic ) {
			return RegularExpressionMatchResult.Failed( diagnostic );
		}
		return RegularExpressionMatchResult.Succeeded(
			search.State is null
				? null
				: CreatePublicTextMatch(
					decodedInput,
					search.Start,
					search.State,
					cancellationToken
				)
		);
	}

	/// <inheritdoc/>
	public ValueTask<RegularExpressionMatchResult> MatchAsync(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult( Match( input, options, cancellationToken ) );

	/// <inheritdoc/>
	public RegularExpressionByteMatchResult Match(
		ReadOnlyMemory<byte> input,
		RegularExpressionInputOptions? inputOptions = null,
		RegularExpressionByteMatchOptions? matchOptions = null,
		CancellationToken cancellationToken = default
	) {
		inputOptions ??= new RegularExpressionInputOptions();
		matchOptions ??= new RegularExpressionByteMatchOptions();
		if ( !Enum.IsDefined( inputOptions.DecodingMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( inputOptions ) );
		}
		if ( !Enum.IsDefined( inputOptions.InvalidEncodingPolicy ) ) {
			throw new ArgumentOutOfRangeException( nameof( inputOptions ) );
		}
		cancellationToken.ThrowIfCancellationRequested();
		var decodedInput = RegexInput.Decode(
			input,
			inputOptions,
			cancellationToken
		);
		if ( !decodedInput.TryGetUnitIndex( matchOptions.StartByteOffset, out var firstStart ) ) {
			return RegularExpressionByteMatchResult.Failed(
				new(
					RegularExpressionDiagnosticCode.InvalidStartByteOffset,
					"start byte offset is outside the input or splits a decoded UTF-8 unit"
				)
			);
		}
		var search = Search(
			decodedInput,
			firstStart,
			matchOptions.RequireMatchAtStart,
			cancellationToken
		);
		if ( search.Diagnostic is RegularExpressionDiagnostic diagnostic ) {
			return RegularExpressionByteMatchResult.Failed( diagnostic );
		}
		return RegularExpressionByteMatchResult.Succeeded(
			search.State is null
				? null
				: CreatePublicByteMatch(
					decodedInput,
					search.Start,
					search.State,
					cancellationToken
				)
		);
	}

	/// <inheritdoc/>
	public ValueTask<RegularExpressionByteMatchResult> MatchAsync(
		ReadOnlyMemory<byte> input,
		RegularExpressionInputOptions? inputOptions = null,
		RegularExpressionByteMatchOptions? matchOptions = null,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult( Match( input, inputOptions, matchOptions, cancellationToken ) );

	private RegexSearchResult Search(
		RegexInput input,
		int firstStart,
		bool requireMatchAtStart,
		CancellationToken cancellationToken
	) {
		try {
			var context = new RegexMatchContext(
				input,
				this.options,
				characterClassProvider,
				cancellationToken
			);
			var finalStart = requireMatchAtStart ? firstStart : input.Length;
			for ( var start = firstStart; finalStart >= start; start++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				RegexMatchState? best = null;
				var initial = new RegexMatchState( start, CaptureCount );
				foreach ( var candidate in expression.Match( context, initial ) ) {
					if ( best is null || candidate.Position > best.Position ) {
						best = candidate;
					}
				}
				if ( best is RegexMatchState selected ) {
					return new( start, selected, null );
				}
			}
			return new( -1, null, null );
		} catch ( RegexMatchResourceLimitException ) {
			return new(
				-1,
				null,
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

	private RegularExpressionMatch CreatePublicTextMatch(
		RegexInput input,
		int start,
		RegexMatchState state,
		CancellationToken cancellationToken
	) {
		var source = input.TextSource!;
		var captures = new RegularExpressionCapture[ CaptureCount ];
		for ( var captureIndex = 0; CaptureCount > captureIndex; captureIndex++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var capture = state.Captures[ captureIndex ];
			if ( capture is not RegexCaptureSpan captureSpan ) {
				captures[ captureIndex ] = new( false, -1, 0, null );
				continue;
			}
			var captureStart = input.GetSourceIndex( captureSpan.Start );
			var captureEnd = input.GetSourceIndex( captureSpan.End );
			captures[ captureIndex ] = new(
				true,
				captureStart,
				captureEnd - captureStart,
				source[ captureStart..captureEnd ]
			);
		}
		var matchStart = input.GetSourceIndex( start );
		var matchEnd = input.GetSourceIndex( state.Position );
		return new(
			matchStart,
			matchEnd - matchStart,
			source[ matchStart..matchEnd ],
			captures
		);
	}

	private RegularExpressionByteMatch CreatePublicByteMatch(
		RegexInput input,
		int start,
		RegexMatchState state,
		CancellationToken cancellationToken
	) {
		var source = input.ByteSource;
		var captures = new RegularExpressionByteCapture[ CaptureCount ];
		for ( var captureIndex = 0; CaptureCount > captureIndex; captureIndex++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var capture = state.Captures[ captureIndex ];
			if ( capture is not RegexCaptureSpan captureSpan ) {
				captures[ captureIndex ] = new( false, -1, 0, ReadOnlyMemory<byte>.Empty );
				continue;
			}
			var captureStart = input.GetSourceIndex( captureSpan.Start );
			var captureEnd = input.GetSourceIndex( captureSpan.End );
			captures[ captureIndex ] = new(
				true,
				captureStart,
				captureEnd - captureStart,
				source.Slice( captureStart, captureEnd - captureStart )
			);
		}
		var matchStart = input.GetSourceIndex( start );
		var matchEnd = input.GetSourceIndex( state.Position );
		return new(
			matchStart,
			matchEnd - matchStart,
			source.Slice( matchStart, matchEnd - matchStart ),
			captures
		);
	}

	private readonly record struct RegexSearchResult(
		int Start,
		RegexMatchState? State,
		RegularExpressionDiagnostic? Diagnostic
	);
}

/// <summary>
/// Represents regex capture span.
/// </summary>
/// <param name="Start">The start value.</param>
/// <param name="End">The end value.</param>
internal readonly record struct RegexCaptureSpan( int Start, int End );

/// <summary>
/// Provides the regex match state implementation.
/// </summary>
internal sealed class RegexMatchState {
	/// <summary>
	/// Initializes a new instance of the RegexMatchState class.
	/// </summary>
	internal RegexMatchState( int position, int captureCount ) {
		Position = position;
		Captures = new RegexCaptureSpan?[ captureCount ];
	}

	private RegexMatchState( int position, RegexCaptureSpan?[] captures ) {
		Position = position;
		Captures = captures;
	}

	/// <summary>
	/// Gets the position value.
	/// </summary>
	internal int Position { get; }

	/// <summary>
	/// Gets the captures value.
	/// </summary>
	internal RegexCaptureSpan?[] Captures { get; }

	/// <summary>
	/// Creates a copy with position.
	/// </summary>
	internal RegexMatchState WithPosition( int position ) => new( position, Captures );

	/// <summary>
	/// Creates a copy with capture.
	/// </summary>
	internal RegexMatchState WithCapture( int captureNumber, RegexCaptureSpan capture ) {
		var captures = (RegexCaptureSpan?[])Captures.Clone();
		captures[ captureNumber - 1 ] = capture;
		return new( Position, captures );
	}
}

/// <summary>
/// Provides the regex match state comparer implementation.
/// </summary>
internal sealed class RegexMatchStateComparer : IEqualityComparer<RegexMatchState> {
	/// <summary>
	/// Performs the new operation.
	/// </summary>
	internal static RegexMatchStateComparer Instance { get; } = new();

	private RegexMatchStateComparer() {
	}

	/// <inheritdoc/>
	public bool Equals( RegexMatchState? left, RegexMatchState? right ) {
		if ( ReferenceEquals( left, right ) ) {
			return true;
		}
		if ( left is null || right is null ) {
			return false;
		}
		if ( left.Position != right.Position ) {
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

/// <summary>
/// Provides the regex match context implementation.
/// </summary>
internal sealed class RegexMatchContext {
	private long stateCount;

	/// <summary>
	/// Initializes a new instance of the RegexMatchContext class.
	/// </summary>
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

	/// <summary>
	/// Gets the input value.
	/// </summary>
	internal RegexInput Input { get; }

	/// <summary>
	/// Gets the options value.
	/// </summary>
	internal RegularExpressionOptions Options { get; }

	/// <summary>
	/// Gets the character class provider value.
	/// </summary>
	internal IRegularExpressionCharacterClassProvider CharacterClassProvider { get; }

	/// <summary>
	/// Gets the cancellation token value.
	/// </summary>
	internal CancellationToken CancellationToken { get; }

	/// <summary>
	/// Performs the register state operation.
	/// </summary>
	internal void RegisterState() {
		CancellationToken.ThrowIfCancellationRequested();
		stateCount++;
		if ( Options.MaximumMatchStates < stateCount ) {
			throw new RegexMatchResourceLimitException();
		}
	}
}

/// <summary>
/// Provides decoded matching units and an exact mapping back to the authoritative source representation.
/// </summary>
internal sealed class RegexInput {
	private const int PreservedInvalidByteRuneBase = 0xF0000;
	private readonly Rune[] runes;
	private readonly bool[] opaqueUnits;
	private readonly int[] sourceIndices;

	private RegexInput(
		string? textSource,
		ReadOnlyMemory<byte> byteSource,
		Rune[] runes,
		bool[] opaqueUnits,
		int[] sourceIndices
	) {
		TextSource = textSource;
		ByteSource = byteSource;
		this.runes = runes;
		this.opaqueUnits = opaqueUnits;
		this.sourceIndices = sourceIndices;
	}

	/// <summary>Gets the authoritative string source for string matching.</summary>
	internal string? TextSource { get; }

	/// <summary>Gets the authoritative byte source for byte-preserving matching.</summary>
	internal ReadOnlyMemory<byte> ByteSource { get; }

	/// <summary>Gets the decoded matching-unit count.</summary>
	internal int Length => runes.Length;

	/// <summary>Gets one decoded matching unit.</summary>
	internal Rune this[ int index ] => runes[ index ];

	/// <summary>Gets whether one unit represents an opaque malformed source byte.</summary>
	internal bool IsOpaque( int index ) => opaqueUnits[ index ];

	/// <summary>Gets the source UTF-16 index or byte offset for a matching-unit boundary.</summary>
	internal int GetSourceIndex( int unitIndex ) => sourceIndices[ unitIndex ];

	/// <summary>Attempts to map an exact source boundary to a matching-unit index.</summary>
	internal bool TryGetUnitIndex( int sourceIndex, out int unitIndex ) {
		if ( 0 > sourceIndex || sourceIndices[ ^1 ] < sourceIndex ) {
			unitIndex = -1;
			return false;
		}
		var result = Array.BinarySearch( sourceIndices, sourceIndex );
		if ( 0 > result ) {
			unitIndex = -1;
			return false;
		}
		unitIndex = result;
		return true;
	}

	/// <summary>Decodes a string into Unicode-scalar matching units while retaining UTF-16 boundaries.</summary>
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
		return new(
			source,
			ReadOnlyMemory<byte>.Empty,
			[ .. runes ],
			new bool[ runes.Count ],
			[ .. indices ]
		);
	}

	/// <summary>Decodes authoritative bytes according to the explicit byte/text policy.</summary>
	internal static RegexInput Decode(
		ReadOnlyMemory<byte> source,
		RegularExpressionInputOptions options,
		CancellationToken cancellationToken
	) {
		var span = source.Span;
		var runes = new List<Rune>( span.Length );
		var opaqueUnits = new List<bool>( span.Length );
		var indices = new List<int>( span.Length + 1 );
		var byteIndex = 0;
		while ( span.Length > byteIndex ) {
			cancellationToken.ThrowIfCancellationRequested();
			indices.Add( byteIndex );
			if ( TextDecodingMode.Bytes == options.DecodingMode ) {
				runes.Add( new Rune( span[ byteIndex ] ) );
				opaqueUnits.Add( false );
				byteIndex++;
				continue;
			}
			var status = Rune.DecodeFromUtf8(
				span[ byteIndex.. ],
				out var value,
				out var consumed
			);
			if ( OperationStatus.Done == status ) {
				runes.Add( value );
				opaqueUnits.Add( false );
				byteIndex += consumed;
				continue;
			}
			var invalidByte = span[ byteIndex ];
			switch ( options.InvalidEncodingPolicy ) {
				case InvalidEncodingPolicy.PreserveBytes:
					runes.Add( new Rune( PreservedInvalidByteRuneBase + invalidByte ) );
					opaqueUnits.Add( true );
					byteIndex++;
					break;
				case InvalidEncodingPolicy.Replace:
					runes.Add( Rune.ReplacementChar );
					opaqueUnits.Add( false );
					byteIndex++;
					break;
				case InvalidEncodingPolicy.Throw:
					throw new DecoderFallbackException(
						string.Concat(
							"Invalid UTF-8 input at byte offset ",
							byteIndex.ToString( CultureInfo.InvariantCulture ),
							"."
						)
					);
				default:
					throw new InvalidOperationException( "Unknown invalid-encoding policy." );
			}
		}
		indices.Add( span.Length );
		return new( null, source, [ .. runes ], [ .. opaqueUnits ], [ .. indices ] );
	}
}

/// <summary>
/// Provides the regex match resource limit exception implementation.
/// </summary>
internal sealed class RegexMatchResourceLimitException : Exception {
}
