namespace Icod.CoreUtils.Expand;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the process entry point for <c>expand [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs <c>expand</c> against the process console streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args, CommandContext.CreateConsole( "expand" ) );
	}
}
