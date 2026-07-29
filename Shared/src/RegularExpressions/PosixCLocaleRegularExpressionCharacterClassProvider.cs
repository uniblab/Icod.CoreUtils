using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Implements POSIX regular-expression classification and collation for the invariant C locale.</summary>
public sealed class PosixCLocaleRegularExpressionCharacterClassProvider : IRegularExpressionCharacterClassProvider {
	private static readonly HashSet<string> SupportedClasses = new(
		[
			"alnum",
			"alpha",
			"blank",
			"cntrl",
			"digit",
			"graph",
			"lower",
			"print",
			"punct",
			"space",
			"upper",
			"xdigit"
		],
		StringComparer.Ordinal
	);

	private PosixCLocaleRegularExpressionCharacterClassProvider() {
	}

	/// <summary>Gets the reusable C-locale provider.</summary>
	public static PosixCLocaleRegularExpressionCharacterClassProvider Instance { get; } = new();

	/// <inheritdoc/>
	public bool IsSupportedClass( string className ) {
		ArgumentNullException.ThrowIfNull( className );
		return SupportedClasses.Contains( className );
	}

	/// <inheritdoc/>
	public bool IsCharacterClass( Rune value, string className, bool ignoreCase ) {
		ArgumentNullException.ThrowIfNull( className );
		var scalar = value.Value;
		return className switch {
			"alnum" => IsAsciiLetter( scalar ) || IsAsciiDigit( scalar ),
			"alpha" => IsAsciiLetter( scalar ),
			"blank" => scalar is ' ' or '\t',
			"cntrl" => scalar is >= 0 and <= 0x1f or 0x7f,
			"digit" => IsAsciiDigit( scalar ),
			"graph" => scalar is >= 0x21 and <= 0x7e,
			"lower" => ignoreCase ? IsAsciiLetter( scalar ) : scalar is >= 'a' and <= 'z',
			"print" => scalar is >= 0x20 and <= 0x7e,
			"punct" => scalar is >= 0x21 and <= 0x7e
				&& !IsAsciiLetter( scalar )
				&& !IsAsciiDigit( scalar ),
			"space" => scalar is ' ' or '\t' or '\n' or '\v' or '\f' or '\r',
			"upper" => ignoreCase ? IsAsciiLetter( scalar ) : scalar is >= 'A' and <= 'Z',
			"xdigit" => IsAsciiHexadecimalDigit( scalar ),
			_ => false
		};
	}

	/// <inheritdoc/>
	public bool IsWordCharacter( Rune value ) => value.Value is '_'
		|| IsAsciiLetter( value.Value )
		|| IsAsciiDigit( value.Value );

	/// <inheritdoc/>
	public int Compare( Rune left, Rune right, bool ignoreCase ) {
		var leftValue = ignoreCase ? FoldAsciiCase( left.Value ) : left.Value;
		var rightValue = ignoreCase ? FoldAsciiCase( right.Value ) : right.Value;
		return leftValue.CompareTo( rightValue );
	}

	/// <inheritdoc/>
	public bool AreCharactersEqual( Rune left, Rune right, bool ignoreCase ) => 0 == Compare( left, right, ignoreCase );

	/// <inheritdoc/>
	public bool AreCollatingElementsEquivalent( Rune left, Rune right, bool ignoreCase ) => 0 == Compare( left, right, ignoreCase );

	private static int FoldAsciiCase( int value ) => value is >= 'a' and <= 'z'
		? value - ( 'a' - 'A' )
		: value;

	private static bool IsAsciiLetter( int value ) => value is >= 'A' and <= 'Z'
		or >= 'a' and <= 'z';

	private static bool IsAsciiDigit( int value ) => value is >= '0' and <= '9';

	private static bool IsAsciiHexadecimalDigit( int value ) => IsAsciiDigit( value )
		|| value is >= 'A' and <= 'F'
		|| value is >= 'a' and <= 'f';
}
