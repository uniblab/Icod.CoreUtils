namespace Icod.CoreUtils.WhoAmI;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>whoami</c> command for reporting the effective user name.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>whoami</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>whoami</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
