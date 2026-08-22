namespace Icod.DiffUtils.SDiff;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>sdiff</c>.</summary>
public static class Program {
	/// <summary>Runs <c>sdiff [OPTION]... FILE1 FILE2</c> and returns its process status.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
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
				CommandContext.CreateConsole( "sdiff", cancellation.Token )
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
