// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.LogName;

using System;
using System.IO;

/// <summary>
/// Prints the user's login name. Best-effort: uses Environment.UserName.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= System.Console.In;
		stdout ??= System.Console.Out;
		stderr ??= System.Console.Error;

		if ( args.Length > 0 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		try {
			var name = Environment.UserName ?? string.Empty;
			stdout.WriteLine( name );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"logname: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: logname" );
	}
}
