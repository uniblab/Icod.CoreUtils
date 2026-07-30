namespace Icod.CoreUtils.Comm;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the process entry point for <c>comm [OPTION]... FILE1 FILE2</c>.</summary>
public static class Program {
	/// <summary>Runs the command using process console streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args, CommandContext.CreateConsole( "comm" ) );
	}
}
