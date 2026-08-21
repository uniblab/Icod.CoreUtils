using Icod.CommandFramework.Diagnostics;

namespace Icod.CoreUtils.DD;

internal static class Program {
	public static Task<int> Main(
		string[] args
	) => Command.RunAsync(
		args,
		CommandContext.CreateConsole(
			"dd"
		)
	);
}
