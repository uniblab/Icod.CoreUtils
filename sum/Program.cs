// Port of the standard UNIX `sum` utility (BSD-like 16-bit checksum).
namespace Icod.CoreUtils.Sum;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
