namespace Icod.CoreUtils.Sha384Sum;

using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>sha384sum</c> command for computing and verifying SHA-384 message digests.
/// </summary>
public static class Program {

	/// <summary>
	/// Runs the <c>sha384sum</c> command using the process console and converts a console interrupt into a cancellation request.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>sha384sum</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> Main(
		string[] args
	) {
		using var cancellation = new CancellationTokenSource();
		Console.CancelKeyPress += (
			sender,
			eventArgs
		) => {
			eventArgs.Cancel = true;
			cancellation.Cancel();
		};

		return await Command.RunAsync(
			args,
			CommandContext.CreateConsole(
				"sha384sum",
				cancellation.Token
			)
		).ConfigureAwait( false );
	}

}
