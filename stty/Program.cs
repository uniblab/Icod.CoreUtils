namespace Icod.CoreUtils.Stty;

/// <summary>Hosts the <c>stty</c> command-line entry point.</summary>
public static class Program {
	/// <summary>Runs the command asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The process exit code.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
