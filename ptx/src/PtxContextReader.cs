namespace Icod.CoreUtils.Ptx;

using System.Runtime.CompilerServices;
using Icod.CoreUtils.Shared.Records;

/// <summary>Streams default GNU sentence contexts, traditional line contexts, or custom-regexp contexts.</summary>
internal static class PtxContextReader {
	private const int BufferSize = 65_536;

	/// <summary>Reads effective contexts from one source stream.</summary>
	/// <param name="source">The caller-owned input stream.</param>
	/// <param name="settings">The effective settings.</param>
	/// <param name="patterns">The compiled pattern collection.</param>
	/// <param name="statistics">The mutable file statistics.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An asynchronous context sequence.</returns>
	internal static IAsyncEnumerable<PtxContextSegment> ReadAsync(
		Stream source,
		PtxSettings settings,
		PtxPatterns patterns,
		PtxFileStatistics statistics,
		CancellationToken cancellationToken
	) {
		if ( settings.HasSentencePattern ) {
			return string.IsNullOrEmpty( settings.SentencePattern )
				? ReadWholeSourceAsync( source, statistics, cancellationToken )
				: ReadCustomAsync(
					source,
					patterns,
					settings.InputReference,
					statistics,
					cancellationToken
				);
		}
		if ( !settings.GnuExtensions || settings.InputReference ) {
			return ReadLinesAsync( source, statistics, cancellationToken );
		}
		return ReadDefaultSentencesAsync( source, statistics, cancellationToken );
	}

