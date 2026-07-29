namespace Icod.CoreUtils.Shared.Text;

using System.Text;

/// <summary>
/// Represents one byte-oriented or decoded text unit while retaining the exact source bytes that formed it.
/// </summary>
public readonly struct TextUnit {
	private readonly uint myPackedBytes;
	private readonly Rune myScalar;

	private TextUnit(
		TextUnitKind kind,
		Rune scalar,
		uint packedBytes,
		int byteCount
	) {
		this.Kind = kind;
		this.myScalar = scalar;
		this.myPackedBytes = packedBytes;
		this.ByteCount = byteCount;
	}

	/// <summary>Gets the number of exact source bytes retained by this unit.</summary>
	public int ByteCount {
		get;
	}

	/// <summary>Gets the representation kind of this unit.</summary>
	public TextUnitKind Kind {
		get;
	}

	/// <summary>
	/// Gets the decoded scalar for <see cref="TextUnitKind.Scalar"/> and
	/// <see cref="TextUnitKind.Replacement"/> units; otherwise, <see langword="null"/>.
	/// </summary>
	public Rune? Scalar => this.Kind is TextUnitKind.Scalar or TextUnitKind.Replacement
		? this.myScalar
		: null;

	/// <summary>Copies the exact source bytes retained by this unit into a destination span.</summary>
	/// <param name="destination">The destination span.</param>
	/// <returns>The number of bytes copied.</returns>
	/// <exception cref="ArgumentException">The destination is shorter than <see cref="ByteCount"/>.</exception>
	public int CopyBytesTo( Span<byte> destination ) {
		if ( destination.Length < this.ByteCount ) {
			throw new ArgumentException(
				"The destination is too short for this text unit.",
				nameof( destination )
			);
		}
		for ( var index = 0; index < this.ByteCount; index++ ) {
			destination[index] = this.GetByte( index );
		}
		return this.ByteCount;
	}

	/// <summary>Gets one retained source byte by zero-based index.</summary>
	/// <param name="index">The zero-based byte index.</param>
	/// <returns>The retained source byte.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside this unit.</exception>
	public byte GetByte( int index ) {
		if ( (index < 0) || (index >= this.ByteCount) ) {
			throw new ArgumentOutOfRangeException( nameof( index ) );
		}
		return (byte)(this.myPackedBytes >> (index * 8));
	}

	/// <summary>Returns a new array containing the exact source bytes retained by this unit.</summary>
	/// <returns>The retained source bytes.</returns>
	public byte[] ToByteArray() {
		var value = new byte[this.ByteCount];
		this.CopyBytesTo( value );
		return value;
	}

	/// <summary>Creates an opaque one-byte unit.</summary>
	/// <param name="value">The source byte.</param>
	/// <returns>The byte unit.</returns>
	internal static TextUnit CreateByte( byte value ) => new(
		TextUnitKind.Byte,
		default,
		value,
		1
	);

	/// <summary>Creates a unit for one invalid source byte.</summary>
	/// <param name="value">The invalid source byte.</param>
	/// <returns>The invalid-byte unit.</returns>
	internal static TextUnit CreateInvalidByte( byte value ) => new(
		TextUnitKind.InvalidByte,
		default,
		value,
		1
	);

	/// <summary>Creates a replacement unit for one invalid source byte.</summary>
	/// <param name="value">The invalid source byte.</param>
	/// <returns>The replacement unit.</returns>
	internal static TextUnit CreateReplacement( byte value ) => new(
		TextUnitKind.Replacement,
		Rune.ReplacementChar,
		value,
		1
	);

	/// <summary>Creates a decoded scalar unit from its exact UTF-8 source bytes.</summary>
	/// <param name="scalar">The decoded scalar.</param>
	/// <param name="bytes">The exact UTF-8 bytes that formed the scalar.</param>
	/// <returns>The scalar unit.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The byte span does not contain between one and four bytes.</exception>
	internal static TextUnit CreateScalar( Rune scalar, ReadOnlySpan<byte> bytes ) {
		if ( (bytes.Length < 1) || (bytes.Length > 4) ) {
			throw new ArgumentOutOfRangeException( nameof( bytes ) );
		}
		uint packed = 0;
		for ( var index = 0; index < bytes.Length; index++ ) {
			packed |= (uint)bytes[index] << (index * 8);
		}
		return new(
			TextUnitKind.Scalar,
			scalar,
			packed,
			bytes.Length
		);
	}
}
