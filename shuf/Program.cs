namespace Icod.CoreUtils.Shuf;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the process entry point for <c>shuf [OPTION]... [FILE]</c>, <c>shuf -e [OPTION]... [ARG]...</c>, and <c>shuf -i LO-HI [OPTION]...</c>.</summary>
public static class Program {
	/// <summary>Runs the command using process console streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync(
			args,
			CommandContext.CreateConsole( "shuf" )
		);
	}
}
