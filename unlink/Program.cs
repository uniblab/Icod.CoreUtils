namespace Icod.CoreUtils.Unlink;

using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Provides the <c>unlink FILE</c> command entry point.
/// </summary>
public static class Program {
	/// <summary>Runs the <c>unlink</c> command.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The command exit status.</returns>
	public static async Task<int> Main( string[] args ) {
		return await Command.RunAsync(
			args,
			CommandContext.CreateConsole( "unlink" )
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
