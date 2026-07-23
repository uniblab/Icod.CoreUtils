namespace Icod.CoreUtils.Id;

using System;
using System.IO;

public static class Program {
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}