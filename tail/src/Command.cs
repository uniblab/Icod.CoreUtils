// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tail;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// tail: output the last part of files.
/// Supports: -n N (lines; default 10)
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var lines = 10;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( args[ i ] == "-n" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out lines ) ) {
					stderr.WriteLine( $"tail: invalid number '{args[ i ]}'" );
					return 1;
				}
			} else {
				break;
			}
		}

		var rem = new List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			rem.Add( "-" );
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				IEnumerable<string> source;
				if ( path == "-" ) {
					var list = new List<string>();
					string? line;
					while ( ( line = stdin.ReadLine() ) is not null ) {
						list.Add( line );
					}

					source = list;
				} else {
					source = File.ReadLines( path, Encoding.UTF8 );
				}

				var temp = source as IList<string> ?? source.ToList();
				var outLines = temp.Skip( Math.Max( 0, temp.Count - lines ) );
				foreach ( var l in outLines ) {
					stdout.WriteLine( l );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"tail: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
