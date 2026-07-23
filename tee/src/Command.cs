// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tee;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Minimal `tee` implementation: writes stdin to stdout and to files.
/// Supports -a (append) and -h/--help.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var append = false;
		var files = new List<string>();
		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-a" ) {
				append = true;
				continue;
			}
			if ( a == "-h" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
			files.Add( a );
		}

		var writers = new List<StreamWriter>();
		try {
			foreach ( var f in files ) {
				var fs = new FileStream( f, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read );
				writers.Add( new StreamWriter( fs, Encoding.UTF8 ) { AutoFlush = true } );
			}

			string? line;
			while ( ( line = stdin.ReadLine() ) is not null ) {
				stdout.WriteLine( line );
				foreach ( var w in writers ) {
					w.WriteLine( line );
				}
			}
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"tee: {ex.Message}" );
			return 1;
		} finally {
			foreach ( var w in writers ) {
				try {
					w.Dispose();
				} catch { }
			}
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: tee [-a] [FILE]..." );
		stdout.WriteLine( "  -a    append to the given FILEs, do not overwrite" );
	}
}
