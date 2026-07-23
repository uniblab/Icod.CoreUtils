// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Expand;

using System;
using System.IO;
using System.Text;

/// <summary>
/// expand: convert tabs to spaces.
/// Options:
///   -t N	set tab stop width (default 8)
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
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
					stderr.WriteLine( $"expand: invalid tab width '{args[ i ]}'" );
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
			return ProcessReader( "<stdin>", Console.In, stdout, stderr, tabWidth );
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				var rc = ProcessReader( path, sr, stdout, stderr, tabWidth );
				if ( rc != 0 ) {
					exit = rc;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"expand: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int ProcessReader( string sourceName, TextReader reader, TextWriter stdout, TextWriter stderr, int tabWidth ) {
		try {
			string? line;
			while ( ( line = reader.ReadLine() ) is not null ) {
				stdout.WriteLine( ExpandLine( line, tabWidth ) );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"expand: {sourceName}: {ex.Message}" );
			return 1;
		}
	}

	private static string ExpandLine( string line, int tabWidth ) {
		var sb = new StringBuilder();
		var col = 0;
		foreach ( var c in line ) {
			if ( c == '\t' ) {
				var spaces = tabWidth - ( col % tabWidth );
				for ( var i = 0; i < spaces; i++ ) {
					sb.Append( ' ' );
				}

				col += spaces;
			} else {
				sb.Append( c );
				col++;
			}
		}

		return sb.ToString();
	}
}
