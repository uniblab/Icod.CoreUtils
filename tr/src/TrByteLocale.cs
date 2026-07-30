namespace Icod.CoreUtils.Tr;

using System.Globalization;

/// <summary>Provides deterministic byte classification for the active character locale.</summary>
internal sealed class TrByteLocale {
	private readonly CultureInfo? myCulture;

	/// <summary>Initializes a byte-character locale.</summary>
	/// <param name="culture">The managed single-byte compatibility culture, or <see langword="null"/> for C/UTF-8 byte rules.</param>
	public TrByteLocale( CultureInfo? culture ) {
		this.myCulture = culture;
	}

	/// <summary>Resolves <c>LC_CTYPE</c> using POSIX environment precedence.</summary>
	/// <returns>The byte-character locale.</returns>
	public static TrByteLocale Resolve() {
		var name = Environment.GetEnvironmentVariable( "LC_ALL" );
		if ( string.IsNullOrEmpty( name ) ) {
			name = Environment.GetEnvironmentVariable( "LC_CTYPE" );
		}
		if ( string.IsNullOrEmpty( name ) ) {
			name = Environment.GetEnvironmentVariable( "LANG" );
		}
		if ( string.IsNullOrWhiteSpace( name )
			|| name.Equals( "C", StringComparison.OrdinalIgnoreCase )
			|| name.Equals( "POSIX", StringComparison.OrdinalIgnoreCase )
			|| name.Contains( "UTF-8", StringComparison.OrdinalIgnoreCase )
			|| name.Contains( "UTF8", StringComparison.OrdinalIgnoreCase ) ) {
			return new TrByteLocale( null );
		}
		var normalized = name.Split( '.', '@' )[0].Replace( '_', '-' );
		try {
			return new TrByteLocale( CultureInfo.GetCultureInfo( normalized ) );
		} catch ( CultureNotFoundException ) {
			throw new InvalidOperationException( string.Concat( "invalid LC_CTYPE locale: '", name, "'" ) );
		}
	}

	/// <summary>Determines whether a byte belongs to a character class.</summary>
	/// <param name="value">The byte.</param>
	/// <param name="characterClass">The requested class.</param>
	/// <returns><see langword="true"/> when the byte is a member.</returns>
	public bool IsMember( byte value, TrCharacterClass characterClass ) {
		if ( characterClass == TrCharacterClass.Digit ) {
			return value is >= (byte)'0' and <= (byte)'9';
		}
		if ( characterClass == TrCharacterClass.XDigit ) {
			return value is >= (byte)'0' and <= (byte)'9'
				or >= (byte)'A' and <= (byte)'F'
				or >= (byte)'a' and <= (byte)'f';
		}
		if ( null == this.myCulture ) {
			return IsAsciiMember( value, characterClass );
		}
		var character = (char)value;
		return characterClass switch {
			TrCharacterClass.Alnum => char.IsLetter( character ) || char.IsDigit( character ),
			TrCharacterClass.Alpha => char.IsLetter( character ),
			TrCharacterClass.Blank => character is ' ' or '\t' || char.GetUnicodeCategory( character ) == UnicodeCategory.SpaceSeparator,
			TrCharacterClass.Cntrl => char.IsControl( character ),
			TrCharacterClass.Graph => !char.IsControl( character ) && !char.IsWhiteSpace( character ),
			TrCharacterClass.Lower => char.IsLower( character ),
			TrCharacterClass.Print => !char.IsControl( character ),
			TrCharacterClass.Punct => char.IsPunctuation( character ) || char.IsSymbol( character ),
			TrCharacterClass.Space => char.IsWhiteSpace( character ),
			TrCharacterClass.Upper => char.IsUpper( character ),
			_ => false
		};
	}

	/// <summary>Converts a lowercase byte to its locale uppercase counterpart when representable.</summary>
	/// <param name="value">The byte.</param>
	/// <returns>The converted byte, or the original byte when conversion is not single-byte.</returns>
	public byte ToUpper( byte value ) {
		if ( null == this.myCulture ) {
			return value is >= (byte)'a' and <= (byte)'z' ? (byte)( value - 32 ) : value;
		}
		var converted = char.ToUpper( (char)value, this.myCulture );
		return converted <= byte.MaxValue ? (byte)converted : value;
	}

	/// <summary>Converts an uppercase byte to its locale lowercase counterpart when representable.</summary>
	/// <param name="value">The byte.</param>
	/// <returns>The converted byte, or the original byte when conversion is not single-byte.</returns>
	public byte ToLower( byte value ) {
		if ( null == this.myCulture ) {
			return value is >= (byte)'A' and <= (byte)'Z' ? (byte)( value + 32 ) : value;
		}
		var converted = char.ToLower( (char)value, this.myCulture );
		return converted <= byte.MaxValue ? (byte)converted : value;
	}

	private static bool IsAsciiMember( byte value, TrCharacterClass characterClass ) {
		var alpha = value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';
		var digit = value is >= (byte)'0' and <= (byte)'9';
		return characterClass switch {
			TrCharacterClass.Alnum => alpha || digit,
			TrCharacterClass.Alpha => alpha,
			TrCharacterClass.Blank => value is (byte)' ' or (byte)'\t',
			TrCharacterClass.Cntrl => value < 32 || value == 127,
			TrCharacterClass.Graph => value is >= 33 and <= 126,
			TrCharacterClass.Lower => value is >= (byte)'a' and <= (byte)'z',
			TrCharacterClass.Print => value is >= 32 and <= 126,
			TrCharacterClass.Punct => value is >= 33 and <= 126 && !alpha && !digit,
			TrCharacterClass.Space => value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\v' or (byte)'\f' or (byte)'\r',
			TrCharacterClass.Upper => value is >= (byte)'A' and <= (byte)'Z',
			_ => false
		};
	}
}
