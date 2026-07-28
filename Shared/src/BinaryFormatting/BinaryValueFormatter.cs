namespace Icod.CoreUtils.Shared.BinaryFormatting;

using System.Buffers.Binary;
using System.Globalization;

/// <summary>
/// Formats primitive binary values using GNU <c>od</c>-style representations.
/// </summary>
public static class BinaryValueFormatter {
	private static readonly string[] ourControlNames = new string[] {
		"nul", "soh", "stx", "etx", "eot", "enq", "ack", "bel",
		"bs", "ht", "nl", "vt", "ff", "cr", "so", "si",
		"dle", "dc1", "dc2", "dc3", "dc4", "nak", "syn", "etb",
		"can", "em", "sub", "esc", "fs", "gs", "rs", "us"
	};

	/// <summary>
	/// Gets the fixed field width normally required by a format.
	/// </summary>
	public static int GetFieldWidth(
		BinaryFormatSpecification specification
	) {
		ArgumentNullException.ThrowIfNull( specification );
		return specification.Kind switch {
			BinaryFormatKind.NamedCharacter => 3,
			BinaryFormatKind.Character => 3,
			BinaryFormatKind.Octal => ( specification.Size * 8 + 2 ) / 3,
			BinaryFormatKind.Hexadecimal => specification.Size * 2,
			BinaryFormatKind.UnsignedDecimal => specification.Size switch {
				1 => 3,
				2 => 5,
				4 => 10,
				8 => 20,
				_ => 24
			},
			BinaryFormatKind.SignedDecimal => specification.Size switch {
				1 => 4,
				2 => 6,
				4 => 11,
				8 => 20,
				_ => 24
			},
			BinaryFormatKind.FloatingPoint => specification.Size switch {
				2 => 14,
				4 => 16,
				8 => 24,
				_ => 32
			},
			_ => 24
		};
	}

	/// <summary>
	/// Formats one value. Missing high-order bytes are treated as zero for a partial final unit.
	/// </summary>
	public static string Format(
		BinaryFormatSpecification specification,
		ReadOnlySpan<byte> value,
		BinaryByteOrder byteOrder
	) {
		ArgumentNullException.ThrowIfNull( specification );
		Span<byte> buffer = stackalloc byte[ 8 ];
		buffer.Clear();
		var copyCount = Math.Min( value.Length, specification.Size );
		value.Slice( 0, copyCount ).CopyTo( buffer );
		var source = buffer.Slice( 0, specification.Size );
		var littleEndian = BinaryByteOrder.Native == byteOrder
			? BitConverter.IsLittleEndian
			: BinaryByteOrder.LittleEndian == byteOrder
		;
		if ( littleEndian != BitConverter.IsLittleEndian && 1 < source.Length ) {
			source.Reverse();
		}

		return specification.Kind switch {
			BinaryFormatKind.NamedCharacter => FormatNamedCharacter( source[ 0 ] ),
			BinaryFormatKind.Character => FormatCharacter( source[ 0 ] ),
			BinaryFormatKind.SignedDecimal => FormatSigned( source, specification.Size ),
			BinaryFormatKind.UnsignedDecimal => FormatUnsigned( source, specification.Size, 10 ),
			BinaryFormatKind.Octal => FormatUnsigned( source, specification.Size, 8 ),
			BinaryFormatKind.Hexadecimal => FormatUnsigned( source, specification.Size, 16 ),
			BinaryFormatKind.FloatingPoint => FormatFloating( source, specification ),
			_ => throw new ArgumentOutOfRangeException( nameof( specification ) )
		};
	}

	private static string FormatNamedCharacter(
		byte value
	) {
		value &= 0x7f;
		if ( value < ourControlNames.Length ) {
			return ourControlNames[ value ];
		}
		if ( 0x20 == value ) {
			return " sp";
		}
		if ( 0x7f == value ) {
			return "del";
		}
		return string.Concat( "  ", ( char )value );
	}

	private static string FormatCharacter(
		byte value
	) {
		return value switch {
			0 => " \\0",
			7 => " \\a",
			8 => " \\b",
			9 => " \\t",
			10 => " \\n",
			11 => " \\v",
			12 => " \\f",
			13 => " \\r",
			_ when 0x20 <= value && 0x7e >= value => string.Concat( "  ", ( char )value ),
			_ => Convert.ToString( value, 8 ).PadLeft( 3, '0' )
		};
	}

	private static string FormatSigned(
		ReadOnlySpan<byte> value,
		int size
	) {
		long number = size switch {
			1 => unchecked( ( sbyte )value[ 0 ] ),
			2 => BitConverter.ToInt16( value ),
			4 => BitConverter.ToInt32( value ),
			8 => BitConverter.ToInt64( value ),
			_ => throw new ArgumentOutOfRangeException( nameof( size ) )
		};
		return number.ToString( CultureInfo.InvariantCulture );
	}

	private static string FormatUnsigned(
		ReadOnlySpan<byte> value,
		int size,
		int radix
	) {
		ulong number = size switch {
			1 => value[ 0 ],
			2 => BitConverter.ToUInt16( value ),
			4 => BitConverter.ToUInt32( value ),
			8 => BitConverter.ToUInt64( value ),
			_ => throw new ArgumentOutOfRangeException( nameof( size ) )
		};
		return radix switch {
			8 => Convert.ToString( unchecked( ( long )number ), 8 ),
			16 => number.ToString( "x", CultureInfo.InvariantCulture ),
			_ => number.ToString( CultureInfo.InvariantCulture )
		};
	}

	private static string FormatFloating(
		ReadOnlySpan<byte> value,
		BinaryFormatSpecification specification
	) {
		if ( 2 == specification.Size ) {
			var bits = BitConverter.ToUInt16( value );
			if ( 'B' == specification.FloatingAlias ) {
				var singleBits = unchecked( ( int )( ( uint )bits << 16 ) );
				return BitConverter.Int32BitsToSingle( singleBits ).ToString( "G9", CultureInfo.InvariantCulture );
			}
			return BitConverter.Int16BitsToHalf( unchecked( ( short )bits ) ).ToString( "G5", CultureInfo.InvariantCulture );
		}
		if ( 4 == specification.Size ) {
			return BitConverter.ToSingle( value ).ToString( "G9", CultureInfo.InvariantCulture );
		}
		if ( 8 == specification.Size ) {
			return BitConverter.ToDouble( value ).ToString( "G17", CultureInfo.InvariantCulture );
		}
		throw new NotSupportedException( "The requested floating-point representation is not supported." );
	}
}
