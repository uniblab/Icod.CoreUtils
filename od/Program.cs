namespace Icod.CoreUtils.Od;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Provides the <c>od</c> process entry point.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs <c>od</c> against the process console streams.
	/// </summary>
	public static Task<int> Main(
		string[] args
	) {
		return Command.RunAsync(
			args,
			CommandContext.CreateConsole( "od" )
		);
	}
}
