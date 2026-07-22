// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Readlink;

using System;
using System.IO;
using System.Reflection;
using System.Text;

/// <summary>
/// readlink: print value of a symbolic link.
/// Best-effort: uses FileSystemInfo 'LinkTarget' property when available via reflection.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			stderr.WriteLine( "readlink: missing operand" );
			return 1;
		}

		var exit = 0;
		foreach ( var path in args ) {
			try {
				var fsi = new FileInfo( path );
				var prop = fsi.GetType().GetProperty( "LinkTarget", BindingFlags.Instance | BindingFlags.Public );
				if ( prop is null ) {
					stderr.WriteLine( "readlink: LinkTarget not available on this platform" );
					throw new NotImplementedException( "readlink requires runtime support for symlink introspection." );
				}

				var val = prop.GetValue( fsi ) as string;
				if ( string.IsNullOrEmpty( val ) ) {
					stderr.WriteLine( $"readlink: {path}: not a symbolic link or no target" );
					exit = 1;
				} else {
					stdout.WriteLine( val );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"readlink: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
