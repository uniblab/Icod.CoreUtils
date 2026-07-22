// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Comm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// comm: compare two sorted files line by line and produce three-column output:
///   column 1: lines unique to file1
///   column 2: lines unique to file2
///   column 3: lines common to both
/// Supported options:
///   -1  suppress column 1
///   -2  suppress column 2
///   -3  suppress column 3
///   -?  display help
/// Behavior:
///   comm FILE1 FILE2
/// Use '-' for stdin. Input must be sorted for meaningful output (this implementation assumes input is sorted).
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var suppress1 = false;
		var suppress2 = false;
		var suppress3 = false;
		var files = new List<string>();

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			switch ( a ) {
				case "-1":
					suppress1 = true;
					break;
				case "-2":
					suppress2 = true;
					break;
				case "-3":
					suppress3 = true;
					break;
				case "-?":
				case "--help":
					PrintUsage( stdout );
					return 0;
				default:
					files.Add( a );
					break;
			}
		}

		if ( files.Count < 2 ) {
			stderr.WriteLine( "comm: two input files required" );
			PrintUsage( stderr );
			return 2;
		}

		var file1 = files[ 0 ];
		var file2 = files[ 1 ];

		try {
			using var r1 = OpenReader( file1, stdin );
			using var r2 = OpenReader( file2, stdin );

			var e1 = ReadLines( r1 ).GetEnumerator();
			var e2 = ReadLines( r2 ).GetEnumerator();

			var has1 = e1.MoveNext();
			var has2 = e2.MoveNext();

			while ( has1 || has2 ) {
				if ( has1 && has2 ) {
					var cmp = string.CompareOrdinal( e1.Current, e2.Current );
					if ( cmp == 0 ) {
						if ( !suppress3 ) {
							WriteCols( stdout, null, null, e1.Current, suppress1, suppress2, suppress3 );
						}
						has1 = e1.MoveNext();
						has2 = e2.MoveNext();
					} else if ( cmp < 0 ) {
						if ( !suppress1 ) {
							WriteCols( stdout, e1.Current, null, null, suppress1, suppress2, suppress3 );
						}
						has1 = e1.MoveNext();
					} else {
						if ( !suppress2 ) {
							WriteCols( stdout, null, e2.Current, null, suppress1, suppress2, suppress3 );
						}
						has2 = e2.MoveNext();
					}
				} else if ( has1 ) {
					if ( !suppress1 ) {
						WriteCols( stdout, e1.Current, null, null, suppress1, suppress2, suppress3 );
					}
					has1 = e1.MoveNext();
				} else {
					if ( !suppress2 ) {
						WriteCols( stdout, null, e2.Current, null, suppress1, suppress2, suppress3 );
					}
					has2 = e2.MoveNext();
				}
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"comm: {ex.Message}" );
			return 1;
		}
	}

	private static TextReader OpenReader( string path, TextReader? stdin ) {
		if ( path == "-" ) {
			return stdin ?? Console.In;
		}
		return new StreamReader( path, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
	}

	private static IEnumerable<string> ReadLines( TextReader reader ) {
		string? line;
		while ( ( line = reader.ReadLine() ) is not null ) {
			yield return line;
		}
	}

	private static void WriteCols( TextWriter w, string? col1, string? col2, string? col3, bool sup1, bool sup2, bool sup3 ) {
		// columns separated by single tab where preceding columns are present
		if ( col1 is not null ) {
			w.WriteLine( col1 );
			return;
		}
		if ( col2 is not null ) {
			w.WriteLine( $"\t{col2}" );
			return;
		}
		if ( col3 is not null ) {
			w.WriteLine( $"\t\t{col3}" );
			return;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: comm [-1] [-2] [-3] FILE1 FILE2" );
		stdout.WriteLine( "  -1    suppress column 1 (lines unique to FILE1)" );
		stdout.WriteLine( "  -2    suppress column 2 (lines unique to FILE2)" );
		stdout.WriteLine( "  -3    suppress column 3 (lines common to both)" );
		stdout.WriteLine( "  -?, --help    display this help and exit" );
	}
}
