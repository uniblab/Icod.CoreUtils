namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents a reusable compiled GNU regular expression.</summary>
public interface ICompiledRegularExpression {
	/// <summary>Gets the original regular-expression pattern.</summary>
	string Pattern { get; }

	/// <summary>Gets the number of numbered parenthesized subexpressions.</summary>
	int CaptureCount { get; }

	/// <summary>Searches an input string using GNU/POSIX leftmost-longest selection.</summary>
	/// <param name="input">The input string.</param>
	/// <param name="options">Optional search positioning policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled search result. Match and capture offsets use UTF-16 input indices.</returns>
	RegularExpressionMatchResult Match(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Asynchronously searches a string without offloading work to the thread pool.</summary>
	/// <param name="input">The input string.</param>
	/// <param name="options">Optional search positioning policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled search result.</returns>
	ValueTask<RegularExpressionMatchResult> MatchAsync(
		string input,
		RegularExpressionMatchOptions? options = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Searches authoritative source bytes using either byte-valued units or explicitly decoded UTF-8 units.
	/// </summary>
	/// <param name="input">The authoritative source bytes.</param>
	/// <param name="inputOptions">Optional byte-decoding and invalid-input policy.</param>
	/// <param name="matchOptions">Optional source-byte search positioning policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled search result. Match and capture offsets are source-byte offsets.</returns>
	/// <exception cref="System.Text.DecoderFallbackException">Malformed UTF-8 is encountered under the throw policy.</exception>
	RegularExpressionByteMatchResult Match(
		ReadOnlyMemory<byte> input,
		RegularExpressionInputOptions? inputOptions = null,
		RegularExpressionByteMatchOptions? matchOptions = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Asynchronously searches authoritative source bytes without offloading work to the thread pool.</summary>
	/// <param name="input">The authoritative source bytes.</param>
	/// <param name="inputOptions">Optional byte-decoding and invalid-input policy.</param>
	/// <param name="matchOptions">Optional source-byte search positioning policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled search result.</returns>
	/// <exception cref="System.Text.DecoderFallbackException">Malformed UTF-8 is encountered under the throw policy.</exception>
	ValueTask<RegularExpressionByteMatchResult> MatchAsync(
		ReadOnlyMemory<byte> input,
		RegularExpressionInputOptions? inputOptions = null,
		RegularExpressionByteMatchOptions? matchOptions = null,
		CancellationToken cancellationToken = default
	);
}
