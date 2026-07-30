namespace Icod.CoreUtils.Tr;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the <c>tr [OPTION]... STRING1 [STRING2]</c> process entry point.</summary>
public static class Program {
	/// <summary>Runs the byte translation, deletion, and squeezing command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync(
		args,
		CommandContext.CreateConsole( "tr" )
	);
}
