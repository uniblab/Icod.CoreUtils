// Port of the standard UNIX `tsort` utility to .NET
namespace Icod.CoreUtils.Tsort;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}