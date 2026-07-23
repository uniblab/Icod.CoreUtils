// Port of the standard UNIX `base64` utility to .NET
namespace Icod.CoreUtils.Base64;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
