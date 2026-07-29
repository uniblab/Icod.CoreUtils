namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Compiles regular expressions behind an injectable command-facing abstraction.</summary>
public interface IRegularExpressionProvider {
	/// <summary>Compiles a GNU basic regular expression.</summary>
	/// <param name="pattern">The pattern text.</param>
	/// <param name="options">Optional compile and match policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled compilation result.</returns>
	RegularExpressionCompileResult Compile(
		string pattern,
		RegularExpressionOptions? options = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Asynchronously compiles a GNU basic regular expression without offloading work to the thread pool.</summary>
	/// <param name="pattern">The pattern text.</param>
	/// <param name="options">Optional compile and match policy.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The controlled compilation result.</returns>
	ValueTask<RegularExpressionCompileResult> CompileAsync(
		string pattern,
		RegularExpressionOptions? options = null,
		CancellationToken cancellationToken = default
	);
}
