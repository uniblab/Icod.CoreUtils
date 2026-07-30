namespace Icod.CoreUtils.Fmt;

using System.Text;

/// <summary>Optimizes and writes one GNU <c>fmt</c> paragraph.</summary>
internal sealed class ParagraphFormatter {
	private const long LineCost = 4900;
	private const long NoBreakCost = 360000;
	private const long OrphanNumerator = 22500;
	private const long ParenthesisBonus = 1600;
	private const long PunctuationBonus = 1600;
	private const long SentenceBonus = 2500;
	private const long WidowNumerator = 40000;
	private readonly FmtOptions myOptions;
	private readonly byte[] myNewLine = Encoding.UTF8.GetBytes( Environment.NewLine );

	/// <summary>Initializes a paragraph optimizer.</summary>
	/// <param name="options">The validated command options.</param>
	internal ParagraphFormatter( FmtOptions options ) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
	}

	/// <summary>Optimizes and writes a paragraph.</summary>
	/// <param name="lines">The source lines forming the paragraph.</param>
	/// <param name="otherIndent">The indentation for the second and later output lines.</param>
	/// <param name="useTabs">Whether generated indentation may use tabs.</param>
	/// <param name="output">The destination stream.</param>
	/// <param name="cancellationToken">A token that can cancel asynchronous writes.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	internal async Task FormatAsync(
		IReadOnlyList<FmtInputLine> lines,
		int otherIndent,
		bool useTabs,
		Stream output,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( lines );
		ArgumentNullException.ThrowIfNull( output );
		if ( 0 == lines.Count ) {
			return;
		}
		var words = lines.SelectMany( line => line.Words ).ToArray();
		if ( 0 == words.Length ) {
			await lines[0].WriteUnformattedAsync( output, useTabs, cancellationToken ).ConfigureAwait( false );
			return;
		}
		var nextBreak = this.BuildBreaks( words, lines[0].ContentColumn, otherIndent );
		var start = 0;
		var firstOutputLine = true;
		while ( start < words.Length ) {
			var end = nextBreak[start];
			var indent = firstOutputLine ? lines[0].ContentColumn : otherIndent;
			await this.WriteLineAsync(
				words,
				start,
				end,
				lines[0].PrefixColumn,
				indent,
				useTabs,
				output,
				cancellationToken
			).ConfigureAwait( false );
			start = end;
			firstOutputLine = false;
		}
	}

	private int[] BuildBreaks( IReadOnlyList<FmtWord> words, int firstIndent, int otherIndent ) {
		var count = words.Count;
		var nextBreak = new int[count + 1];
		var lineLength = new int[count + 1];
		var bestCost = new long[count + 1];
		nextBreak[count] = count;
		for ( var start = count - 1; 0 <= start; start-- ) {
			var best = long.MaxValue;
			var bestNext = start + 1;
			var bestLength = checked((0 == start ? firstIndent : otherIndent) + words[start].Length);
			var length = bestLength;
			var next = start + 1;
			while ( true ) {
				var candidate = AddCost(
					AddCost( this.LineDependentCost( next, length, count, nextBreak, lineLength ), bestCost[next] ),
					BaseCost( start, words )
				);
				if ( candidate < best ) {
					best = candidate;
					bestNext = next;
					bestLength = length;
				}
				if ( next == count ) {
					break;
				}
				length = checked(length + words[next - 1].SpaceAfter + words[next].Length);
				next++;
				if ( this.myOptions.MaximumWidth < length ) {
					break;
				}
			}
			nextBreak[start] = bestNext;
			lineLength[start] = bestLength;
			bestCost[start] = best;
		}
		return nextBreak;
	}

	private static long AddCost( long left, long right ) {
		if ( 0 < right && long.MaxValue - right < left ) {
			return long.MaxValue;
		}
		if ( right < 0 && left < long.MinValue - right ) {
			return long.MinValue;
		}
		return left + right;
	}

	private static long BaseCost( int start, IReadOnlyList<FmtWord> words ) {
		var cost = LineCost;
		if ( 0 < start ) {
			var previous = words[start - 1];
			if ( previous.EndsPunctuation ) {
				cost += previous.EndsSentence ? -SentenceBonus : NoBreakCost;
			} else if ( previous.EndsWithPunctuation ) {
				cost -= PunctuationBonus;
			} else if ( 1 < start && IsFinal( start - 2, words ) ) {
				cost += WidowNumerator / checked(words[start - 1].Length + 2L);
			}
		}
		if ( words[start].StartsWithOpenPunctuation ) {
			cost -= ParenthesisBonus;
		} else if ( IsFinal( start, words ) ) {
			cost += OrphanNumerator / checked(words[start].Length + 2L);
		}
		return cost;
	}

	private long LineDependentCost(
		int next,
		int length,
		int wordCount,
		IReadOnlyList<int> nextBreak,
		IReadOnlyList<int> lineLength
	) {
		if ( next == wordCount ) {
			return 0;
		}
		var goalDifference = (long)this.myOptions.GoalWidth - length;
		var cost = checked(100L * goalDifference * goalDifference);
		if ( nextBreak[next] != wordCount ) {
			var raggedDifference = (long)length - lineLength[next];
			cost = AddCost( cost, checked(50L * raggedDifference * raggedDifference) );
		}
		return cost;
	}

	private static bool IsFinal( int index, IReadOnlyList<FmtWord> words ) {
		return index == words.Count - 1 || words[index].EndsSentence;
	}

	private async Task WriteLineAsync(
		IReadOnlyList<FmtWord> words,
		int start,
		int end,
		int prefixColumn,
		int indent,
		bool useTabs,
		Stream output,
		CancellationToken cancellationToken
	) {
		var column = 0;
		column = await FmtSpacing.WriteToColumnAsync( output, useTabs, column, prefixColumn, cancellationToken ).ConfigureAwait( false );
		if ( 0 < this.myOptions.Prefix.CoreBytes.Length ) {
			await output.WriteAsync( this.myOptions.Prefix.CoreBytes, cancellationToken ).ConfigureAwait( false );
			column = checked(column + this.myOptions.Prefix.CoreBytes.Length);
		}
		column = await FmtSpacing.WriteToColumnAsync( output, useTabs, column, indent, cancellationToken ).ConfigureAwait( false );
		for ( var index = start; index < end; index++ ) {
			await output.WriteAsync( words[index].Bytes, cancellationToken ).ConfigureAwait( false );
			column = checked(column + words[index].Length);
			if ( index + 1 < end ) {
				column = await FmtSpacing.WriteToColumnAsync(
					output,
					useTabs,
					column,
					checked(column + words[index].SpaceAfter),
					cancellationToken
				).ConfigureAwait( false );
			}
		}
		await output.WriteAsync( this.myNewLine, cancellationToken ).ConfigureAwait( false );
	}

}
