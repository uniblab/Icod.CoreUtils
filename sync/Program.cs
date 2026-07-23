// Minimal port of the UNIX `sync` utility (best-effort).
namespace Icod.CoreUtils.Sync;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}