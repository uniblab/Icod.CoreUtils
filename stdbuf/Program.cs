namespace Icod.CoreUtils.StdBuf;

using System;

internal static class Program {
	public static async Task<int> Main( string[] args ) {
		return await Command.RunAsync(
			args,
			Console.In,
			Console.Out,
			Console.Error
		).ConfigureAwait( false );
	}
}
