// Port of the standard UNIX `logname` utility to .NET
namespace Icod.CoreUtils.LogName;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}