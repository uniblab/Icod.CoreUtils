namespace Icod.CoreUtils.Ptx;

using System.Text;
using Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Provides byte-oriented keyword and context matching over the Shared GNU Emacs regular-expression engine.</summary>
internal sealed class PtxPatterns {
	private static readonly Encoding Latin1 = Encoding.Latin1;
	private readonly bool[] wordMap;
	private readonly ICompiledRegularExpression? wordExpression;
	private readonly ICompiledRegularExpression? sentenceExpression;

	private PtxPatterns(
		bool[] wordMap,
		ICompiledRegularExpression? wordExpression,
		ICompiledRegularExpression? sentenceExpression
	) {
		this.wordMap = wordMap;
		this.wordExpression = wordExpression;
		this.sentenceExpression = sentenceExpression;
	}

	/// <summary>Creates the effective matchers after reading any break-character file.</summary>
	/// <param name="settings">The effective command settings.</param>
	/// <param name="wordMap">The effective 256-entry word-character map.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The compiled matcher collection.</returns>
	internal static async Task<PtxPatterns> CreateAsync(
		PtxSettings settings,
		bool[] wordMap,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( settings );
		ArgumentNullException.ThrowIfNull( wordMap );
		if ( 256 != wordMap.Length ) {
			throw new ArgumentException( "The word map must contain 256 entries.", nameof( wordMap ) );
		}
		var provider = new GnuEmacsRegularExpressionProvider(
			PosixCLocaleRegularExpressionCharacterClassProvider.Instance
		);
		var options = RegularExpressionOptions.GnuEmacsCompatibility with {
			IgnoreCase = settings.IgnoreCase,
			NewLineSensitive = false
		};
		ICompiledRegularExpression? word = null;
		if ( !string.IsNullOrEmpty( settings.WordPattern ) ) {
			word = await CompileAsync(
				provider,
				settings.WordPattern,
				options,
				cancellationToken
			).ConfigureAwait( false );
		}
		ICompiledRegularExpression? sentence = null;
		if ( settings.HasSentencePattern && !string.IsNullOrEmpty( settings.SentencePattern ) ) {
			sentence = await CompileAsync(
				provider,
				settings.SentencePattern,
				options,
				cancellationToken
			).ConfigureAwait( false );
		}
		return new PtxPatterns( (bool[])wordMap.Clone(), word, sentence );
	}

	/// <summary>Gets whether a custom sentence expression was compiled.</summary>
	internal bool HasSentenceExpression => null != this.sentenceExpression;
	/// <summary>Gets whether a custom word expression was compiled.</summary>
	internal bool HasWordExpression => null != this.wordExpression;

	/// <summary>Finds all positive-length words in one effective context.</summary>
	/// <param name="context">The byte context.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The word spans in source order.</returns>
	internal IReadOnlyList<PtxWordSpan> FindWords(
		ReadOnlyMemory<byte> context,
		CancellationToken cancellationToken
	) {
		var words = new List<PtxWordSpan>();
		if ( null == this.wordExpression ) {
			var span = context.Span;
			var cursor = 0;
			while ( cursor < span.Length ) {
				cancellationToken.ThrowIfCancellationRequested();
				while ( cursor < span.Length && !this.wordMap[ span[ cursor ] ] ) {
					cursor++;
				}
				var start = cursor;
				while ( cursor < span.Length && this.wordMap[ span[ cursor ] ] ) {
					cursor++;
				}
				if ( start < cursor ) {
					words.Add( new PtxWordSpan( start, cursor - start ) );
				}
			}
			return words;
		}
		var text = Latin1.GetString( context.Span );
		var index = 0;
		while ( index < text.Length ) {
			cancellationToken.ThrowIfCancellationRequested();
			var result = this.wordExpression.Match(
				text,
				new RegularExpressionMatchOptions { StartIndex = index },
				cancellationToken
			);
			EnsureMatchSucceeded( result );
			if ( !result.IsMatch || null == result.Match ) {
				break;
			}
			var match = result.Match;
			if ( 0 == match.Length ) {
				index = checked( match.Index + 1 );
				continue;
			}
			words.Add( new PtxWordSpan( match.Index, match.Length ) );
			index = checked( match.Index + match.Length );
		}
		return words;
	}

	/// <summary>Finds the next custom sentence separator in decoded one-to-one text.</summary>
	/// <param name="text">The complete source text.</param>
	/// <param name="startIndex">The search start.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The match span, or <see langword="null"/> when no separator remains.</returns>
	internal PtxWordSpan? FindSentenceSeparator(
		string text,
		int startIndex,
		CancellationToken cancellationToken
	) {
		if ( null == this.sentenceExpression ) {
			return null;
		}
		var result = this.sentenceExpression.Match(
			text,
			new RegularExpressionMatchOptions { StartIndex = startIndex },
			cancellationToken
		);
		EnsureMatchSucceeded( result );
		if ( !result.IsMatch || null == result.Match ) {
			return null;
		}
		if ( 0 == result.Match.Length ) {
			throw new InvalidDataException( string.Concat(
				"error: regular expression has a match of length zero: '",
				this.sentenceExpression.Pattern,
				"'"
			) );
		}
		return new PtxWordSpan( result.Match.Index, result.Match.Length );
	}

