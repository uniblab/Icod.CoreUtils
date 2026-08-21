namespace Icod.CoreUtils.Test;

using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Provides the <c>test EXPRESSION</c> command entry point. The command exits 0 when the expression is true,
/// 1 when it is false, and 2 for expression syntax errors.
/// </summary>
public static class Program {
	/// <summary>Runs the <c>test</c> command.</summary>
	/// <param name="args">The expression operands and operators.</param>
	/// <returns>The command exit status.</returns>
	public static async Task<int> Main( string[] args ) {
		return await Command.RunAsync(
			args,
			CommandContext.CreateConsole( "test" )
		).ConfigureAwait( false );
	}

	/// <summary>Writes the command usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the usage text has been written.</returns>
	internal static ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) => Command.WriteUsageAsync( output, cancellationToken );
}
