// Port of the standard UNIX `sha1sum` utility to .NET
namespace Icod.CoreUtils.Sha1Sum;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
