// Port of the standard UNIX `nice` utility to .NET (best-effort).
namespace Icod.CoreUtils.Nice;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}