	/// <summary>Advances over one word match or one nonword byte, matching GNU field planning.</summary>
	/// <param name="context">The complete context.</param>
	/// <param name="index">The current byte index.</param>
	/// <param name="limit">The exclusive context limit.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An index strictly greater than <paramref name="index"/> unless it already equals the limit.</returns>
	internal int SkipSomething(
		ReadOnlyMemory<byte> context,
		int index,
		int limit,
		CancellationToken cancellationToken
	) {
		if ( index >= limit ) {
			return limit;
		}
		if ( null == this.wordExpression ) {
			if ( this.wordMap[ context.Span[ index ] ] ) {
				var cursor = index + 1;
				while ( cursor < limit && this.wordMap[ context.Span[ cursor ] ] ) {
					cursor++;
				}
				return cursor;
			}
			return index + 1;
		}
		var text = Latin1.GetString( context.Span[ ..limit ] );
		var result = this.wordExpression.Match(
			text,
			new RegularExpressionMatchOptions {
				StartIndex = index,
				RequireMatchAtStart = true
			},
			cancellationToken
		);
		EnsureMatchSucceeded( result );
		return result.IsMatch && null != result.Match && 0 < result.Match.Length
			? checked( index + result.Match.Length )
			: index + 1;
	}

	/// <summary>Decodes byte-oriented GNU input one-to-one for the managed regular-expression engine.</summary>
	/// <param name="value">The source bytes.</param>
	/// <returns>A Latin-1 string whose UTF-16 indices are identical to byte offsets.</returns>
	internal static string DecodeForRegularExpression( ReadOnlySpan<byte> value ) => Latin1.GetString( value );

	/// <summary>Compares byte words in GNU <c>ptx</c> order.</summary>
	/// <param name="left">The left word.</param>
	/// <param name="right">The right word.</param>
	/// <param name="ignoreCase">Whether ASCII case is folded.</param>
	/// <returns>A signed ordering result.</returns>
	internal static int CompareWords(
		ReadOnlySpan<byte> left,
		ReadOnlySpan<byte> right,
		bool ignoreCase
	) {
		var length = Math.Min( left.Length, right.Length );
		for ( var index = 0; index < length; index++ ) {
			var leftByte = ignoreCase ? FoldAscii( left[ index ] ) : left[ index ];
			var rightByte = ignoreCase ? FoldAscii( right[ index ] ) : right[ index ];
			var difference = leftByte - rightByte;
			if ( 0 != difference ) {
				return difference;
			}
		}
		return left.Length.CompareTo( right.Length );
	}

	private static async Task<ICompiledRegularExpression> CompileAsync(
		IRegularExpressionProvider provider,
		string pattern,
		RegularExpressionOptions options,
		CancellationToken cancellationToken
	) {
		var result = await provider.CompileAsync(
			pattern,
			options,
			cancellationToken
		).ConfigureAwait( false );
		if ( !result.IsSuccess || null == result.Expression ) {
			throw new ArgumentException( string.Concat(
				result.Diagnostic?.Message ?? "invalid regular expression",
				" (for regexp '", pattern, "')"
			) );
		}
		return result.Expression;
	}

	private static void EnsureMatchSucceeded( RegularExpressionMatchResult result ) {
		if ( !result.IsSuccess ) {
			throw new InvalidDataException(
				result.Diagnostic?.Message ?? "error in regular expression matcher"
			);
		}
	}

	private static byte FoldAscii( byte value ) => value is >= (byte)'a' and <= (byte)'z'
		? (byte)( value - ( (byte)'a' - (byte)'A' ) )
		: value;
}

/// <summary>Compares occurrences by keyword while the Shared external-ordering engine preserves ties.</summary>
internal sealed class PtxOccurrenceComparer : IComparer<PtxOccurrence> {
	private readonly bool ignoreCase;
	/// <summary>Initializes an occurrence comparer.</summary>
	/// <param name="ignoreCase">Whether ASCII case is folded.</param>
	internal PtxOccurrenceComparer( bool ignoreCase ) {
		this.ignoreCase = ignoreCase;
	}
	/// <inheritdoc/>
	public int Compare( PtxOccurrence? x, PtxOccurrence? y ) {
		if ( ReferenceEquals( x, y ) ) {
			return 0;
		}
		if ( null == x ) {
			return -1;
		}
		if ( null == y ) {
			return 1;
		}
		return PtxPatterns.CompareWords( x.Keyword, y.Keyword, this.ignoreCase );
	}
}

/// <summary>Performs binary-search membership checks for ignore and only tables.</summary>
internal sealed class PtxWordTable {
	private readonly bool ignoreCase;
	private readonly List<byte[]> words;
	/// <summary>Initializes a sorted word table.</summary>
	/// <param name="words">The owned word entries.</param>
	/// <param name="ignoreCase">Whether ASCII case is folded.</param>
	internal PtxWordTable( IEnumerable<byte[]> words, bool ignoreCase ) {
		ArgumentNullException.ThrowIfNull( words );
		this.ignoreCase = ignoreCase;
		this.words = words.Select( value => value.ToArray() ).ToList();
		this.words.Sort( ( left, right ) => PtxPatterns.CompareWords( left, right, ignoreCase ) );
	}
	/// <summary>Gets whether the table has no entries.</summary>
	internal bool IsEmpty => 0 == this.words.Count;
	/// <summary>Determines whether a word is in the table.</summary>
	/// <param name="word">The candidate word.</param>
	/// <returns><see langword="true"/> when an equal entry exists.</returns>
	internal bool Contains( ReadOnlySpan<byte> word ) {
		var low = 0;
		var high = this.words.Count;
		while ( low < high ) {
			var middle = low + ( ( high - low ) / 2 );
			var result = PtxPatterns.CompareWords( word, this.words[ middle ], this.ignoreCase );
			if ( 0 > result ) {
				high = middle;
			} else if ( 0 < result ) {
				low = middle + 1;
			} else {
				return true;
			}
		}
		return false;
	}
}
