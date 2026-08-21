namespace Icod.CoreUtils.Sort;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>sort [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs the command against the process standard streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync(
			args,
			CommandContext.CreateConsole( "sort" )
		);
	}
}
