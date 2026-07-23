// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Base32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// RFC 4648 Base32 encode/decode (minimal, common options).
/// Usage: base32 [-d] [FILE]...
/// With no FILE or when FILE is -, read standard input.
/// </summary>
public static class Command {
	private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

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
					var decoded = DecodeBase32( s );
					stdout.Write( Encoding.UTF8.GetString( decoded ) );
				} else {
					var encoded = EncodeBase32( data );
					stdout.WriteLine( encoded );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"base32: {name}: {ex.Message}" );
				exitCode = 1;
			}
		}
		return exitCode;
	}

	private static string EncodeBase32( byte[] data ) {
		if ( data.Length == 0 )
			return string.Empty;
		var bits = 0;
		var value = 0;
		var sb = new StringBuilder( ( data.Length + 4 ) / 5 * 8 );
		foreach ( var b in data ) {
			value = ( value << 8 ) | b;
			bits += 8;
			while ( bits >= 5 ) {
				bits -= 5;
				var index = ( value >> bits ) & 0x1F;
				sb.Append( Alphabet[ index ] );
			}
		}
		if ( bits > 0 ) {
			var index = ( value << ( 5 - bits ) ) & 0x1F;
			sb.Append( Alphabet[ index ] );
		}
		// padding to a multiple of 8 characters with '='
		while ( sb.Length % 8 != 0 )
			sb.Append( '=' );
		return sb.ToString();
	}

	private static byte[] DecodeBase32( string s ) {
		var clean = s.Trim().TrimEnd( '=' ).ToUpperInvariant();
		var buffer = new List<byte>();
		var bits = 0;
		var value = 0;
		foreach ( var ch in clean ) {
			var idx = Alphabet.IndexOf( ch );
			if ( idx < 0 )
				continue; // ignore non-alphabet chars (whitespace)
			value = ( value << 5 ) | idx;
			bits += 5;
			if ( bits >= 8 ) {
				bits -= 8;
				var b = (byte)( ( value >> bits ) & 0xFF );
				buffer.Add( b );
			}
		}
		return buffer.ToArray();
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: base32 [-d] [FILE]..." );
		stdout.WriteLine( "  -d    decode data" );
	}
}
