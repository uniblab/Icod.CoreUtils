// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Unexpand;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// unexpand: convert spaces to tabs.
/// Options:
///   -t N   tab stops (default 8)
/// Reads files or stdin.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var tabWidth = 8;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( !args[ i ].StartsWith( '-' ) ) {
				break;
			}

			if ( args[ i ] == "-t" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out tabWidth ) ) {
					stderr.WriteLine( $"unexpand: invalid tab width '{args[ i ]}'" );
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
		foreach ( var p in rem ) {
			try {
				using var r = p == "-" ? stdin! : new StreamReader( p, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				string? line;
				while ( ( line = r.ReadLine() ) is not null ) {
					stdout.WriteLine( UnexpandLine( line, tabWidth ) );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"unexpand: {p}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static string UnexpandLine( string line, int tabWidth ) {
		if ( string.IsNullOrEmpty( line ) ) {
			return line;
		}

		var sb = new StringBuilder();
		var spaceCount = 0;
		var col = 0;
		for ( var i = 0; i < line.Length; i++ ) {
			var c = line[ i ];
			if ( c == ' ' ) {
				spaceCount++;
				col++;
				if ( col % tabWidth == 0 ) {
					sb.Append( '\t' );
					spaceCount = 0;
				}
			} else {
				if ( spaceCount > 0 ) {
					for ( var j = 0; j < spaceCount; j++ ) {
						sb.Append( ' ' );
					}
					spaceCount = 0;
				}

				sb.Append( c );
				if ( c == '\t' ) {
					col += tabWidth - ( col % tabWidth );
				} else {
					col++;
				}
			}
		}

		// trailing spaces
		for ( var j = 0; j < spaceCount; j++ ) {
			sb.Append( ' ' );
		}

		return sb.ToString();
	}
}
