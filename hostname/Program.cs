namespace Icod.CoreUtils.HostName;
/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>hostname</c> command for reporting or setting the system host name where supported.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>hostname</c> command using the process console and converts a console interrupt into a cancellation request.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>hostname</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> Main( string[] args ) {
		using var cancellation = new CancellationTokenSource();
		ConsoleCancelEventHandler handler = ( _, eventArgs ) => {
			eventArgs.Cancel = true;
			cancellation.Cancel();
		};
		Console.CancelKeyPress += handler;
		try {
			return await Command.RunAsync(
				args,
				Console.In,
				Console.Out,
				Console.Error,
				cancellation.Token
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
