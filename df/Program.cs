namespace Icod.CoreUtils.Df;

/// <summary>Provides the executable entry point for <c>df</c>. Usage: <c>df [OPTION]... [FILE]...</c>.</summary>
public static class Program {
	/// <summary>Runs <c>df</c>.</summary>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
