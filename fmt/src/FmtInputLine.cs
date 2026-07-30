namespace Icod.CoreUtils.Fmt;

using System.Text;
using Icod.CoreUtils.Shared.Text;

/// <summary>Contains one byte-oriented input line analyzed for paragraph selection and formatting.</summary>
internal sealed class FmtInputLine {
	private static readonly byte[] ourGeneratedLineEnding = Encoding.UTF8.GetBytes( Environment.NewLine );
	private readonly bool myCopyEntered;
	private readonly bool myCopyHasLineEnding;
	private readonly int myCopyInputColumn;
	private readonly byte[]? myCopyPrefixBytes;
	private readonly byte[]? myCopyRemainderBytes;

	private FmtInputLine(
		TextLine source,
		bool isEligible,
		bool isBlank,
		bool isPrefixOnly,
		bool hasTab,
		int prefixColumn,
		int contentColumn,
		IReadOnlyList<FmtWord> words,
		bool copyEntered = false,
		int copyInputColumn = 0,
		byte[]? copyPrefixBytes = null,
		byte[]? copyRemainderBytes = null,
		bool copyHasLineEnding = false
	) {
		this.Source = source;
		this.IsEligible = isEligible;
		this.IsBlank = isBlank;
		this.IsPrefixOnly = isPrefixOnly;
		this.HasTab = hasTab;
		this.PrefixColumn = prefixColumn;
		this.ContentColumn = contentColumn;
		this.Words = words;
		this.myCopyEntered = copyEntered;
		this.myCopyInputColumn = copyInputColumn;
		this.myCopyPrefixBytes = copyPrefixBytes;
		this.myCopyRemainderBytes = copyRemainderBytes;
		this.myCopyHasLineEnding = copyHasLineEnding;
	}

	/// <summary>Gets the byte-column of the first word.</summary>
	internal int ContentColumn { get; }

	/// <summary>Gets whether GNU's blank scanner encountered an ASCII tab while examining the line.</summary>
	internal bool HasTab { get; }

	/// <summary>Gets whether the line contains only ASCII spaces and tabs.</summary>
	internal bool IsBlank { get; }

	/// <summary>Gets whether the line is eligible under the configured prefix.</summary>
	internal bool IsEligible { get; }

	/// <summary>Gets whether the line consists only of the required prefix and surrounding blanks.</summary>
	internal bool IsPrefixOnly { get; }

	/// <summary>Gets the byte-column at which the normalized prefix begins.</summary>
	internal int PrefixColumn { get; }

	/// <summary>Gets the byte-preserving source line.</summary>
	internal TextLine Source { get; }

	/// <summary>Gets the words found after indentation and any required prefix.</summary>
	internal IReadOnlyList<FmtWord> Words { get; }

	/// <summary>Analyzes one line using GNU's byte-oriented prefix and word-width rules.</summary>
	/// <param name="source">The source line.</param>
	/// <param name="prefix">The normalized prefix.</param>
	/// <param name="uniformSpacing">Whether all inter-word spacing is normalized.</param>
	/// <returns>The analyzed line.</returns>
	internal static FmtInputLine Analyze(
		TextLine source,
		FmtPrefix prefix,
		bool uniformSpacing
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( prefix );
		var bytes = source.ToByteArray( includeLineFeed: false );
		var index = 0;
		var column = 0;
		var hasTab = false;
		while ( index < bytes.Length && IsAsciiBlank( bytes[index] ) ) {
			hasTab |= (byte)'\t' == bytes[index];
			column = AdvanceBlank( column, bytes[index] );
			index++;
		}
		var leadingEnd = index;
		var prefixColumn = 0;
		var matchedPrefixLength = 0;
		if ( 0 == prefix.CoreBytes.Length ) {
			prefixColumn = Math.Min( prefix.LeadingSpaces, column );
		} else {
			prefixColumn = column;
			while (
				matchedPrefixLength < prefix.CoreBytes.Length
				&& index < bytes.Length
				&& bytes[index] == prefix.CoreBytes[matchedPrefixLength]
			) {
				matchedPrefixLength++;
				index++;
				column = checked(column + 1);
			}
			if ( matchedPrefixLength < prefix.CoreBytes.Length ) {
				return CreateCopiedLine(
					source,
					prefix,
					bytes,
					hasTab,
					prefixColumn,
					column,
					matchedPrefixLength,
					index,
					leadingEnd == bytes.Length,
					false
				);
			}
			while ( index < bytes.Length && IsAsciiBlank( bytes[index] ) ) {
				hasTab |= (byte)'\t' == bytes[index];
				column = AdvanceBlank( column, bytes[index] );
				index++;
			}
		}

		var eligible = prefixColumn >= prefix.LeadingSpaces
			&& column >= checked(prefixColumn + prefix.FullLength);
		if ( index == bytes.Length || !eligible ) {
			return CreateCopiedLine(
				source,
				prefix,
				bytes,
				hasTab,
				prefixColumn,
				column,
				matchedPrefixLength,
				index,
				leadingEnd == bytes.Length,
				index == bytes.Length && 0 < prefix.CoreBytes.Length && matchedPrefixLength == prefix.CoreBytes.Length
			);
		}

		var contentColumn = column;
		var words = new List<FmtWord>();
		while ( index < bytes.Length ) {
			var start = index++;
			while ( index < bytes.Length && !IsCWhitespace( bytes[index] ) ) {
				index++;
			}
			var wordBytes = bytes[start..index];
			column = checked(column + wordBytes.Length);
			var beforeSeparator = column;
			while ( index < bytes.Length && IsAsciiBlank( bytes[index] ) ) {
				hasTab |= (byte)'\t' == bytes[index];
				column = AdvanceBlank( column, bytes[index] );
				index++;
			}
			var separatorWidth = column - beforeSeparator;
			var endsPunctuation = EndsInSentencePunctuation( wordBytes );
			var endsSentence = (!source.HasLineFeed && index == bytes.Length)
				|| (endsPunctuation && (index == bytes.Length || 2 <= separatorWidth));
			var outputSpace = index == bytes.Length || uniformSpacing
				? (endsSentence ? 2 : 1)
				: separatorWidth;
			words.Add(
				new FmtWord(
					wordBytes,
					outputSpace,
					StartsWithOpenPunctuation( wordBytes ),
					EndsWithPunctuation( wordBytes ),
					endsPunctuation,
					endsSentence
				)
			);
		}
		return new FmtInputLine(
			source,
			true,
			false,
			false,
			hasTab,
			prefixColumn,
			contentColumn,
			words
		);
	}

