namespace Icod.DiffUtils.Diff;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>diff</c>.</summary>
public static class Program {
	/// <summary>Runs the command and returns its process exit status.</summary>
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
				CommandContext.CreateConsole( "diff", cancellation.Token )
			).ConfigureAwait( false );
		} finally {
			Console.CancelKeyPress -= handler;
		}
	}
}
