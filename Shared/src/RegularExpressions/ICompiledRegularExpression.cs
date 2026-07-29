namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents a reusable compiled regular expression.</summary>
public interface ICompiledRegularExpression {
	/// <summary>Gets the original GNU basic regular-expression pattern.</summary>
	string Pattern { get; }

	/// <summary>Gets the number of numbered parenthesized subexpressions.</summary>
	int CaptureCount { get; }

	/// <summary>Searches an input string using GNU/POSIX leftmost-longest selection.</summary>
	/// <param name="input">The input string.</param>
	/// <param name="options">Optional search positioning policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled search result.</returns>
	RegularExpressionMatchResult Match(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Asynchronously searches without offloading work to the thread pool.</summary>
	/// <param name="input">The input string.</param>
	/// <param name="options">Optional search positioning policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled search result.</returns>
	ValueTask<RegularExpressionMatchResult> MatchAsync(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	);
}
