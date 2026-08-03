namespace Icod.CoreUtils.MkNod;

/// <summary>Provides the managed executable entry point for <c>mknod</c>.</summary>
public static class Program {
	/// <summary>Runs <c>mknod</c> with the process command-line arguments.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args ).AsTask();
}
