namespace Icod.CoreUtils.Touch;

/// <summary>
/// Hosts <c>touch</c>. Usage: <c>touch [OPTION]... FILE...</c>.
/// </summary>
public static class Program {
	/// <summary>Runs the command-line entry point.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );

	/// <summary>Writes the command usage text.</summary>
	/// <param name="writer">The destination writer.</param>
	/// <returns>A task representing the asynchronous write.</returns>
	public static Task WriteUsageAsync( TextWriter writer ) {
		ArgumentNullException.ThrowIfNull( writer );
		return writer.WriteLineAsync( "Usage: touch [OPTION]... FILE..." );
	}
}
