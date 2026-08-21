namespace Icod.CoreUtils.MkTemp;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the <c>mktemp</c> process entry point.</summary>
public static class Program {
	/// <summary>Runs <c>mktemp</c> against the process console streams.</summary>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args, CommandContext.CreateConsole( "mktemp" ) );
	}
}
