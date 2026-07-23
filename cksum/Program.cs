// Port of the standard UNIX `cksum` utility to .NET (CRC32).
namespace Icod.CoreUtils.Cksum;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
