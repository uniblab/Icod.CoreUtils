// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.Pmap;

/// <summary>Provides the <c>pmap [options] PID [PID ...]</c> executable entry point.</summary>
public static class Program {
	/// <summary>Runs the procps-ng-inspired <c>pmap</c> command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
