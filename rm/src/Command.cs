// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Rm;

using System;
using System.IO;

/// <summary>
/// rm: remove files; -r recursive; -f ignore errors.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		var recursive = false;
		var force = false;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( !args[ i ].StartsWith( '-' ) ) {
				break;
			}

			if ( args[ i ].Contains( 'r' ) ) {
				recursive = true;
			}

			if ( args[ i ].Contains( 'f' ) ) {
				force = true;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			stderr.WriteLine( "rm: missing operand" );
			return 1;
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				if ( Directory.Exists( path ) ) {
					if ( !recursive ) {
						stderr.WriteLine( $"rm: cannot remove '{path}': Is a directory" );
						exit = 1;
						continue;
					}

					Directory.Delete( path, recursive: true );
				} else if ( File.Exists( path ) ) {
					File.Delete( path );
				} else {
					if ( !force ) {
						stderr.WriteLine( $"rm: cannot remove '{path}': No such file or directory" );
						exit = 1;
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"rm: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
