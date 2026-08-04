namespace Icod.LineEditor.Red;

/// <summary>
/// Hosts the <c>red</c> executable entry point.
/// </summary>
internal static class Program {
	/// <summary>
	/// Runs the restricted line-editor command facade.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The process exit status.</returns>
	internal static int Main( string[] args ) {
		return Command.Run( args );
	}
}
