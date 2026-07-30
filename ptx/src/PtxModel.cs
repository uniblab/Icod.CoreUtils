namespace Icod.CoreUtils.Ptx;

/// <summary>Identifies a supported GNU <c>ptx</c> output representation.</summary>
internal enum PtxOutputFormat {
	/// <summary>Selects the aligned plain-text representation.</summary>
	Dumb,
	/// <summary>Selects roff macro invocations.</summary>
	Roff,
	/// <summary>Selects TeX macro invocations.</summary>
	Tex
}

/// <summary>Contains the effective command-line policy for one <c>ptx</c> execution.</summary>
internal sealed class PtxSettings {
	/// <summary>Gets or sets whether GNU extensions are enabled.</summary>
	internal bool GnuExtensions { get; set; } = true;
	/// <summary>Gets or sets whether automatic file-and-line references are generated.</summary>
	internal bool AutoReference { get; set; }
	/// <summary>Gets or sets whether the first field of each input line is a reference.</summary>
	internal bool InputReference { get; set; }
	/// <summary>Gets or sets whether references are emitted on the right.</summary>
	internal bool RightReference { get; set; }
	/// <summary>Gets or sets whether keyword comparison ignores ASCII case.</summary>
	internal bool IgnoreCase { get; set; }
	/// <summary>Gets or sets the break-character file.</summary>
	internal string? BreakFile { get; set; }
	/// <summary>Gets or sets the ignore-word file.</summary>
	internal string? IgnoreFile { get; set; }
	/// <summary>Gets or sets the only-word file.</summary>
	internal string? OnlyFile { get; set; }
	/// <summary>Gets or sets the truncation marker bytes.</summary>
	internal byte[] TruncationString { get; set; } = [ (byte)'/' ];
	/// <summary>Gets or sets the inter-field gap.</summary>
	internal int GapSize { get; set; } = 3;
	/// <summary>Gets or sets the total requested line width.</summary>
	internal int LineWidth { get; set; } = 72;
	/// <summary>Gets or sets the roff or TeX macro name.</summary>
	internal string MacroName { get; set; } = "xx";
	/// <summary>Gets or sets the output representation.</summary>
	internal PtxOutputFormat OutputFormat { get; set; } = PtxOutputFormat.Dumb;
	/// <summary>Gets or sets an explicitly supplied sentence regular expression.</summary>
	internal string? SentencePattern { get; set; }
	/// <summary>Gets or sets whether a sentence regular expression was explicitly supplied.</summary>
	internal bool HasSentencePattern { get; set; }
	/// <summary>Gets or sets an explicitly supplied word regular expression.</summary>
	internal string? WordPattern { get; set; }
	/// <summary>Gets the input operands in encounter order.</summary>
	internal List<string> InputFiles { get; } = new();
	/// <summary>Gets or sets the traditional-mode output pathname.</summary>
	internal string? OutputFile { get; set; }
	/// <summary>Gets or sets the approximate in-memory external-ordering limit.</summary>
	internal long MemoryLimitBytes { get; set; } = 32L * 1024L * 1024L;
}

/// <summary>Describes one context selected from an input source.</summary>
internal sealed class PtxContextSegment {
	/// <summary>Initializes a context segment.</summary>
	/// <param name="content">The context bytes, excluding trailing separator whitespace.</param>
	/// <param name="startingLineNumber">The one-based line containing the first context byte.</param>
	/// <param name="startsAtLineStart">Whether the context begins at the start of a physical line.</param>
	internal PtxContextSegment(
		byte[] content,
		long startingLineNumber,
		bool startsAtLineStart
	) {
		this.Content = content;
		this.StartingLineNumber = startingLineNumber;
		this.StartsAtLineStart = startsAtLineStart;
	}
	/// <summary>Gets the context bytes, excluding trailing separator whitespace.</summary>
	internal byte[] Content { get; }
	/// <summary>Gets the one-based line containing the first context byte.</summary>
	internal long StartingLineNumber { get; }
	/// <summary>Gets whether the context begins at the start of a physical line.</summary>
	internal bool StartsAtLineStart { get; }
}

/// <summary>Describes one word span inside a context.</summary>
internal readonly struct PtxWordSpan {
	/// <summary>Initializes a word span.</summary>
	/// <param name="start">The zero-based byte offset.</param>
	/// <param name="length">The positive byte length.</param>
	internal PtxWordSpan( int start, int length ) {
		this.Start = start;
		this.Length = length;
	}
	/// <summary>Gets the zero-based byte offset.</summary>
	internal int Start { get; }
	/// <summary>Gets the positive byte length.</summary>
	internal int Length { get; }
}

/// <summary>Stores one sortable keyword occurrence and a reference to its spooled context.</summary>
internal sealed class PtxOccurrence {
	/// <summary>Initializes an occurrence.</summary>
	/// <param name="keyword">The copied keyword bytes used for ordering.</param>
	/// <param name="contextOffset">The context offset in the context spool.</param>
	/// <param name="contextLength">The context byte length.</param>
	/// <param name="keywordStart">The keyword offset inside the context.</param>
	/// <param name="keywordLength">The keyword byte length.</param>
	/// <param name="reference">The optional reference bytes.</param>
	internal PtxOccurrence(
		byte[] keyword,
		long contextOffset,
		int contextLength,
		int keywordStart,
		int keywordLength,
		byte[] reference
	) {
		this.Keyword = keyword;
		this.ContextOffset = contextOffset;
		this.ContextLength = contextLength;
		this.KeywordStart = keywordStart;
		this.KeywordLength = keywordLength;
		this.Reference = reference;
	}
	/// <summary>Gets the copied keyword bytes used for ordering.</summary>
	internal byte[] Keyword { get; }
	/// <summary>Gets the context offset in the context spool.</summary>
	internal long ContextOffset { get; }
	/// <summary>Gets the context byte length.</summary>
	internal int ContextLength { get; }
	/// <summary>Gets the keyword offset inside the context.</summary>
	internal int KeywordStart { get; }
	/// <summary>Gets the keyword byte length.</summary>
	internal int KeywordLength { get; }
	/// <summary>Gets the optional reference bytes.</summary>
	internal byte[] Reference { get; }
}

/// <summary>Tracks source-wide values required after occurrence discovery.</summary>
internal sealed class PtxProcessingState {
	/// <summary>Gets or sets the largest recognized word length, including rejected words.</summary>
	internal int MaximumWordLength { get; set; }
	/// <summary>Gets or sets the maximum input-reference width.</summary>
	internal int InputReferenceMaximumWidth { get; set; }
	/// <summary>Gets per-file statistics in input order.</summary>
	internal List<PtxFileStatistics> Files { get; } = new();
}

/// <summary>Tracks final line counts and automatic-reference names for one input.</summary>
internal sealed class PtxFileStatistics {
	/// <summary>Initializes file statistics.</summary>
	/// <param name="referenceName">The filename portion used by automatic references.</param>
	internal PtxFileStatistics( string referenceName ) {
		this.ReferenceName = referenceName;
	}
	/// <summary>Gets the automatic-reference filename, or an empty string for standard input.</summary>
	internal string ReferenceName { get; }
	/// <summary>Gets or sets the GNU-compatible physical-line count.</summary>
	internal long LineCount { get; set; } = 1;
}
