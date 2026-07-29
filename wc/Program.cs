namespace Icod.CoreUtils.WC;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>wc</c> command for counting lines, words, characters, and bytes.
/// </summary>
public static class Program {

	/// <summary>
	/// Runs the <c>wc</c> command using the process console and converts a console interrupt into a cancellation request.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>wc</c>.</param>
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
				"wc",
				cancellation.Token
			)
		).ConfigureAwait( false );
	}

}
