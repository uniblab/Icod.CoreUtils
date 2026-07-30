namespace Icod.CoreUtils.Fmt;

/// <summary>Represents one byte-preserving word and its paragraph-breaking metadata.</summary>
internal sealed class FmtWord {
	/// <summary>Initializes a paragraph word.</summary>
	/// <param name="bytes">The exact source bytes.</param>
	/// <param name="spaceAfter">The normalized or retained column space following the word.</param>
	/// <param name="startsWithOpenPunctuation">Whether the word begins with opening punctuation.</param>
	/// <param name="endsWithPunctuation">Whether the word ends with punctuation.</param>
	/// <param name="endsPunctuation">Whether the word ends in sentence punctuation.</param>
	/// <param name="endsSentence">Whether the source spacing marks a sentence end.</param>
	internal FmtWord(
		byte[] bytes,
		int spaceAfter,
		bool startsWithOpenPunctuation,
		bool endsWithPunctuation,
		bool endsPunctuation,
		bool endsSentence
	) {
		this.Bytes = bytes ?? throw new ArgumentNullException( nameof( bytes ) );
		this.SpaceAfter = spaceAfter;
		this.StartsWithOpenPunctuation = startsWithOpenPunctuation;
		this.EndsWithPunctuation = endsWithPunctuation;
		this.EndsPunctuation = endsPunctuation;
		this.EndsSentence = endsSentence;
	}

	/// <summary>Gets the exact source bytes of the word.</summary>
	internal byte[] Bytes { get; }

	/// <summary>Gets whether the word ends in sentence punctuation.</summary>
	internal bool EndsPunctuation { get; }

	/// <summary>Gets whether the word ends with punctuation.</summary>
	internal bool EndsWithPunctuation { get; }

	/// <summary>Gets whether the original input marks the word as ending a sentence.</summary>
	internal bool EndsSentence { get; }

	/// <summary>Gets the source-byte length used by GNU <c>fmt</c> as the word width.</summary>
	internal int Length => this.Bytes.Length;

	/// <summary>Gets the output column space following the word when it is not a line break.</summary>
	internal int SpaceAfter { get; }

	/// <summary>Gets whether the word begins with opening punctuation.</summary>
	internal bool StartsWithOpenPunctuation { get; }
}
