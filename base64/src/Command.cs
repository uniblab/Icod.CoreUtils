namespace Icod.CoreUtils.Base64;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Minimal base64 encoder/decoder. Usage:
///   base64 [-d] [FILE]...
/// With no FILE or when FILE is -, read standard input.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var decode = false;
		var files = new List<string>();
		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-d" ) {
				decode = true;
				continue;
			}
			if ( a == "-h" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
			files.Add( a );
		}

		if ( files.Count == 0 )
			files.Add( "-" );

		var exitCode = 0;
		foreach ( var name in files ) {
			try {
				byte[] data;
				if ( name == "-" ) {
					using var ms = new MemoryStream();
					var buf = new char[ 4096 ];
					int r;
					while ( ( r = stdin.Read( buf, 0, buf.Length ) ) > 0 )
						ms.Write( Encoding.UTF8.GetBytes( buf, 0, r ) );
					data = ms.ToArray();
				} else {
					data = File.ReadAllBytes( name );
				}

				if ( decode ) {
					var s = Encoding.UTF8.GetString( data ).Trim();
					var decoded = Convert.FromBase64String( s );
					stdout.Write( Encoding.UTF8.GetString( decoded ) );
				} else {
					var encoded = Convert.ToBase64String( data );
					stdout.WriteLine( encoded );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"base64: {name}: {ex.Message}" );
				exitCode = 1;
			}
		}
		return exitCode;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: base64 [-d] [FILE]..." );
		stdout.WriteLine( "  -d    decode data" );
	}
}
