namespace Icod.CoreUtils.Cp;

/// <summary>
/// Provides the <c>cp</c> process entry point. Usage: <c>cp [OPTION]... SOURCE... DEST</c>.
/// </summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The process exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args ).AsTask();

	/// <summary>Writes the command usage synopsis.</summary>
	/// <param name="writer">The destination writer.</param>
	public static void WriteUsage( TextWriter writer ) {
		ArgumentNullException.ThrowIfNull( writer );
		writer.WriteLine( "Usage: cp [OPTION]... SOURCE... DEST" );
	}
}
