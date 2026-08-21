namespace Icod.CoreUtils.Fold;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>fold [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs <c>fold</c> against the process console streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args, CommandContext.CreateConsole( "fold" ) );
	}
}
