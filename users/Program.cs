// Minimal port of the UNIX `users` utility to .NET (best-effort).
namespace Icod.CoreUtils.Users;

using System;

internal static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}