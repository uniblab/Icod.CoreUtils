// Port of the standard UNIX `tac` utility (reverse concatenate).
namespace Icod.CoreUtils.Tac;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}