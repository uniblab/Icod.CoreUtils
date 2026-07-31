namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Specifies how pathname characters are compared.
/// </summary>
public enum PathCaseSensitivity {
	/// <summary>
	/// Uses the host pathname convention: insensitive on Windows and sensitive elsewhere.
	/// </summary>
	Automatic = 0,

	/// <summary>
	/// Compares pathname characters using ordinal case-sensitive comparison.
	/// </summary>
	Sensitive = 1,

	/// <summary>
	/// Compares pathname characters using ordinal case-insensitive comparison.
	/// </summary>
	Insensitive = 2
}

/// <summary>
/// Specifies whether a leading period must be named explicitly by a pathname pattern segment.
/// </summary>
public enum LeadingPeriodPolicy {
	/// <summary>
	/// Allows wildcard tokens to match a leading period.
	/// </summary>
	WildcardMayMatch = 0,

	/// <summary>
	/// Requires the first token in a segment to be a literal period before a leading period can match.
	/// </summary>
	RequireExplicitPeriod = 1
}

/// <summary>
/// Configures parsing and matching of pathname patterns.
/// </summary>
public sealed class PathnamePatternOptions {
	/// <summary>
	/// Gets a default immutable-by-convention option instance.
	/// </summary>
	public static PathnamePatternOptions Default { get; } = new();

	/// <summary>
	/// Gets or initializes the pathname case-sensitivity policy.
	/// </summary>
	public PathCaseSensitivity CaseSensitivity { get; init; } = PathCaseSensitivity.Automatic;

	/// <summary>
	/// Gets or initializes the leading-period policy.
	/// </summary>
	public LeadingPeriodPolicy LeadingPeriodPolicy { get; init; } = LeadingPeriodPolicy.RequireExplicitPeriod;

	/// <summary>
	/// Gets or initializes whether a backslash quotes the following metacharacter inside a segment.
	/// The default is enabled on Unix-like hosts and disabled on Windows, where backslash is a pathname separator.
	/// </summary>
	public bool BackslashEscapes { get; init; } = !OperatingSystem.IsWindows();


	/// <summary>
	/// Validates the configured syntax and comparison values.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">An enumeration value is invalid.</exception>
	internal void Validate() {
		if ( !Enum.IsDefined( typeof( PathCaseSensitivity ), CaseSensitivity ) ) {
			throw new ArgumentOutOfRangeException( nameof( CaseSensitivity ) );
		}
		if ( !Enum.IsDefined( typeof( LeadingPeriodPolicy ), LeadingPeriodPolicy ) ) {
			throw new ArgumentOutOfRangeException( nameof( LeadingPeriodPolicy ) );
		}
	}

	/// <summary>
	/// Resolves the configured comparison to an ordinal string comparison.
	/// </summary>
	/// <returns>The resolved ordinal comparison.</returns>
	internal StringComparison ResolveStringComparison() => ResolveCaseSensitive()
		? StringComparison.Ordinal
		: StringComparison.OrdinalIgnoreCase;

	/// <summary>
	/// Resolves whether matching is case-sensitive.
	/// </summary>
	/// <returns><see langword="true"/> when matching is case-sensitive.</returns>
	internal bool ResolveCaseSensitive() => CaseSensitivity switch {
		PathCaseSensitivity.Sensitive => true,
		PathCaseSensitivity.Insensitive => false,
		_ => !OperatingSystem.IsWindows()
	};
}
