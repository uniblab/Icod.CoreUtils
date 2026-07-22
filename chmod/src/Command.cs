// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chmod;

using System;
using System.IO;
using System.Globalization;

/// <summary>
/// chmod: change file mode bits (best-effort).
/// Supports numeric octal modes to toggle the owner write bit (maps to FileAttributes.ReadOnly on Windows).
/// Full POSIX mode semantics are not available via BCL and are approximated where possible.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		if ( args.Length < 2 ) {
			stderr.WriteLine( "chmod: missing operand" );
			return 1;
		}

		var modeStr = args[ 0 ];
		var rem = new System.Collections.Generic.List<string>();
		for ( var i = 1; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( !TryParseOctalMode( modeStr, out var mode ) ) {
			stderr.WriteLine( $"chmod: invalid mode: '{modeStr}'" );
			return 1;
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				if ( !File.Exists( path ) && !Directory.Exists( path ) ) {
					stderr.WriteLine( $"chmod: cannot access '{path}': No such file or directory" );
					exit = 1;
					continue;
				}

				// Best-effort mapping: if owner write bit is off => set ReadOnly attribute
				var ownerWrite = ( mode & 0b_100_000_000 ) != 0; // owner write bit in full 9-bit representation, but user passes 3-digit octal; handle simple mapping below
																 // Simpler: interpret last 3 octal digits: owner perms are (mode >> 6) & 7
				var ownerPerms = ( mode >> 6 ) & 7;
				var ownerCanWrite = ( ownerPerms & 2 ) != 0;

				var attr = File.GetAttributes( path );
				if ( ownerCanWrite ) {
					attr &= ~FileAttributes.ReadOnly;
				} else {
					attr |= FileAttributes.ReadOnly;
				}

				File.SetAttributes( path, attr );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"chmod: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static bool TryParseOctalMode( string s, out int mode ) {
		mode = 0;
		try {
			s = s.Trim();
			if ( s.StartsWith( '0' ) ) {
				s = s.TrimStart( '0' );
			}

			// Accept 3-digit octal like 755
			if ( int.TryParse( s, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed ) ) {
				// Validate digits are octal
				foreach ( var ch in s ) {
					if ( ch < '0' || ch > '7' ) {
						return false;
					}
				}

				mode = parsed;
				return true;
			}

			return false;
		} catch {
			return false;
		}
	}
}
