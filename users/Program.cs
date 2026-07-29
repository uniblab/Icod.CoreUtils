namespace Icod.CoreUtils.Users;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>users</c> command for listing users currently logged in.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>users</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>users</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
