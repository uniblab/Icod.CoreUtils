namespace Icod.CoreUtils.Tee;

using Icod.CoreUtils.Shared.Diagnostics;

public static class Program {

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
