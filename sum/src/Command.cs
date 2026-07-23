// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sum;

using System;
using System.IO;
using System.Text;

/// <summary>
/// sum: checksum and block count (BSD-style 1K blocks).
/// Outputs: "&lt;checksum&gt; &lt;blocks&gt; &lt;filename&gt;"
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var paths = args.Length == 0 ? new[] { "-" } : args;
		var exit = 0;
		foreach ( var p in paths ) {
			try {
				byte[] data;
				if ( p == "-" ) {
					using var ms = new MemoryStream();
					Console.OpenStandardInput().CopyTo( ms );
					data = ms.ToArray();
				} else {
					data = File.ReadAllBytes( p );
				}

				uint sum = 0;
				foreach ( var b in data ) {
					sum = ( sum + b ) & 0xFFFFFFFFu;
				}

				var blocks = ( data.Length + 1023 ) / 1024;
				stdout.WriteLine( $"{sum} {blocks} {p}" );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"sum: {p}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}
}
