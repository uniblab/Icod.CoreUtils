namespace Icod.CoreUtils.DirColors;

/// <summary>Hosts the <c>dircolors</c> executable. Usage: <c>dircolors [OPTION]... [FILE]</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The asynchronous process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args );
	}
}
