// Port of the standard UNIX `env` utility (minimal).
namespace Icod.CoreUtils.Env;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
