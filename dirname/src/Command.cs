// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Dirname;

using System;
using System.IO;

/// <summary>
/// dirname: strip the last component from NAME, print directory portion.
/// Usage: dirname NAME
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			stderr.WriteLine( "dirname: missing operand" );
			return 1;
		}

		foreach ( var name in args ) {
			try {
				var dir = GetDirname( name );
				stdout.WriteLine( dir );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"dirname: {name}: {ex.Message}" );
				return 1;
			}
		}

		return 0;
	}

	private static string GetDirname( string path ) {
		if ( string.IsNullOrEmpty( path ) ) {
			return ".";
		}

		var trimmed = path.TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
		if ( trimmed.Length == 0 ) {
			return Path.DirectorySeparatorChar.ToString();
		}

		var dir = Path.GetDirectoryName( trimmed );
		if ( string.IsNullOrEmpty( dir ) ) {
			return ".";
		}

		return dir;
	}
}
