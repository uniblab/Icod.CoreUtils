namespace Icod.CoreUtils.DU;

/// <summary>Provides the executable entry point for <c>du</c>. Usage: <c>du [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs <c>du</c>.</summary>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
