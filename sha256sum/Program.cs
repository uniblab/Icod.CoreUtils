namespace Icod.CoreUtils.Sha256Sum;

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

		return await Command.RunAsync(
			args,
			CommandContext.CreateConsole(
				"sha256sum",
				cancellation.Token
			)
		).ConfigureAwait( false );
	}

}
