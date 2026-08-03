namespace Icod.CoreUtils.Ln;

/// <summary>Provides the asynchronous entry point for <c>ln</c>.</summary>
internal static class Program {
	/// <summary>Runs the command.</summary>
	public static async Task<int> Main( string[] args ) => await Command.RunAsync( args ).ConfigureAwait( false );
}
