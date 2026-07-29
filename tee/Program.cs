namespace Icod.CoreUtils.Tee;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>tee</c> command for copying standard input to standard output and files.
/// </summary>
public static class Program {

	/// <summary>
	/// Runs the <c>tee</c> command using the process console and applies the command's interrupt-handling policy.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>tee</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> Main(
		string[] args
	) {
		using var cancellation = new CancellationTokenSource();
		var ignoreInterrupts = Command.RequestsIgnoredInterrupts(
			args
		);
		Console.CancelKeyPress += (
			sender,
			eventArgs
		) => {
			eventArgs.Cancel = true;
			if ( !ignoreInterrupts ) {
				cancellation.Cancel();
			}
		};
		return await Command.RunAsync(
			args,
			CommandContext.CreateConsole(
				"tee",
				cancellation.Token
			)
		).ConfigureAwait( false );
	}

}
