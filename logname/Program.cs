namespace Icod.CoreUtils.LogName;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>logname</c> command for reporting the login name associated with the current session.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>logname</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>logname</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
