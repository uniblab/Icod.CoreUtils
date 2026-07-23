// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Stat;

using System;
using System.IO;
using System.Globalization;

/// <summary>
/// stat: display file status (best-effort).
/// Prints file size, access/mod/change times and file type.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			stderr.WriteLine( "stat: missing operand" );
			return 1;
		}

		var exit = 0;
		foreach ( var path in args ) {
			try {
				if ( File.Exists( path ) ) {
					var fi = new FileInfo( path );
					stdout.WriteLine( $"  File: {path}" );
					stdout.WriteLine( $"  Size: {fi.Length}  Blocks: {( ( fi.Length + 511 ) / 512 ).ToString( CultureInfo.InvariantCulture )}" );
					stdout.WriteLine( $"  Access: {fi.LastAccessTime.ToString( "s", CultureInfo.InvariantCulture )}" );
					stdout.WriteLine( $"  Modify: {fi.LastWriteTime.ToString( "s", CultureInfo.InvariantCulture )}" );
					stdout.WriteLine( $"  Change: {fi.CreationTime.ToString( "s", CultureInfo.InvariantCulture )}" );
				} else if ( Directory.Exists( path ) ) {
					var di = new DirectoryInfo( path );
					stdout.WriteLine( $"  File: {path}" );
					stdout.WriteLine( $"  Size: 0" );
					stdout.WriteLine( $"  Access: {di.LastAccessTime.ToString( "s", CultureInfo.InvariantCulture )}" );
					stdout.WriteLine( $"  Modify: {di.LastWriteTime.ToString( "s", CultureInfo.InvariantCulture )}" );
					stdout.WriteLine( $"  Change: {di.CreationTime.ToString( "s", CultureInfo.InvariantCulture )}" );
				} else {
					stderr.WriteLine( $"stat: cannot stat '{path}': No such file or directory" );
					exit = 1;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"stat: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
