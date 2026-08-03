namespace Icod.CoreUtils.Chown;

/// <summary>Provides the managed executable entry point for <c>chown</c>.</summary>
public static class Program {
	/// <summary>Runs <c>chown</c> with the process command-line arguments.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args ).AsTask();
}
