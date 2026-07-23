// Minimal port of the UNIX `stdbuf` utility (best-effort).
namespace Icod.CoreUtils.StdBuf;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}