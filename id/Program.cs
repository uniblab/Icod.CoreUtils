namespace Icod.CoreUtils.ID;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>id</c> command for reporting user and group identity information.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>id</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>id</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
