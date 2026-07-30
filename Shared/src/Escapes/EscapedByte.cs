namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Represents one parsed byte and whether a backslash escape produced it.</summary>
public readonly struct EscapedByte {

	/// <summary>Initializes an escaped-byte result.</summary>
	/// <param name="value">The resulting byte.</param>
	/// <param name="wasEscaped">Whether an escape introduced the byte.</param>
	/// <param name="sourceOffset">The zero-based UTF-16 source offset of the byte or its backslash.</param>
	public EscapedByte(
		byte value,
		bool wasEscaped,
		int sourceOffset
	) {
		if ( sourceOffset < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( sourceOffset ) );
		}
		this.Value = value;
		this.WasEscaped = wasEscaped;
		this.SourceOffset = sourceOffset;
	}

	/// <summary>Gets the resulting byte.</summary>
	public byte Value { get; }

	/// <summary>Gets whether an escape introduced the byte.</summary>
	public bool WasEscaped { get; }

	/// <summary>Gets the zero-based UTF-16 source offset of the byte or its backslash.</summary>
	public int SourceOffset { get; }

}
