// Port of the standard UNIX `unlink` utility to .NET
namespace Icod.CoreUtils.Unlink;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}