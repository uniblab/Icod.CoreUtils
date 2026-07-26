namespace Icod.CoreUtils.Sed;

using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Program {

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
		var context = CommandContext.CreateConsole(
			"sed",
			cancellation.Token
		);
		return await Command.RunAsync(
			args,
			context.StandardInput,
			context.StandardOutput,
			context.StandardError,
			context.CancellationToken
		).ConfigureAwait( false );
	}

}
