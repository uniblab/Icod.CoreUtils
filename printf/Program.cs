namespace Icod.CoreUtils.Printf;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Provides the <c>printf</c> process entry point.</summary>
public static class Program {
	/// <summary>Runs <c>printf</c> against the process console streams.</summary>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args, CommandContext.CreateConsole( "printf" ) );
	}
}
