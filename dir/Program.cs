namespace Icod.CoreUtils.Dir;

/// <summary>Hosts the <c>dir</c> executable. Usage: <c>dir [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The asynchronous process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args );
	}
}
