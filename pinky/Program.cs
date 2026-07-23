// Minimal port of the UNIX `pinky` utility (best-effort).
namespace Icod.CoreUtils.Pinky;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}