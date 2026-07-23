namespace Icod.CoreUtils.Shred;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// shred: overwrite a file several times with random data then optionally delete.
/// Credit: Colin Plumb.
/// Usage: shred [-n passes] [-u] file
/// </summary>
public static class Command {
	public static int Run( string[] args, System.IO.TextReader? stdin = null, System.IO.TextWriter? stdout = null, System.IO.TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		if ( args.Length == 0 ) {
			stderr.WriteLine( "Usage: shred [-n passes] [-u] file" );
			return 2;
		}

		var passes = 3;
		var remove = false;
		var list = new System.Collections.Generic.List<string>();
		for ( var i = 0; i < args.Length; i++ ) {
			if ( args[ i ] == "-u" ) {
				remove = true;
				continue;
			}
			if ( args[ i ] == "-n" && i + 1 < args.Length ) {
				if ( int.TryParse( args[ i + 1 ], out var p ) ) {
					passes = p;
				}
				i++;
				continue;
			}
			list.Add( args[ i ] );
		}

		var exit = 0;
		foreach ( var f in list ) {
			try {
				if ( !File.Exists( f ) ) {
					stderr.WriteLine( $"shred: {f}: No such file" );
					exit = 1;
					continue;
				}
				var fi = new FileInfo( f );
				var length = fi.Length;
				using var rng = RandomNumberGenerator.Create();
				var buffer = new byte[ 8192 ];
				for ( var pass = 0; pass < passes; pass++ ) {
					using var fs = new FileStream( f, FileMode.Open, FileAccess.Write );
					fs.Seek( 0, SeekOrigin.Begin );
					var remaining = length;
					while ( remaining > 0 ) {
						rng.GetBytes( buffer );
						var toWrite = (int)Math.Min( buffer.Length, remaining );
						fs.Write( buffer, 0, toWrite );
						remaining -= toWrite;
					}

					fs.Flush( true );
				}

				if ( remove ) {
					File.Delete( f );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"shred: {f}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
