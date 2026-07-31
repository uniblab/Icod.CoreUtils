namespace Icod.CoreUtils.Tac;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the <c>tac [OPTION]... [FILE]...</c> process entry point.</summary>
public static class Program {
	/// <summary>Runs the GNU-compatible reverse-record command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync(
		args,
		CommandContext.CreateConsole( "tac" )
	);
}
