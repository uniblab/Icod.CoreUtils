// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Head;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// head: output the first part of files.
/// Supports:
///   -n N, -n=NUM, -nNUM           print first NUM lines (default 10)
///   -n -NUM, -n=-NUM, -n-NUM      with leading '-', print all but the last NUM lines
/// Example:
///   head -n -15   # print all but last 15 lines
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var lines = 10;
		var allBut = false;
		var tailDiscard = 0;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a.StartsWith( "-n" ) ) {
				string val;
				if ( a == "-n" ) {
					if ( i + 1 >= args.Length ) {
						stderr.WriteLine( "head: option requires an argument -- 'n'" );
						return 1;
					}
					val = args[ ++i ];
				} else if ( a.StartsWith( "-n=" ) ) {
					val = a.Substring( 3 );
				} else {
					val = a.Substring( 2 );
				}

				if ( val.StartsWith( "=" ) )
					val = val.Substring( 1 );

				if ( val.StartsWith( "-" ) ) {
					// all but last NUM lines
					allBut = true;
					var numText = val.Substring( 1 );
					if ( !int.TryParse( numText, out tailDiscard ) || tailDiscard < 0 ) {
						stderr.WriteLine( $"head: invalid number '{val}'" );
						return 1;
					}
				} else {
					if ( !int.TryParse( val, out lines ) || lines < 0 ) {
						stderr.WriteLine( $"head: invalid number '{val}'" );
						return 1;
					}
				}

				continue;
			}

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
					if ( allBut ) {
						exit = OutputHeadAllBut( "<stdin>", stdin ?? Console.In, stdout, stderr, tailDiscard );
					} else {
						exit = OutputHead( "<stdin>", stdin ?? Console.In, stdout, stderr, lines );
					}
				} else {
					using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
					if ( allBut ) {
						var rc = OutputHeadAllBut( path, sr, stdout, stderr, tailDiscard );
						if ( rc != 0 )
							exit = rc;
					} else {
						var rc = OutputHead( path, sr, stdout, stderr, lines );
						if ( rc != 0 )
							exit = rc;
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"head: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int OutputHead( string sourceName, TextReader reader, TextWriter stdout, TextWriter stderr, int lines ) {
		try {
			var count = 0;
			string? line;
			while ( count < lines && ( line = reader.ReadLine() ) is not null ) {
				stdout.WriteLine( line );
				count++;
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"head: {sourceName}: {ex.Message}" );
			return 1;
		}
	}

	// Print all but the last `discard` lines by buffering a sliding window of size `discard`.
	private static int OutputHeadAllBut( string sourceName, TextReader reader, TextWriter stdout, TextWriter stderr, int discard ) {
		try {
			if ( discard <= 0 ) {
				// nothing to discard -> print everything
				string? line;
				while ( ( line = reader.ReadLine() ) is not null )
					stdout.WriteLine( line );
				return 0;
			}

			var buffer = new Queue<string>();
			string? ln;
			while ( ( ln = reader.ReadLine() ) is not null ) {
				buffer.Enqueue( ln );
				if ( buffer.Count > discard ) {
					stdout.WriteLine( buffer.Dequeue() );
				}
			}

			// remaining `discard` lines in buffer are skipped
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"head: {sourceName}: {ex.Message}" );
			return 1;
		}
	}
}