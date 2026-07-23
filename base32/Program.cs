// Port of the standard UNIX `base32` utility to .NET
namespace Icod.CoreUtils.Base32;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
