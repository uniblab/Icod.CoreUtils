namespace Icod.CoreUtils.Expr;

using Icod.CommandFramework.Diagnostics;

/// <summary>Provides the executable entry point for <c>expr</c>.</summary>
public static class Program {
	/// <summary>Runs <c>expr</c> with the process console streams.</summary>
	/// <param name="args">The expression tokens.</param>
	/// <returns>The command exit status.</returns>
	public static Task<int> Main( string[] args ) {
		return Command.RunAsync(
			args,
			CommandContext.CreateConsole( "expr" )
		);
	}
}