	private static async IAsyncEnumerable<PtxContextSegment> ReadLinesAsync(
		Stream source,
		PtxFileStatistics statistics,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		using var reader = new ByteRecordReader( source, RecordSeparator.LineFeed );
		long line = 1;
		long lineFeeds = 0;
		while ( true ) {
			var record = await reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}
			var content = TrimTrailingWhitespace( record.Content.Span );
			if ( !content.IsEmpty ) {
				yield return new PtxContextSegment( content.ToArray(), line, true );
			}
			if ( record.IsTerminated ) {
				lineFeeds++;
				line++;
			}
		}
		statistics.LineCount = checked( lineFeeds + 1 );
	}

	private static async IAsyncEnumerable<PtxContextSegment> ReadWholeSourceAsync(
		Stream source,
		PtxFileStatistics statistics,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		var bytes = await ReadAllAsync( source, cancellationToken ).ConfigureAwait( false );
		statistics.LineCount = checked( CountLineFeeds( bytes ) + 1 );
		var content = TrimTrailingWhitespace( bytes );
		if ( !content.IsEmpty ) {
			yield return new PtxContextSegment( content.ToArray(), 1, true );
		}
	}

	private static async IAsyncEnumerable<PtxContextSegment> ReadCustomAsync(
		Stream source,
		PtxPatterns patterns,
		bool inputReference,
		PtxFileStatistics statistics,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		var bytes = await ReadAllAsync( source, cancellationToken ).ConfigureAwait( false );
		statistics.LineCount = checked( CountLineFeeds( bytes ) + 1 );
		var text = PtxPatterns.DecodeForRegularExpression( bytes );
		var cursor = 0;
		var currentLineStart = 0;
		long line = 1;
		var atLineStart = true;
		while ( cursor < bytes.Length ) {
			cancellationToken.ThrowIfCancellationRequested();
			var separator = patterns.FindSentenceSeparator( text, cursor, cancellationToken );
			var next = null == separator
				? bytes.Length
				: checked( separator.Value.Start + separator.Value.Length );
			var end = TrimTrailingWhitespaceIndex( bytes.AsSpan(), cursor, next );
			if ( cursor < end ) {
				var inheritedInputReference = inputReference && !atLineStart
					? ReadInputReference( bytes, currentLineStart )
					: Array.Empty<byte>();
				yield return new PtxContextSegment(
					bytes.AsSpan( cursor, end - cursor ).ToArray(),
					line,
					atLineStart,
					inheritedInputReference
				);
			}
			UpdatePosition(
				bytes.AsSpan( cursor, next - cursor ),
				cursor,
				ref line,
				ref atLineStart,
				ref currentLineStart
			);
			cursor = next;
			if ( null == separator ) {
				break;
			}
		}
	}

	private static async IAsyncEnumerable<PtxContextSegment> ReadDefaultSentencesAsync(
		Stream source,
		PtxFileStatistics statistics,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		var pending = new List<byte>();
		var pendingStart = 0;
		var buffer = new byte[ BufferSize ];
		long line = 1;
		long totalLineFeeds = 0;
		var atLineStart = true;
		while ( true ) {
			var count = await source.ReadAsync( buffer, cancellationToken ).ConfigureAwait( false );
			if ( 0 == count ) {
				break;
			}
			for ( var index = 0; index < count; index++ ) {
				pending.Add( buffer[ index ] );
				if ( (byte)'\n' == buffer[ index ] ) {
					totalLineFeeds++;
				}
			}
			while ( TryFindBoundary(
				pending,
				pendingStart,
				endOfSource: false,
				out var contentEnd,
				out var consumeEnd
			) ) {
				var segmentLine = line;
				var segmentAtLineStart = atLineStart;
				if ( pendingStart < contentEnd ) {
					yield return new PtxContextSegment(
						pending.GetRange( pendingStart, contentEnd - pendingStart ).ToArray(),
						segmentLine,
						segmentAtLineStart
					);
				}
				UpdatePosition( pending, pendingStart, consumeEnd, ref line, ref atLineStart );
				pendingStart = consumeEnd;
			}
			CompactPending( pending, ref pendingStart );
		}
		while ( TryFindBoundary(
			pending,
			pendingStart,
			endOfSource: true,
			out var contentEnd,
			out var consumeEnd
		) ) {
			var segmentLine = line;
			var segmentAtLineStart = atLineStart;
			if ( pendingStart < contentEnd ) {
				yield return new PtxContextSegment(
					pending.GetRange( pendingStart, contentEnd - pendingStart ).ToArray(),
					segmentLine,
					segmentAtLineStart
				);
			}
			UpdatePosition( pending, pendingStart, consumeEnd, ref line, ref atLineStart );
			pendingStart = consumeEnd;
		}
		if ( pendingStart < pending.Count ) {
			var contentEnd = TrimTrailingWhitespaceIndex( pending, pendingStart, pending.Count );
			if ( pendingStart < contentEnd ) {
				yield return new PtxContextSegment(
					pending.GetRange( pendingStart, contentEnd - pendingStart ).ToArray(),
					line,
					atLineStart
				);
			}
		}
		statistics.LineCount = checked( totalLineFeeds + 1 );
	}

	private static bool TryFindBoundary(
		IReadOnlyList<byte> buffer,
		int startIndex,
		bool endOfSource,
		out int contentEnd,
		out int consumeEnd
	) {
		for ( var index = startIndex; index < buffer.Count; index++ ) {
			if ( buffer[ index ] is not (byte)'.' and not (byte)'?' and not (byte)'!' ) {
				continue;
			}
			var separatorStart = index + 1;
			while ( separatorStart < buffer.Count && IsClosingByte( buffer[ separatorStart ] ) ) {
				separatorStart++;
			}
			if ( separatorStart >= buffer.Count ) {
				if ( endOfSource ) {
					contentEnd = separatorStart;
					consumeEnd = separatorStart;
					return true;
				}
				continue;
			}
			var hasSeparator = (byte)'\t' == buffer[ separatorStart ]
				|| (
					(byte)' ' == buffer[ separatorStart ]
					&& separatorStart + 1 < buffer.Count
					&& (byte)' ' == buffer[ separatorStart + 1 ]
				);
			if ( !hasSeparator ) {
				continue;
			}
			var cursor = (byte)'\t' == buffer[ separatorStart ]
				? separatorStart + 1
				: separatorStart + 2;
			while ( cursor < buffer.Count && IsSentenceSuffixWhitespace( buffer[ cursor ] ) ) {
				cursor++;
			}
			if ( cursor == buffer.Count && !endOfSource ) {
				continue;
			}
			contentEnd = separatorStart;
			consumeEnd = cursor;
			return true;
		}
		contentEnd = 0;
		consumeEnd = 0;
		return false;
	}

	private static async Task<byte[]> ReadAllAsync(
		Stream source,
		CancellationToken cancellationToken
	) {
		using var destination = new MemoryStream();
		await source.CopyToAsync( destination, cancellationToken ).ConfigureAwait( false );
		return destination.ToArray();
	}

	private static long CountLineFeeds( ReadOnlySpan<byte> bytes ) {
		long count = 0;
		foreach ( var value in bytes ) {
			if ( (byte)'\n' == value ) {
				count++;
			}
		}
		return count;
	}

	private static ReadOnlySpan<byte> TrimTrailingWhitespace( ReadOnlySpan<byte> value ) {
		var end = value.Length;
		while ( 0 < end && PtxText.IsWhiteSpace( value[ end - 1 ] ) ) {
			end--;
		}
		return value[ ..end ];
	}

	private static int TrimTrailingWhitespaceIndex(
		IReadOnlyList<byte> value,
		int start,
		int end
	) {
		while ( start < end && PtxText.IsWhiteSpace( value[ end - 1 ] ) ) {
			end--;
		}
		return end;
	}

	private static int TrimTrailingWhitespaceIndex(
		ReadOnlySpan<byte> value,
		int start,
		int end
	) {
		while ( start < end && PtxText.IsWhiteSpace( value[ end - 1 ] ) ) {
			end--;
		}
		return end;
	}

	private static byte[] ReadInputReference( ReadOnlySpan<byte> source, int lineStart ) {
		var end = lineStart;
		while (
			end < source.Length
			&& (byte)'\n' != source[ end ]
			&& !PtxText.IsWhiteSpace( source[ end ] )
		) {
			end++;
		}
		return source[ lineStart..end ].ToArray();
	}

	private static void CompactPending( List<byte> pending, ref int pendingStart ) {
		if ( 0 == pendingStart || ( pendingStart < BufferSize && pendingStart < pending.Count ) ) {
			return;
		}
		pending.RemoveRange( 0, pendingStart );
		pendingStart = 0;
	}

	private static void UpdatePosition(
		IReadOnlyList<byte> consumed,
		int start,
		int end,
		ref long line,
		ref bool atLineStart
	) {
		for ( var index = start; index < end; index++ ) {
			if ( (byte)'\n' == consumed[ index ] ) {
				line++;
				atLineStart = true;
			} else {
				atLineStart = false;
			}
		}
	}

	private static void UpdatePosition(
		ReadOnlySpan<byte> consumed,
		int absoluteStart,
		ref long line,
		ref bool atLineStart,
		ref int currentLineStart
	) {
		for ( var index = 0; index < consumed.Length; index++ ) {
			if ( (byte)'\n' == consumed[ index ] ) {
				line++;
				atLineStart = true;
				currentLineStart = checked( absoluteStart + index + 1 );
			} else {
				atLineStart = false;
			}
		}
	}

	private static bool IsClosingByte( byte value ) => value is
		(byte)']' or (byte)'"' or (byte)'\'' or (byte)')' or (byte)'}';

	private static bool IsSentenceSuffixWhitespace( byte value ) => value is
		(byte)' ' or (byte)'\t' or (byte)'\n';
}

/// <summary>Provides shared byte classifications used by input processing and formatting.</summary>
internal static class PtxText {
	/// <summary>Determines whether a byte is one of the C-locale whitespace bytes used by GNU <c>ptx</c>.</summary>
	/// <param name="value">The byte.</param>
	/// <returns><see langword="true"/> for space, tab, line feed, vertical tab, form feed, or carriage return.</returns>
	internal static bool IsWhiteSpace( byte value ) => value is
		(byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\v' or (byte)'\f' or (byte)'\r';
}
