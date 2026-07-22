// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Dir;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// dir: list directory contents (simple implementation).
/// Supported options:
///   -a    include entries beginning with '.' (hidden)
///   -1    list one entry per line
///   -? --help  display help
/// Usage:
///   dir [OPTION]... [FILE]...
/// If no FILE is given, list the current directory.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var showAll = false;
		var onePerLine = false;
		var paths = new List<string>();

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			switch ( a ) {
				case "-a":
					showAll = true;
					break;
				case "-1":
					onePerLine = true;
					break;
				case "-?":
				case "--help":
					PrintUsage( stdout );
					return 0;
				default:
					paths.Add( a );
					break;
			}
		}

		if ( paths.Count == 0 ) {
			paths.Add( "." );
		}

		try {
			foreach ( var p in paths ) {
				var entries = new List<string>();
				if ( Directory.Exists( p ) ) {
					var opts = new EnumerationOptions { RecurseSubdirectories = false };
					foreach ( var e in Directory.EnumerateFileSystemEntries( p ) ) {
						var name = Path.GetFileName( e ) ?? e;
						if ( !showAll && name.StartsWith( "." ) ) {
							continue;
						}
						entries.Add( name );
					}
				} else if ( File.Exists( p ) ) {
					entries.Add( Path.GetFileName( p )! );
				} else {
					stderr.WriteLine( $"dir: cannot access '{p}': No such file or directory" );
					continue;
				}

				entries.Sort( StringComparer.Ordinal );

				if ( onePerLine ) {
					foreach ( var e in entries ) {
						stdout.WriteLine( e );
					}
				} else {
					// Print space separated in a single line
					stdout.WriteLine( string.Join( "  ", entries ) );
				}
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"dir: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: dir [OPTION]... [FILE]..." );
		stdout.WriteLine( "  -a    include entries starting with '.'" );
		stdout.WriteLine( "  -1    list one entry per line" );
		stdout.WriteLine( "  -?, --help    display this help and exit" );
	}
}
