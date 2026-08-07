namespace Icod.CoreUtils.Nice;

/// <summary>Hosts the GNU <c>nice</c> command.</summary>
internal static class Program {
	/// <summary>Runs <c>nice</c>.</summary>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
