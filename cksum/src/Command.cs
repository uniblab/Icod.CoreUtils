// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Cksum;

using System;
using System.IO;
using System.Text;

// TODO: expose as service in CoreUtils.Common
// TODO: augment with other checksums (MD5, SHA*, etc.)
/// <summary>
/// Compute CRC-32 (IEEE) checksum and length in bytes, similar to POSIX `cksum`.
/// </summary>
public static class Command {
	private static readonly uint[] Table = CreateTable();

	private static uint[] CreateTable() {
		const uint poly = 0xEDB88320u;
		var t = new uint[ 256 ];
		for ( uint i = 0; i < 256; i++ ) {
			var r = i;
			for ( var j = 0; j < 8; j++ ) {
				if ( ( r & 1 ) != 0 )
					r = ( r >> 1 ) ^ poly;
				else
					r >>= 1;
			}

			t[ i ] = r;
		}

		return t;
	}

	/// <summary>
	/// cksum: compute CRC-32 and file length. Outputs: "&lt;crc&gt; &lt;length&gt; &lt;filename&gt;"."
	/// </summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 1 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		var files = args.Length == 0 ? new[] { "-" } : args;
		var exitCode = 0;
		foreach ( var name in files ) {
			try {
				using var stream = name == "-" ? Console.OpenStandardInput() : File.OpenRead( name );
				var buffer = new byte[ 8192 ];
				long len = 0;
				uint crc = 0xFFFFFFFFu;
				int read;
				while ( ( read = stream.Read( buffer, 0, buffer.Length ) ) > 0 ) {
					len += read;
					for ( var i = 0; i < read; i++ ) {
						crc = ( crc >> 8 ) ^ Table[ ( crc ^ buffer[ i ] ) & 0xFF ];
					}
				}

				crc ^= 0xFFFFFFFFu;
				// cksum prints decimal CRC and decimal length
				stdout.WriteLine( $"{crc} {len} {( name == "-" ? "-" : name )}" );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"cksum: {name}: {ex.Message}" );
				exitCode = 1;
			}
		}

		return exitCode;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: cksum [FILE]..." );
	}
}
