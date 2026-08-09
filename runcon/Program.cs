namespace Icod.CoreUtils.RunCon;

using System;

/// <summary>Executable entry point for <c>runcon</c>.</summary>
public static class Program {
	/// <summary>Runs the command.</summary>
	public static int Main( string[] args ) {
		return Command.Run( args, Console.In, Console.Out, Console.Error );
	}
}
