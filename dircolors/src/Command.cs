// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.DirColors;

using Icod.CoreUtils.Shared.DirectoryListing;

/// <summary>Provides the <c>dircolors</c> executable boundary over the shared database parser.</summary>
public static class Command {
	/// <summary>Runs <c>dircolors</c> asynchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdin">Optional standard input.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The asynchronous process exit status.</returns>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		return DirColorsCommand.RunAsync(
			args,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		);
	}

	/// <summary>Runs <c>dircolors</c> synchronously for compatibility with existing callers.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdin">Optional standard input.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		return RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
	}
}
