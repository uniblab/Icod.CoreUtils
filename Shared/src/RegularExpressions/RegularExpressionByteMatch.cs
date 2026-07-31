namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents a leftmost-longest match over authoritative source bytes.</summary>
public sealed class RegularExpressionByteMatch {
	/// <summary>Initializes a selected byte-preserving regular-expression match.</summary>
	/// <param name="byteIndex">The zero-based source-byte offset.</param>
	/// <param name="byteLength">The source-byte match length.</param>
	/// <param name="value">The exact matched source bytes.</param>
	/// <param name="captures">The numbered subexpression captures.</param>
	public RegularExpressionByteMatch(
		int byteIndex,
		int byteLength,
		ReadOnlyMemory<byte> value,
		IReadOnlyList<RegularExpressionByteCapture> captures
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( byteIndex );
		ArgumentOutOfRangeException.ThrowIfNegative( byteLength );
		ArgumentNullException.ThrowIfNull( captures );
		ByteIndex = byteIndex;
		ByteLength = byteLength;
		Value = value;
		Captures = Array.AsReadOnly( captures.ToArray() );
	}

	/// <summary>Gets the zero-based source-byte offset.</summary>
	public int ByteIndex { get; }

	/// <summary>Gets the source-byte match length.</summary>
	public int ByteLength { get; }

	/// <summary>Gets the exact matched source bytes.</summary>
	public ReadOnlyMemory<byte> Value { get; }

	/// <summary>Gets numbered captures in opening-parenthesis order; element zero represents subexpression 1.</summary>
	public IReadOnlyList<RegularExpressionByteCapture> Captures { get; }
}
