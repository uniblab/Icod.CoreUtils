namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Rewrites a legacy command-line token into one or more ordinary parser tokens.
/// </summary>
/// <remarks>
/// A rule should return <see langword="null"/> when it does not recognize the token.
/// The first matching rule wins. Rules run only while option parsing is active;
/// tokens consumed as option values and tokens following <c>--</c> are not rewritten.
/// Rewritten tokens retain the original argument index.
/// </remarks>
public sealed class OptionTokenRewriteRule {

	private readonly Func<string, IReadOnlyList<string>?> myRewrite;

	/// <summary>
	/// Initializes a new token rewrite rule.
	/// </summary>
	/// <param name="rewrite">Function returning replacement tokens, or <see langword="null"/> when unmatched.</param>
	public OptionTokenRewriteRule(
		Func<string, IReadOnlyList<string>?> rewrite
	) {
		this.myRewrite = rewrite ?? throw new ArgumentNullException(
			nameof( rewrite )
		);
	}

	internal IReadOnlyList<string>? Rewrite(
		string token
	) {
		return this.myRewrite(
			token
		);
	}

}
