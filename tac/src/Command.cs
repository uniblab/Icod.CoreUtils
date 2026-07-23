// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tac;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// `tac` prints files in reverse line order. Supports reading '-' for stdin
/// and multiple files; prints each file's lines reversed in order.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 1 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		var files = new List<string>();
		if ( args.Length == 0 )
			files.Add( "-" ); // read stdin by default
		else
			files.AddRange( args );

		var exitCode = 0;
		foreach ( var name in files ) {
			try {
				string[] lines;
				if ( name == "-" ) {
					var content = stdin.ReadToEnd();
					lines = SplitLines( content );
				} else {
					lines = File.ReadAllLines( name, Encoding.UTF8 );
				}

				for ( var i = lines.Length - 1; i >= 0; i-- ) {
					stdout.WriteLine( lines[ i ] );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"tac: {name}: {ex.Message}" );
				exitCode = 1;
			}
		}

		return exitCode;
	}

	private static string[] SplitLines( string s ) {
		// Preserve behavior similar to File.ReadAllLines
		var list = new List<string>();
		using var sr = new StringReader( s );
		string? line;
		while ( ( line = sr.ReadLine() ) != null )
			list.Add( line );
		// If the input ends with a trailing newline, ReadLine will have consumed it and last line is empty as expected.
		return list.ToArray();
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: tac [FILE]..." );
		stdout.WriteLine( "Print each file to standard output in reverse line order." );
	}
}
