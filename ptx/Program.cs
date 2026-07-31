namespace Icod.CoreUtils.Ptx;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the <c>ptx [OPTION]... [INPUT]...</c> process entry point.</summary>
public static class Program {
	/// <summary>Runs the permuted-index command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync(
		args,
		CommandContext.CreateConsole( "ptx" )
	);
}
