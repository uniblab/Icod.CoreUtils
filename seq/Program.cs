// Port of the standard UNIX `seq` utility (minimal feature set).
namespace Icod.CoreUtils.Seq;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}