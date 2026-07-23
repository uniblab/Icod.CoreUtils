// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Cksum;

using System;
using System.IO;
using System.Text;
using Icod.CoreUtils.Shared;

/// <summary>
/// cksum: compute CRC-32 and file length. Outputs: "&lt;crc&gt; &lt;length&gt; &lt;filename&gt;"."
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			try {
				using var ms = new MemoryStream();
				Console.OpenStandardInput().CopyTo( ms );
				var data = ms.ToArray();
				var crc = Crc32.Compute( data );
				stdout.WriteLine( $"""{crc} {data.Length} -""" );
				return 0;
			} catch ( Exception ex ) {
				stderr.WriteLine( $"cksum: {ex.Message}" );
				return 1;
			}
		}

		var exit = 0;
		foreach ( var path in args ) {
			if ( path == "-" ) {
				try {
					using var ms = new MemoryStream();
					Console.OpenStandardInput().CopyTo( ms );
					var data = ms.ToArray();
					var crc = Crc32.Compute( data );
					stdout.WriteLine( $"""{crc} {data.Length} -""" );
				} catch ( Exception ex ) {
					stderr.WriteLine( $"cksum: -: {ex.Message}" );
					exit = 1;
				}

				continue;
			}

			try {
				var bytes = File.ReadAllBytes( path );
				var crc = Crc32.Compute( bytes );
				stdout.WriteLine( $"""{crc} {bytes.Length} {path}""" );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"cksum: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static class Crc32 {
		private static readonly uint[] Table = BuildTable();

		private static uint[] BuildTable() {
			var table = new uint[ 256 ];
			const uint poly = 0xEDB88320u;
			for ( uint i = 0; i < 256; i++ ) {
				var crc = i;
				for ( var j = 0; j < 8; j++ ) {
					crc = ( crc & 1 ) != 0 ? ( poly ^ ( crc >> 1 ) ) : ( crc >> 1 );
				}

				table[ i ] = crc;
			}

			return table;
		}

		public static uint Compute( byte[] buffer ) {
			var crc = 0xFFFFFFFFu;
			foreach ( var b in buffer ) {
				crc = ( crc >> 8 ) ^ Table[ ( crc ^ b ) & 0xFFu ];
			}

			return crc ^ 0xFFFFFFFFu;
		}
	}
}
