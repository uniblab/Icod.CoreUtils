namespace Icod.CoreUtils.UName;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>uname</c> command for reporting system and kernel identification information.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>uname</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>uname</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
