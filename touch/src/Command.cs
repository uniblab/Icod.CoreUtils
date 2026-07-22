// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Touch;

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// touch: update modification/access times or create files if missing.
/// Supports options: -a (access), -m (modification), -c (no create), -t [[CC]YY]MMDDhhmm[.ss]
/// Best-effort POSIX behavior using BCL only.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var updateAccess = false;
		var updateModification = false;
		var noCreate = false;
		DateTime? specifiedTime = null;
		var rem = new System.Collections.Generic.List<string>();
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( !args[ i ].StartsWith( '-' ) ) {
				break;
			}

			if ( args[ i ] == "-a" ) {
				updateAccess = true;
			} else if ( args[ i ] == "-m" ) {
				updateModification = true;
			} else if ( args[ i ] == "-c" ) {
				noCreate = true;
			} else if ( args[ i ].StartsWith( "-t" ) && args[ i ].Length > 2 ) {
				var t = args[ i ].Substring( 2 );
				specifiedTime = ParseTouchTimestamp( t );
			} else if ( args[ i ] == "-t" && i + 1 < args.Length ) {
				i++;
				specifiedTime = ParseTouchTimestamp( args[ i ] );
			} else {
				// ignore unknown
			}
		}

		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			stderr.WriteLine( "touch: missing file operand" );
			return 1;
		}

		if ( !updateAccess && !updateModification ) {
			updateAccess = true;
			updateModification = true;
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				var exists = File.Exists( path );
				if ( !exists ) {
					if ( noCreate ) {
						continue;
					}

					using var fs = File.Create( path );
				}

				var time = specifiedTime ?? DateTime.Now;
				if ( updateModification ) {
					File.SetLastWriteTime( path, time );
				}

				if ( updateAccess ) {
					File.SetLastAccessTime( path, time );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"touch: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static DateTime? ParseTouchTimestamp( string s ) {
		if ( string.IsNullOrEmpty( s ) ) {
			return null;
		}

		var patterns = new[]
		{
			"yyyyMMddHHmm.ss",
			"yyyyMMddHHmm",
			"yyMMddHHmm.ss",
			"yyMMddHHmm",
			"MMddHHmm.ss",
			"MMddHHmm"
		};

		foreach ( var p in patterns ) {
			if ( DateTime.TryParseExact( s, p, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt ) ) {
				return dt;
			}
		}

		if ( DateTime.TryParse( s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2 ) ) {
			return dt2;
		}

		return null;
	}
}
