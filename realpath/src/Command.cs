// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Realpath;

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// realpath: canonicalize by following every symlink in every component of the given path (best-effort).
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			stderr.WriteLine( "realpath: missing operand" );
			return 1;
		}

		var exit = 0;
		foreach ( var path in args ) {
			try {
				var resolved = ResolvePath( path );
				stdout.WriteLine( resolved );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"realpath: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static string ResolvePath( string path ) {
		try {
			var current = Path.GetFullPath( path );
			// Best-effort: attempt to resolve symbolic links by following LinkTarget if available
			var parts = new List<string>();
			var di = new DirectoryInfo( current );
			var prop = typeof( FileSystemInfo ).GetProperty( "LinkTarget", BindingFlags.Instance | BindingFlags.Public );
			if ( prop is null ) {
				return current;
			}

			// Walk components and replace symlinks
			var segments = current.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
			var accum = segments.Length > 0 && !string.IsNullOrEmpty( segments[ 0 ] ) ? segments[ 0 ] : Path.DirectorySeparatorChar.ToString();
			for ( var i = 1; i < segments.Length; i++ ) {
				accum = Path.Combine( accum, segments[ i ] );
				var fsi = new FileInfo( accum );
				var linkVal = prop.GetValue( fsi ) as string;
				if ( !string.IsNullOrEmpty( linkVal ) ) {
					accum = Path.GetFullPath( Path.Combine( Path.GetDirectoryName( accum ) ?? string.Empty, linkVal ) );
				}
			}

			return accum;
		} catch {
			return path;
		}
	}
}
