// Port of the standard UNIX `echo` utility (minimal).
namespace Icod.CoreUtils.Echo;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
