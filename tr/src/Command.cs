// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tr;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// tr: translate or delete characters (simplified).
/// Supported modes:
///   -d SET	 delete characters in SET
///   SET1 SET2  translate characters from SET1 to SET2 (one-to-one)
///   -s		 squeeze repeated characters in output (after translation)
/// Note: character set expressions are treated as literal sequences; no ranges or classes.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var delete = false;
		var squeeze = false;
		var rem = new List<string>();
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( args[ i ] == "-d" ) {
				delete = true;
			} else if ( args[ i ] == "-s" ) {
				squeeze = true;
			} else {
				rem.Add( args[ i ] );
			}
		}

		try {
			if ( delete ) {
				if ( rem.Count < 1 ) {
					stderr.WriteLine( "tr: missing set for -d" );
					return 1;
				}

				var set = rem[ 0 ];
				var delSet = new HashSet<char>( set );
				string? line;
				while ( ( line = stdin.ReadLine() ) is not null ) {
					var sb = new StringBuilder();
					foreach ( var c in line ) {
						if ( !delSet.Contains( c ) ) {
							sb.Append( c );
						}
					}

					if ( squeeze ) {
						var outStr = Squeeze( sb.ToString() );
						stdout.WriteLine( outStr );
					} else {
						stdout.WriteLine( sb.ToString() );
					}
				}

				return 0;
			} else {
				if ( rem.Count < 2 ) {
					stderr.WriteLine( "tr: missing operand" );
					return 1;
				}

				var s1 = rem[ 0 ];
				var s2 = rem[ 1 ];
				var map = new Dictionary<char, char>();
				var len = Math.Min( s1.Length, s2.Length );
				for ( var j = 0; j < len; j++ ) {
					map[ s1[ j ] ] = s2[ j ];
				}

				if ( s2.Length > 0 ) {
					var last = s2[ ^1 ];
					for ( var j = len; j < s1.Length; j++ ) {
						map[ s1[ j ] ] = last;
					}
				}

				string? line;
				while ( ( line = stdin.ReadLine() ) is not null ) {
					var sb = new StringBuilder();
					foreach ( var c in line ) {
						if ( map.TryGetValue( c, out var nc ) ) {
							sb.Append( nc );
						} else {
							sb.Append( c );
						}
					}

					var outLine = squeeze ? Squeeze( sb.ToString() ) : sb.ToString();
					stdout.WriteLine( outLine );
				}

				return 0;
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"tr: {ex.Message}" );
			return 1;
		}
	}

	private static string Squeeze( string s ) {
		if ( string.IsNullOrEmpty( s ) ) {
			return s;
		}

		var sb = new StringBuilder();
		var prev = s[ 0 ];
		sb.Append( prev );
		for ( var i = 1; i < s.Length; i++ ) {
			if ( s[ i ] != prev ) {
				sb.Append( s[ i ] );
			}

			prev = s[ i ];
		}

		return sb.ToString();
	}
}
