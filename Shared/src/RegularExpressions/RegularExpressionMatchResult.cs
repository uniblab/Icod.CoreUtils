namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents the controlled result of searching with a compiled regular expression.</summary>
public sealed class RegularExpressionMatchResult {
	private RegularExpressionMatchResult(
		bool isSuccess,
		RegularExpressionMatch? match,
		RegularExpressionDiagnostic? diagnostic
	) {
		IsSuccess = isSuccess;
		Match = match;
		Diagnostic = diagnostic;
	}

	/// <summary>Gets whether the search completed without a controlled error.</summary>
	public bool IsSuccess { get; }

	/// <summary>Gets whether a match was found.</summary>
	public bool IsMatch => null is not Match;

	/// <summary>Gets the selected leftmost-longest match, or <see langword="null"/> when none was found.</summary>
	public RegularExpressionMatch? Match { get; }

	/// <summary>Gets the deterministic match diagnostic when the search failed.</summary>
	public RegularExpressionDiagnostic? Diagnostic { get; }

	/// <summary>Creates a successful search result.</summary>
	/// <param name="match">The selected match, or <see langword="null"/> for a successful no-match result.</param>
	/// <returns>A successful result.</returns>
	public static RegularExpressionMatchResult Succeeded( RegularExpressionMatch? match ) => new( true, match, null );

	/// <summary>Creates a failed search result.</summary>
	/// <param name="diagnostic">The deterministic match diagnostic.</param>
	/// <returns>A failed result.</returns>
	public static RegularExpressionMatchResult Failed( RegularExpressionDiagnostic diagnostic ) {
		ArgumentNullException.ThrowIfNull( diagnostic );
		return new( false, null, diagnostic );
	}
}
