namespace Icod.CoreUtils.RealPath;

/// <summary>Provides the executable entry point for <c>realpath [OPTION]... FILE...</c>.</summary>
public static class Program {
	/// <summary>Runs <c>realpath</c> with process-console streams and interrupt cancellation.</summary>
	/// <param name="args">The command-line arguments supplied to <c>realpath</c>.</param>
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
