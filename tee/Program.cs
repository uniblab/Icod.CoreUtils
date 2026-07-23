// Port of the standard UNIX `tee` utility to .NET
namespace Icod.CoreUtils.Tee;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}