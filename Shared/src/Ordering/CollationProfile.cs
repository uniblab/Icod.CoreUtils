namespace Icod.CoreUtils.Shared.Ordering;

using System.Globalization;

/// <summary>Describes the locale and comparison policy used to collate managed strings.</summary>
public sealed class CollationProfile {
	private CollationProfile(
		string name,
		CultureInfo? culture,
		CompareOptions compareOptions,
		bool isBytewise
	) {
		this.Name = name;
		this.Culture = culture;
		this.CompareOptions = compareOptions;
		this.IsBytewise = isBytewise;
	}

	/// <summary>Gets the normalized locale name.</summary>
	public string Name { get; }

	/// <summary>Gets the managed culture, or <see langword="null"/> for C/POSIX bytewise collation.</summary>
	public CultureInfo? Culture { get; }

	/// <summary>Gets the managed comparison options.</summary>
	public CompareOptions CompareOptions { get; }

	/// <summary>Gets whether values are compared by their UTF-8 bytes instead of managed linguistic collation.</summary>
	public bool IsBytewise { get; }

	/// <summary>Creates the exact C/POSIX bytewise collation profile.</summary>
	/// <returns>The bytewise profile.</returns>
	public static CollationProfile CreateBytewise() {
		return new CollationProfile( "C", null, CompareOptions.Ordinal, true );
	}

	/// <summary>Creates a managed linguistic collation profile.</summary>
	/// <param name="culture">The culture whose <see cref="CompareInfo"/> supplies collation.</param>
	/// <param name="compareOptions">The requested managed comparison options.</param>
	/// <returns>The linguistic profile.</returns>
	public static CollationProfile CreateCulture(
		CultureInfo culture,
		CompareOptions compareOptions = CompareOptions.None
	) {
		ArgumentNullException.ThrowIfNull( culture );
		return new CollationProfile(
			string.IsNullOrEmpty( culture.Name ) ? "Invariant" : culture.Name,
			culture,
			compareOptions,
			false
		);
	}
}

/// <summary>Describes locale-resolution success or a controlled unsupported-locale diagnostic.</summary>
/// <param name="IsSuccess">Whether a profile was resolved.</param>
/// <param name="Profile">The resolved profile when successful.</param>
/// <param name="ErrorMessage">The controlled error message when unsuccessful.</param>
public sealed record CollationResolutionResult(
	bool IsSuccess,
	CollationProfile? Profile,
	string? ErrorMessage
) {
	/// <summary>Creates a successful locale-resolution result.</summary>
	/// <param name="profile">The resolved profile.</param>
	/// <returns>The successful result.</returns>
	public static CollationResolutionResult Succeeded( CollationProfile profile ) {
		ArgumentNullException.ThrowIfNull( profile );
		return new( true, profile, null );
	}

	/// <summary>Creates an unsuccessful locale-resolution result.</summary>
	/// <param name="errorMessage">The controlled diagnostic.</param>
	/// <returns>The unsuccessful result.</returns>
	public static CollationResolutionResult Failed( string errorMessage ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( errorMessage );
		return new( false, null, errorMessage );
	}
}
