namespace Icod.CoreUtils.Install;

/// <summary>Provides the <c>install</c> executable entry point.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args ).AsTask();
}
