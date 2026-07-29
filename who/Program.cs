namespace Icod.CoreUtils.Who;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>who</c> command for reporting users currently logged in and their sessions.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>who</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>who</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
