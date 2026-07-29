namespace Icod.CoreUtils.Groups;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>groups</c> command for reporting the supplementary group memberships of users.
/// </summary>
public static class Program {
	/// <summary>
	/// Runs the <c>groups</c> command with the supplied command-line arguments.
	/// </summary>
	/// <param name="args">The command-line arguments supplied to <c>groups</c>.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
