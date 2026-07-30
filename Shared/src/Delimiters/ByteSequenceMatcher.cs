namespace Icod.CoreUtils.Shared.Delimiters;

/// <summary>Matches a nonempty byte delimiter incrementally across arbitrary input-buffer boundaries.</summary>
public sealed class ByteSequenceMatcher {

	private readonly int[] myPrefixLengths;
	private readonly byte[] myPattern;
	private int myMatchedLength;

	/// <summary>Initializes an incremental matcher.</summary>
	/// <param name="delimiter">The immutable nonempty byte delimiter.</param>
	public ByteSequenceMatcher( ByteDelimiter delimiter ) {
		ArgumentNullException.ThrowIfNull( delimiter );
		this.myPattern = delimiter.Bytes.ToArray();
		this.myPrefixLengths = BuildPrefixLengths( this.myPattern );
	}

	/// <summary>Gets the number of leading delimiter bytes matched by the current suffix.</summary>
	public int MatchedLength => this.myMatchedLength;

	/// <summary>Consumes one byte and reports whether a complete delimiter ended at that byte.</summary>
	/// <param name="value">The next input byte.</param>
	/// <returns><see langword="true"/> when a full delimiter has just matched.</returns>
	public bool Advance( byte value ) {
		while ( 0 < this.myMatchedLength && value != this.myPattern[this.myMatchedLength] ) {
			this.myMatchedLength = this.myPrefixLengths[this.myMatchedLength - 1];
		}
		if ( value == this.myPattern[this.myMatchedLength] ) {
			this.myMatchedLength++;
		}
		if ( this.myPattern.Length != this.myMatchedLength ) {
			return false;
		}
		this.myMatchedLength = this.myPrefixLengths[this.myMatchedLength - 1];
		return true;
	}

	/// <summary>Discards any partial match.</summary>
	public void Reset() {
		this.myMatchedLength = 0;
	}

	private static int[] BuildPrefixLengths( byte[] pattern ) {
		var result = new int[pattern.Length];
		var matched = 0;
		for ( var index = 1; index < pattern.Length; index++ ) {
			while ( 0 < matched && pattern[index] != pattern[matched] ) {
				matched = result[matched - 1];
			}
			if ( pattern[index] == pattern[matched] ) {
				matched++;
			}
			result[index] = matched;
		}
		return result;
	}

}
