namespace Icod.CoreUtils.Env;

/// <summary>Entry point for GNU <c>env</c>.</summary>
internal static class Program {
	/// <summary>Runs GNU <c>env</c>.</summary>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
