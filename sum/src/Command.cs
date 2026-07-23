// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sum;

using System;
using System.IO;
using System.Text;

/// <summary>
/// Compute a simple 16-bit checksum and 512-byte block count similar to BSD `sum`.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var files = args.Length == 0 ? new[] { "-" } : args;
		var exitCode = 0;
		foreach ( var name in files ) {
			try {
				using var stream = name == "-" ? Console.OpenStandardInput() : File.OpenRead( name );
				var buffer = new byte[ 8192 ];
				long len = 0;
				ulong sum = 0;
				int read;
				while ( ( read = stream.Read( buffer, 0, buffer.Length ) ) > 0 ) {
					len += read;
					for ( var i = 0; i < read; i++ )
						sum = ( sum + buffer[ i ] ) & 0xFFFFu;
				}
				var blocks = ( len + 511 ) / 512;
				stdout.WriteLine( $"{sum} {blocks} {( name == "-" ? "-" : name )}" );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"sum: {name}: {ex.Message}" );
				exitCode = 1;
			}
		}
		return exitCode;
	}
}