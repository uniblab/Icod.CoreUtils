// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Dircolors;

using System;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// dircolors: output shell commands to set LS_COLORS from a database file.
/// This minimal implementation reads a dircolors-format file and emits:
///   export LS_COLORS='...'
/// If no file is specified, it tries ~/.dircolors then /etc/DIR_COLORS.
/// Supported options:
///   -b    output Bourne shell code (default)
///   -p    print database to stdout (raw)
///   -? --help  display help
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var printRaw = false;
		var path = string.Empty;

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			switch ( a ) {
				case "-p":
					printRaw = true;
					break;
				case "-b":
					// default, ignore
					break;
				case "-?":
				case "--help":
					PrintUsage( stdout );
					return 0;
				default:
					if ( string.IsNullOrEmpty( path ) ) {
						path = a;
					}
					break;
			}
		}

		var candidates = new[] {
			path,
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".dircolors"),
			"/etc/DIR_COLORS"
		}.Where( p => !string.IsNullOrEmpty( p ) ).ToArray();

		string? file = null;
		foreach ( var c in candidates ) {
			if ( File.Exists( c ) ) {
				file = c;
				break;
			}
		}

		if ( file is null ) {
			// no database found: output empty export
			stdout.WriteLine( "export LS_COLORS='''" );
			return 0;
		}

		try {
			var lines = File.ReadAllLines( file, Encoding.UTF8 );
			if ( printRaw ) {
				foreach ( var l in lines ) {
					stdout.WriteLine( l );
				}
				return 0;
			}

			var parts = lines
				.Select( l => l.Trim() )
				.Where( l => l.Length > 0 && !l.StartsWith( '#' ) )
				.ToArray();

			// join with colon to produce LS_COLORS string
			var sb = new StringBuilder();
			for ( var i = 0; i < parts.Length; i++ ) {
				if ( i > 0 )
					sb.Append( ':' );
				sb.Append( parts[ i ] );
			}

			stdout.WriteLine( $"export LS_COLORS='{sb.ToString()}'" );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"dircolors: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: dircolors [OPTION] [FILE]" );
		stdout.WriteLine( "  -b           output Bourne shell code (default)" );
		stdout.WriteLine( "  -p           print database to stdout (raw)" );
		stdout.WriteLine( "  -?, --help   display this help and exit" );
	}
}
