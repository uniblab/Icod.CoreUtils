namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents the controlled result of searching authoritative source bytes.</summary>
public sealed class RegularExpressionByteMatchResult {
	private RegularExpressionByteMatchResult(
		bool isSuccess,
		RegularExpressionByteMatch? match,
		RegularExpressionDiagnostic? diagnostic
	) {
		IsSuccess = isSuccess;
		Match = match;
		Diagnostic = diagnostic;
	}

	/// <summary>Gets whether the search completed without a controlled error.</summary>
	public bool IsSuccess { get; }

	/// <summary>Gets whether a match was found.</summary>
	public bool IsMatch => Match is not null;

	/// <summary>Gets the selected leftmost-longest match, or <see langword="null"/> when none was found.</summary>
	public RegularExpressionByteMatch? Match { get; }

	/// <summary>Gets the deterministic match diagnostic when the search failed.</summary>
	public RegularExpressionDiagnostic? Diagnostic { get; }

	/// <summary>Creates a successful search result.</summary>
	/// <param name="match">The selected match, or <see langword="null"/> for a successful no-match result.</param>
	/// <returns>A successful result.</returns>
	public static RegularExpressionByteMatchResult Succeeded(
		RegularExpressionByteMatch? match
	) => new( true, match, null );

	/// <summary>Creates a failed search result.</summary>
	/// <param name="diagnostic">The deterministic match diagnostic.</param>
	/// <returns>A failed result.</returns>
	public static RegularExpressionByteMatchResult Failed(
		RegularExpressionDiagnostic diagnostic
	) {
		ArgumentNullException.ThrowIfNull( diagnostic );
		return new( false, null, diagnostic );
	}
}
