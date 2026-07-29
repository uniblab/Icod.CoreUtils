namespace Icod.CoreUtils.Shared.Formatting;

using System.Globalization;
using System.Text;

/// <summary>Describes the result of decoding GNU command-line escape sequences.</summary>
/// <param name="Text">The text value.</param>
/// <param name="StopOutput">The stop output value.</param>
public sealed record GnuEscapeDecodeResult(
	string Text,
	bool StopOutput
);

/// <summary>Decodes the escape language shared by GNU-style formatting commands.</summary>
public static class GnuEscapeDecoder {
	/// <summary>Decodes backslash escapes in <paramref name="value"/>.</summary>
	/// <param name="value">Text containing escapes.</param>
	/// <param name="allowBareOctal">Whether <c>\NNN</c> is accepted in addition to <c>\0NNN</c>.</param>
	/// <param name="allowStopOutput">Whether <c>\c</c> terminates output.</param>
	/// <exception cref="FormatException">An escape is malformed or denotes an invalid Unicode scalar.</exception>
	public static GnuEscapeDecodeResult Decode(
		string value,
		bool allowBareOctal = true,
		bool allowStopOutput = true
	) {
		ArgumentNullException.ThrowIfNull( value );
		var output = new StringBuilder( value.Length );
		for ( var index = 0; index < value.Length; index++ ) {
			var current = value[ index ];
			if ( '\\' != current ) {
				output.Append( current );
				continue;
			}
			if ( ++index >= value.Length ) {
				output.Append( '\\' );
				break;
			}
			current = value[ index ];
			switch ( current ) {
				case '"': output.Append( '"' ); break;
				case '\\': output.Append( '\\' ); break;
				case 'a': output.Append( '\a' ); break;
				case 'b': output.Append( '\b' ); break;
				case 'c':
					if ( allowStopOutput ) {
						return new GnuEscapeDecodeResult( output.ToString(), true );
					}
					output.Append( 'c' );
					break;
				case 'e': output.Append( '\u001b' ); break;
				case 'f': output.Append( '\f' ); break;
				case 'n': output.Append( '\n' ); break;
				case 'r': output.Append( '\r' ); break;
				case 't': output.Append( '\t' ); break;
				case 'v': output.Append( '\v' ); break;
				case 'x':
					AppendHex( value, ref index, 2, output, "hexadecimal" );
					break;
				case 'u':
					AppendUnicode( value, ref index, 4, output );
					break;
				case 'U':
					AppendUnicode( value, ref index, 8, output );
					break;
				case '0':
					AppendOctal( value, ref index, 3, output, includeCurrent: false );
					break;
				default:
					if ( allowBareOctal && IsOctal( current ) ) {
						AppendOctal( value, ref index, 3, output, includeCurrent: true );
					} else {
						output.Append( '\\' );
						output.Append( current );
					}
					break;
			}
		}
		return new GnuEscapeDecodeResult( output.ToString(), false );
	}

	private static void AppendHex(
		string value,
		ref int index,
		int maximumDigits,
		StringBuilder output,
		string description
	) {
		var start = index + 1;
		var count = 0;
		var scalar = 0;
		while ( count < maximumDigits && start + count < value.Length ) {
			var digit = HexValue( value[ start + count ] );
			if ( 0 > digit ) {
				break;
			}
			scalar = checked( scalar * 16 + digit );
			count++;
		}
		if ( 0 == count ) {
			throw new FormatException( string.Concat( "missing ", description, " number in escape" ) );
		}
		index += count;
		output.Append( (char)scalar );
	}

	private static void AppendUnicode(
		string value,
		ref int index,
		int digits,
		StringBuilder output
	) {
		var start = index + 1;
		if ( start + digits > value.Length ) {
			throw new FormatException( "missing hexadecimal Unicode value in escape" );
		}
		var scalar = 0;
		for ( var offset = 0; offset < digits; offset++ ) {
			var digit = HexValue( value[ start + offset ] );
			if ( 0 > digit ) {
				throw new FormatException( "invalid hexadecimal Unicode value in escape" );
			}
			scalar = checked( scalar * 16 + digit );
		}
		if ( !Rune.IsValid( scalar ) ) {
			throw new FormatException(
				string.Concat(
					"invalid Unicode character U+",
					scalar.ToString( "X", CultureInfo.InvariantCulture )
				)
			);
		}
		index += digits;
		output.Append( new Rune( scalar ).ToString() );
	}

	private static void AppendOctal(
		string value,
		ref int index,
		int maximumDigits,
		StringBuilder output,
		bool includeCurrent
	) {
		var scalar = 0;
		var count = 0;
		var cursor = includeCurrent ? index : index + 1;
		while ( count < maximumDigits && cursor < value.Length && IsOctal( value[ cursor ] ) ) {
			scalar = checked( scalar * 8 + ( value[ cursor ] - '0' ) );
			count++;
			cursor++;
		}
		if ( 0 == count ) {
			output.Append( '\0' );
			return;
		}
		index = cursor - 1;
		output.Append( (char)( scalar & 0xff ) );
	}

	private static bool IsOctal( char value ) {
		return '0' <= value && '7' >= value;
	}

	private static int HexValue( char value ) {
		if ( '0' <= value && '9' >= value ) {
			return value - '0';
		}
		if ( 'a' <= value && 'f' >= value ) {
			return value - 'a' + 10;
		}
		if ( 'A' <= value && 'F' >= value ) {
			return value - 'A' + 10;
		}
		return -1;
	}
}
