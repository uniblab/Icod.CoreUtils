namespace Icod.CoreUtils.Unlink;

using System;
using System.IO;

/// <summary>
/// Minimal `unlink` implementation: remove a single file name.
/// Matches typical UNIX behavior: requires exactly one operand and refuses to remove directories.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 1 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		if ( args.Length != 1 ) {
			stderr.WriteLine( "unlink: missing or extra operand" );
			stderr.WriteLine( "Try 'unlink --help' for more information." );
			return 2;
		}

		var path = args[ 0 ];

		try {
			// Do not remove directories; behave like POSIX unlink.
			if ( Directory.Exists( path ) && ( File.GetAttributes( path ) & FileAttributes.ReparsePoint ) == 0 ) {
				stderr.WriteLine( $"unlink: cannot unlink '{path}': Is a directory" );
				return 1;
			}

			if ( !File.Exists( path ) && !Directory.Exists( path ) ) {
				stderr.WriteLine( $"unlink: '{path}': No such file or directory" );
				return 1;
			}

			// Use File.Delete which also removes file symlinks; for junctions/directories above we already refused.
			File.Delete( path );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"unlink: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: unlink NAME" );
		stdout.WriteLine( "Remove the file NAME. This utility accepts exactly one operand." );
	}
}