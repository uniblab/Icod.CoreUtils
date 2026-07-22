// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Pr;

using System;
using System.IO;
using System.Text;

/// <summary>
/// pr: paginate or columnate files. Simplified:
/// - supports -l lines-per-page (default 66) and -h header.
/// - prints pages with header showing file name and date.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var linesPerPage = 66;
		var header = string.Empty;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( args[ i ] == "-l" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out linesPerPage ) ) {
					stderr.WriteLine( $"pr: invalid lines value '{args[ i ]}'" );
					return 1;
				}
			} else if ( args[ i ] == "-h" && i + 1 < args.Length ) {
				i++;
				header = args[ i ];
			} else {
				break;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			rem.Add( "-" );
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				using var r = path == "-" ? Console.In : new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				var rc = OutputPages( path, r, stdout, stderr, linesPerPage, header );
				if ( rc != 0 ) {
					exit = rc;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"pr: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int OutputPages( string sourceName, TextReader reader, TextWriter stdout, TextWriter stderr, int linesPerPage, string header ) {
		try {
			var page = 1;
			var lineCount = 0;
			var title = string.IsNullOrEmpty( header ) ? sourceName : header;
			var date = DateTime.Now.ToString( "MMM dd yyyy", System.Globalization.CultureInfo.InvariantCulture );
			string? line;
			while ( ( line = reader.ReadLine() ) is not null ) {
				if ( lineCount == 0 ) {
					stdout.WriteLine( $" {title} {date} Page {page}" );
				}

				stdout.WriteLine( line );
				lineCount++;
				if ( lineCount >= linesPerPage ) {
					page++;
					lineCount = 0;
				}
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"pr: {sourceName}: {ex.Message}" );
			return 1;
		}
	}
}
