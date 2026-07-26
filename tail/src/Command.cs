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
/// Supports:
///		-n N (lines; default 10)
///		-nNUM, -n=NUM, -n NUM		print last NUM lines (default 10)
///		-n +NUM, -n=+NUM, -n+NUM	start printing with line NUM (skip NUM-1 lines)
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var lines = 10;
		var startAt = 0; // if >0 then print starting at this 1-based line
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-?" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			} else if ( a.StartsWith( "-n" ) ) {
				string val;
				if ( a == "-n" ) {
					if ( i + 1 >= args.Length ) {
						stderr.WriteLine( "tail: option requires an argument -- 'n'" );
						return 1;
					}
					val = args[ ++i ];
				} else if ( a.StartsWith( "-n=" ) ) {
					val = a.Substring( 3 );
				} else {
					// -nNUM or -n+NUM etc.
					val = a.Substring( 2 );
				}

				// strip optional leading '=' if present
				if ( val.StartsWith( "=" ) )
					val = val.Substring( 1 );

				// +NUM means start at NUM
				if ( val.Length > 0 && val[ 0 ] == '+' ) {
					if ( !int.TryParse( val.Substring( 1 ), out startAt ) || startAt < 1 ) {
						stderr.WriteLine( $"tail: invalid number '{val}'" );
						return 1;
					}
				} else {
					// treat as last-N lines
					if ( !int.TryParse( val, out lines ) || lines < 0 ) {
						stderr.WriteLine( $"tail: invalid number '{val}'" );
						return 1;
					}
				}

				continue;
			}

			// not an option: stop option parsing
			break;
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
				if ( path == "-" ) {
					// stream from stdin
					if ( startAt > 0 ) {
						// skip startAt-1 lines, then print rest
						var cnt = 1;
						string? line;
						while ( ( line = stdin.ReadLine() ) is not null ) {
							if ( cnt >= startAt )
								stdout.WriteLine( line );
							cnt++;
						}
					} else {
						// need last `lines` lines: read all into buffer
						var buffer = new Queue<string>();
						string? line;
						while ( ( line = stdin.ReadLine() ) is not null ) {
							buffer.Enqueue( line );
							if ( buffer.Count > lines )
								buffer.Dequeue();
						}
						foreach ( var l in buffer )
							stdout.WriteLine( l );
					}
				} else {
					// file input
					if ( startAt > 0 ) {
						// stream and skip initial lines
						using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
						var cnt = 1;
						string? line;
						while ( ( line = sr.ReadLine() ) is not null ) {
							if ( cnt >= startAt )
								stdout.WriteLine( line );
							cnt++;
						}
					} else {
						// efficient case: read lines lazily but we need count; use File.ReadLines then Take/Skip
						var source = File.ReadLines( path, Encoding.UTF8 );
						// materialize only as needed
						var temp = source as IList<string> ?? source.ToList();
						var startIndex = Math.Max( 0, temp.Count - lines );
						foreach ( var l in temp.Skip( startIndex ) )
							stdout.WriteLine( l );
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"tail: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
	/// <summary>
	/// tail: output the last part of files.
	/// Supports:
	///		-n N (lines; default 10)
	///		-nNUM, -n=NUM, -n NUM		print last NUM lines (default 10)
	///		-n +NUM, -n=+NUM, -n+NUM	start printing with line NUM (skip NUM-1 lines)
	/// </summary>
	private static void PrintUsage( TextWriter writer ) {
		writer.WriteLine( "Usage: tail [(-n N) | (-n +NUM)] [file ...]" );
		writer.WriteLine( "  -?, --help    display this help and exit" );
		writer.WriteLine( "  -n N          print last N lines (default 10)" );
		writer.WriteLine( "  -n +NUM       start printing with line NUM (skip NUM-1 lines)" );
	}
}