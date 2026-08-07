namespace Icod.CoreUtils.Nohup;

/// <summary>Entry point for GNU <c>nohup</c>.</summary>
internal static class Program {
	/// <summary>Runs GNU <c>nohup</c>.</summary>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
