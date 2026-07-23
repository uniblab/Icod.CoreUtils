// Port of the standard UNIX `sha384sum` utility to .NET
namespace Icod.CoreUtils.Sha384Sum;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}