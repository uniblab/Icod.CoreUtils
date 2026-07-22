// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Paste;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// paste: merge lines of files horizontally.
/// Usage: paste [-d delim] file...
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var delim = "\t";
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( !args[ i ].StartsWith( '-' ) ) {
				break;
			}

			if ( args[ i ] == "-d" && i + 1 < args.Length ) {
				i++;
				delim = args[ i ];
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

		var readers = new List<TextReader>();
		try {
			foreach ( var p in rem ) {
				if ( p == "-" ) {
					readers.Add( Console.In );
				} else {
					readers.Add( new StreamReader( p, Encoding.UTF8, detectEncodingFromByteOrderMarks: true ) );
				}
			}

			while ( true ) {
				var parts = new List<string>();
				var any = false;
				foreach ( var r in readers ) {
					var line = r.ReadLine();
					if ( line is not null ) {
						parts.Add( line );
						any = true;
					} else {
						parts.Add( string.Empty );
					}
				}

				if ( !any ) {
					break;
				}

				stdout.WriteLine( string.Join( delim, parts ) );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"paste: {ex.Message}" );
			return 1;
		} finally {
			foreach ( var r in readers ) {
				if ( r != Console.In ) {
					r.Dispose();
				}
			}
		}
	}
}
