// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Rmdir;

using System;
using System.IO;

/// <summary>
/// rmdir: remove empty directories. Supports -p to remove directory and its ancestors if they become empty.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		var parents = false;
		var rem = new System.Collections.Generic.List<string>();
		foreach ( var a in args ) {
			if ( a == "-p" ) {
				parents = true;
			} else {
				rem.Add( a );
			}
		}

		if ( rem.Count == 0 ) {
			stderr.WriteLine( "rmdir: missing operand" );
			return 1;
		}

		var exit = 0;
		foreach ( var d in rem ) {
			try {
				if ( !Directory.Exists( d ) ) {
					stderr.WriteLine( $"rmdir: failed to remove '{d}': No such file or directory" );
					exit = 1;
					continue;
				}

				Directory.Delete( d );
				if ( parents ) {
					var parent = Path.GetDirectoryName( d );
					while ( !string.IsNullOrEmpty( parent ) && Directory.Exists( parent ) && Directory.GetFileSystemEntries( parent ).Length == 0 ) {
						Directory.Delete( parent );
						parent = Path.GetDirectoryName( parent );
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"rmdir: {d}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
