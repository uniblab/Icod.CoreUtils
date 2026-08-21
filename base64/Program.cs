namespace Icod.CoreUtils.Base64;

using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>base64</c> command for encoding and decoding data with the Base64 representation.
/// </summary>
public static class Program {

	/// <summary>
	/// Runs the <c>base64</c> command using the process console and converts a console interrupt into a cancellation request.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>base64</c>.</param>
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
				"base64",
				cancellation.Token
			)
		).ConfigureAwait( false );
	}

}
