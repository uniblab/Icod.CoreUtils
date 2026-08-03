namespace Icod.CoreUtils.Rm;

/// <summary>Provides the executable entry point for <c>rm [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs the <c>rm</c> command asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static async Task<int> Main( string[] args ) {
		return await Command.RunAsync( args ).ConfigureAwait( false );
	}
}
