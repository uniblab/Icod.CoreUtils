namespace Icod.CoreUtils.Fmt;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>fmt</c>.</summary>
public static class Program {
	/// <summary>Runs the command with console-backed streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args, CommandContext.CreateConsole( "fmt" ) );
	}
}
