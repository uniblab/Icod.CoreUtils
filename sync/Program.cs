using Icod.CoreUtils.Shared.Diagnostics;

namespace Icod.CoreUtils.Sync;

internal static class Program {
	public static Task<int> Main(
		string[] args
	) => Command.RunAsync(
		args,
		CommandContext.CreateConsole(
			"sync"
		)
	);
}
