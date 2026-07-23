// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.MD5Sum;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Compute MD5 checksums for files or standard input.
/// </summary>
public static class Command {
	private static string ToHex( ReadOnlySpan<byte> bytes ) {
		var sb = new StringBuilder( bytes.Length * 2 );
		foreach ( var b in bytes )
			sb.Append( b.ToString( "x2" ) );
		return sb.ToString();
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			try {
				using var ms = new MemoryStream();
				var buffer = new byte[ 8192 ];
				int read;
				using var input = Console.OpenStandardInput();
				while ( ( read = input.Read( buffer, 0, buffer.Length ) ) > 0 )
					ms.Write( buffer, 0, read );
				var hash = ComputeMd5( ms.ToArray() );
				stdout.WriteLine( $"{hash}  -" );
				return 0;
			} catch ( Exception ex ) {
				stderr.WriteLine( $"md5sum: {ex.Message}" );
				return 1;
			}
		}

		var exitCode = 0;
		foreach ( var name in args ) {
			if ( name == "-" ) {
				try {
					using var ms = new MemoryStream();
					var buffer = new byte[ 8192 ];
					int read;
					using var input = Console.OpenStandardInput();
					while ( ( read = input.Read( buffer, 0, buffer.Length ) ) > 0 )
						ms.Write( buffer, 0, read );
					var hash = ComputeMd5( ms.ToArray() );
					stdout.WriteLine( $"{hash}  -" );
				} catch ( Exception ex ) {
					stderr.WriteLine( $"md5sum: -: {ex.Message}" );
					exitCode = 1;
				}
				continue;
			}

			try {
				var data = File.ReadAllBytes( name );
				var hash = ComputeMd5( data );
				stdout.WriteLine( $"{hash}  {name}" );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"md5sum: {name}: {ex.Message}" );
				exitCode = 1;
			}
		}
		return exitCode;
	}

	private static string ComputeMd5( byte[] data ) {
		var hash = MD5.HashData( data );
		return ToHex( hash );
	}
}
