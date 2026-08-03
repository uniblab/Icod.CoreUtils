namespace Icod.CoreUtils.Link;

/// <summary>Provides the asynchronous entry point for <c>link</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	public static async Task<int> Main( string[] args ) => await Command.RunAsync( args ).ConfigureAwait( false );
}
