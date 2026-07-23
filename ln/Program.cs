// Port of the standard UNIX `ln` utility to .NET
namespace Icod.CoreUtils.Ln;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}