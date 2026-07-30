namespace Icod.CoreUtils.Paste;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the process entry point for <c>paste</c>.</summary>
public static class Program {
	/// <summary>Runs the command using process standard streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args, CommandContext.CreateConsole( "paste" ) );
}
