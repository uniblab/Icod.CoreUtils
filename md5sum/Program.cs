// Port of the standard UNIX `md5sum` utility to .NET
namespace Icod.CoreUtils.MD5Sum;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
