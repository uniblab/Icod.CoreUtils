// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Fold;

using System;
using System.IO;
using System.Text;

/// <summary>
/// fold: wrap each input line to fit a specified width (default 80).
/// Usage: fold [-w width] [file...]
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var width = 80;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( args[ i ] == "-w" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out width ) ) {
					stderr.WriteLine( $"fold: invalid width '{args[ i ]}'" );
					return 1;
				}
			} else {
				break;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			return ProcessReader( "<stdin>", Console.In, stdout, stderr, width );
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				var rc = ProcessReader( path, sr, stdout, stderr, width );
				if ( rc != 0 ) {
					exit = rc;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"fold: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int ProcessReader( string name, TextReader reader, TextWriter stdout, TextWriter stderr, int width ) {
		try {
			string? line;
			while ( ( line = reader.ReadLine() ) is not null ) {
				var pos = 0;
				while ( pos < line.Length ) {
					var take = Math.Min( width, line.Length - pos );
					stdout.WriteLine( line.AsSpan( pos, take ) );
					pos += take;
				}
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"fold: {name}: {ex.Message}" );
			return 1;
		}
	}
}
