using System.Globalization;
using System.Text;

namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Implements POSIX-style classes and scalar collation with .NET Unicode and culture data.</summary>
public sealed class UnicodeRegularExpressionCharacterClassProvider : IRegularExpressionCharacterClassProvider {
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

	private readonly CompareInfo compareInfo;

	/// <summary>Initializes a provider using the supplied culture.</summary>
	/// <param name="culture">The culture whose collation rules are used.</param>
	public UnicodeRegularExpressionCharacterClassProvider( CultureInfo culture ) {
		ArgumentNullException.ThrowIfNull( culture );
		Culture = culture;
		compareInfo = culture.CompareInfo;
	}

	/// <summary>Gets a provider using <see cref="CultureInfo.CurrentCulture"/>.</summary>
	public static UnicodeRegularExpressionCharacterClassProvider CurrentCulture => new( CultureInfo.CurrentCulture );

	/// <summary>Gets an invariant provider suitable for deterministic tests and culture-neutral commands.</summary>
	public static UnicodeRegularExpressionCharacterClassProvider InvariantCulture { get; } = new( CultureInfo.InvariantCulture );

	/// <summary>Gets the culture used for collation.</summary>
	public CultureInfo Culture { get; }

	/// <inheritdoc/>
	public bool IsSupportedClass( string className ) {
		ArgumentNullException.ThrowIfNull( className );
		return SupportedClasses.Contains( className );
	}

	/// <inheritdoc/>
	public bool IsCharacterClass( Rune value, string className, bool ignoreCase ) {
		ArgumentNullException.ThrowIfNull( className );
		var category = Rune.GetUnicodeCategory( value );
		return className switch {
			"alnum" => Rune.IsLetterOrDigit( value ),
			"alpha" => Rune.IsLetter( value ),
			"blank" => value.Value is '\t' || category is UnicodeCategory.SpaceSeparator,
			"cntrl" => category is UnicodeCategory.Control,
			"digit" => category is UnicodeCategory.DecimalDigitNumber,
			"graph" => IsGraphical( value, category ),
			"lower" => ignoreCase ? Rune.IsLetter( value ) : Rune.IsLower( value ),
			"print" => IsPrintable( value, category ),
			"punct" => IsGraphical( value, category ) && !Rune.IsLetterOrDigit( value ),
			"space" => Rune.IsWhiteSpace( value ),
			"upper" => ignoreCase ? Rune.IsLetter( value ) : Rune.IsUpper( value ),
			"xdigit" => IsHexadecimalDigit( value ),
			_ => false
		};
	}

	/// <inheritdoc/>
	public bool IsWordCharacter( Rune value ) => value.Value is '_' || Rune.IsLetterOrDigit( value );

	/// <inheritdoc/>
	public int Compare( Rune left, Rune right, bool ignoreCase ) => compareInfo.Compare(
		left.ToString(),
		right.ToString(),
		ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None
	);

	/// <inheritdoc/>
	public bool AreCharactersEqual( Rune left, Rune right, bool ignoreCase ) => ignoreCase
		? 0 == compareInfo.Compare( left.ToString(), right.ToString(), CompareOptions.IgnoreCase )
		: left == right;

	/// <inheritdoc/>
	public bool AreCollatingElementsEquivalent( Rune left, Rune right, bool ignoreCase ) {
		var compareOptions = CompareOptions.IgnoreNonSpace;
		if ( ignoreCase ) {
			compareOptions |= CompareOptions.IgnoreCase;
		}
		return 0 == compareInfo.Compare( left.ToString(), right.ToString(), compareOptions );
	}

	private static bool IsGraphical( Rune value, UnicodeCategory category ) =>
		IsPrintable( value, category ) && !Rune.IsWhiteSpace( value );

	private static bool IsPrintable( Rune value, UnicodeCategory category ) =>
		0 != value.Value
		&& category is not UnicodeCategory.Control
		&& category is not UnicodeCategory.Format
		&& category is not UnicodeCategory.Surrogate
		&& category is not UnicodeCategory.OtherNotAssigned;

	private static bool IsHexadecimalDigit( Rune value ) => value.Value is >= '0' and <= '9'
		or >= 'a' and <= 'f'
		or >= 'A' and <= 'F';
}
