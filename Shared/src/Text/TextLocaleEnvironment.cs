namespace Icod.CoreUtils.Shared.Text;

/// <summary>Resolves the text locale used by byte-preserving command implementations.</summary>
/// <remarks>
/// This provisional cross-suite <c>Icod.CommandFramework</c> candidate recognizes the C/POSIX
/// byte locale explicitly and otherwise selects the deterministic UTF-8 text profile.
/// </remarks>
public static class TextLocaleEnvironment {
	/// <summary>Resolves the current process locale using <c>LC_ALL</c>, <c>LC_CTYPE</c>, then <c>LANG</c>.</summary>
	/// <returns>The selected locale provider.</returns>
	public static ITextLocaleProvider Resolve() {
		return Resolve(
			Environment.GetEnvironmentVariable( "LC_ALL" ),
			Environment.GetEnvironmentVariable( "LC_CTYPE" ),
			Environment.GetEnvironmentVariable( "LANG" )
		);
	}

	/// <summary>Resolves a locale from values supplied in standard environment-variable precedence.</summary>
	/// <param name="lcAll">The <c>LC_ALL</c> value.</param>
	/// <param name="lcCtype">The <c>LC_CTYPE</c> value.</param>
	/// <param name="lang">The <c>LANG</c> value.</param>
	/// <returns>The selected locale provider.</returns>
	public static ITextLocaleProvider Resolve(
		string? lcAll,
		string? lcCtype,
		string? lang
	) {
		var name = FirstNonempty( lcAll, lcCtype, lang );
		if (
			string.Equals( name, "C", StringComparison.OrdinalIgnoreCase )
			|| string.Equals( name, "POSIX", StringComparison.OrdinalIgnoreCase )
		) {
			return PosixCLocaleProvider.Instance;
		}
		return new UnicodeTextLocaleProvider(
			string.IsNullOrWhiteSpace( name ) ? "UTF-8" : name
		);
	}

	private static string? FirstNonempty( params string?[] values ) {
		foreach ( var value in values ) {
			if ( !string.IsNullOrWhiteSpace( value ) ) {
				return value;
			}
		}
		return null;
	}
}
