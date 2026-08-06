namespace Icod.CoreUtils.Ls;

/// <summary>Hosts the <c>ls</c> executable. Usage: <c>ls [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The asynchronous process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args );
	}
}
