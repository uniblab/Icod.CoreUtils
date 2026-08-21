namespace Icod.CoreUtils.Mkdir;

using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Provides the <c>mkdir [OPTION]... DIRECTORY...</c> command entry point.
/// </summary>
public static class Program {
	/// <summary>Runs the <c>mkdir</c> command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static async Task<int> Main( string[] args ) {
		return await Command.RunAsync(
			args,
			CommandContext.CreateConsole( "mkdir" )
		).ConfigureAwait( false );
	}

	/// <summary>Writes the command usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	internal static ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) => Command.WriteUsageAsync( output, cancellationToken );
}
