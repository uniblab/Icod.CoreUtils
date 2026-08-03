namespace Icod.CoreUtils.Chmod;

/// <summary>Provides the managed executable entry point for <c>chmod</c>.</summary>
public static class Program {
	/// <summary>Runs <c>chmod</c> with the process command-line arguments.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args ).AsTask();
}
