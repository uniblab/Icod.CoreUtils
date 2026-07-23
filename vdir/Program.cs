// Minimal port of the UNIX `vdir` utility to .NET (prints long listing similar to `ls -l`).
namespace Icod.CoreUtils.Vdir;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}