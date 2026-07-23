// Port of the standard UNIX `sha224sum` utility to .NET (best-effort).
namespace Icod.CoreUtils.Sha224Sum;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}