	/// <summary>Writes a line copied by GNU's prefix and blank-line path.</summary>
	/// <param name="output">The destination stream.</param>
	/// <param name="useTabs">Whether equivalent tabs may be generated in normalized indentation.</param>
	/// <param name="cancellationToken">A token that can cancel asynchronous writes.</param>
	/// <returns>A value task that represents the asynchronous operation.</returns>
	internal async ValueTask WriteUnformattedAsync(
		Stream output,
		bool useTabs,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		if ( this.myCopyPrefixBytes is null || this.myCopyRemainderBytes is null ) {
			await this.Source.WriteAsync( output, cancellationToken: cancellationToken ).ConfigureAwait( false );
			return;
		}
		var column = 0;
		if ( this.myCopyEntered ) {
			column = await FmtSpacing.WriteToColumnAsync(
				output,
				useTabs,
				column,
				this.PrefixColumn,
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 < this.myCopyPrefixBytes.Length ) {
				await output.WriteAsync( this.myCopyPrefixBytes, cancellationToken ).ConfigureAwait( false );
				column = checked(column + this.myCopyPrefixBytes.Length);
			}
			if ( 0 < this.myCopyRemainderBytes.Length ) {
				_ = await FmtSpacing.WriteToColumnAsync(
					output,
					useTabs,
					column,
					this.myCopyInputColumn,
					cancellationToken
				).ConfigureAwait( false );
				await output.WriteAsync( this.myCopyRemainderBytes, cancellationToken ).ConfigureAwait( false );
			}
		}
		if ( this.myCopyHasLineEnding ) {
			await output.WriteAsync( ourGeneratedLineEnding, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static FmtInputLine CreateCopiedLine(
		TextLine source,
		FmtPrefix prefix,
		byte[] bytes,
		bool hasTab,
		int prefixColumn,
		int inputColumn,
		int matchedPrefixLength,
		int remainderIndex,
		bool isBlank,
		bool isPrefixOnly
	) {
		var hasRemainder = remainderIndex < bytes.Length;
		var entered = inputColumn > prefixColumn || hasRemainder;
		var prefixBytes = prefix.CoreBytes[..Math.Min( matchedPrefixLength, prefix.CoreBytes.Length )];
		var remainder = hasRemainder ? bytes[remainderIndex..] : Array.Empty<byte>();
		var appendLineEnding = source.HasLineFeed
			|| !hasRemainder
				&& entered
				&& inputColumn >= checked(prefixColumn + prefix.CoreBytes.Length);
		return new FmtInputLine(
			source,
			false,
			isBlank,
			isPrefixOnly,
			hasTab,
			prefixColumn,
			inputColumn,
			[ ],
			entered,
			inputColumn,
			prefixBytes,
			remainder,
			appendLineEnding
		);
	}

	private static int AdvanceBlank( int column, byte value ) {
		return (byte)'\t' == value
			? checked(column + (8 - (column % 8)))
			: checked(column + 1);
	}

	private static bool StartsWithOpenPunctuation( ReadOnlySpan<byte> bytes ) {
		return 0 < bytes.Length && bytes[0] is (byte)'(' or (byte)'[' or (byte)'\'' or (byte)'`' or (byte)'"';
	}

	private static bool EndsWithPunctuation( ReadOnlySpan<byte> bytes ) {
		if ( 0 == bytes.Length ) {
			return false;
		}
		var value = bytes[^1];
		return value is >= 0x21 and <= 0x2F
			or >= 0x3A and <= 0x40
			or >= 0x5B and <= 0x60
			or >= 0x7B and <= 0x7E;
	}

	private static bool EndsInSentencePunctuation( ReadOnlySpan<byte> bytes ) {
		var index = bytes.Length - 1;
		while ( 0 <= index && bytes[index] is (byte)')' or (byte)']' or (byte)'\'' or (byte)'"' ) {
			index--;
		}
		return 0 <= index && bytes[index] is (byte)'.' or (byte)'?' or (byte)'!';
	}

	private static bool IsAsciiBlank( byte value ) {
		return value is (byte)' ' or (byte)'\t';
	}

	private static bool IsCWhitespace( byte value ) {
		return value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\v' or (byte)'\f' or (byte)'\r';
	}
}
