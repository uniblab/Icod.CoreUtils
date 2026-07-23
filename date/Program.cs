// Port of the standard UNIX `date` utility (minimal).
namespace Icod.CoreUtils.Date;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
