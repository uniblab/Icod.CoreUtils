namespace Icod.CoreUtils.Vdir;

/// <summary>Hosts the <c>vdir</c> executable. Usage: <c>vdir [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The asynchronous process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync( args );
	}
}
