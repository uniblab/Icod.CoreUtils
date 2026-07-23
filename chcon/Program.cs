namespace Icod.CoreUtils.Chcon;

using System;

public static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}