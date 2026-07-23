namespace Icod.CoreUtils.Diff;

using System;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// diff: simple line-oriented comparison producing a minimal unified-like output.
/// Credit: Douglas McIlroy (original diff concepts).
/// Ported to .NET by Timothy J. Bruce &lt;uniblab@hotmail.com&gt;
/// Usage: diff &lt;file1&gt; &lt;file2&gt;
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length < 2 ) {
			stderr.WriteLine( "Usage: diff <file1> <file2>" );
			return 2;
		}

		var aPath = args[ 0 ];
		var bPath = args[ 1 ];

		string[] aLines;
		string[] bLines;
		try {
			aLines = File.ReadAllLines( aPath, Encoding.UTF8 );
		} catch ( Exception ex ) {
			stderr.WriteLine( $"diff: {aPath}: {ex.Message}" );
			return 1;
		}

		try {
			bLines = File.ReadAllLines( bPath, Encoding.UTF8 );
		} catch ( Exception ex ) {
			stderr.WriteLine( $"diff: {bPath}: {ex.Message}" );
			return 1;
		}

		var max = Math.Max( aLines.Length, bLines.Length );
		for ( var i = 0; i < max; i++ ) {
			var a = i < aLines.Length ? aLines[ i ] : null;
			var b = i < bLines.Length ? bLines[ i ] : null;
			if ( a == b )
				continue;
			if ( a is null ) {
				stdout.WriteLine( $"+ {b}" );
			} else if ( b is null ) {
				stdout.WriteLine( $"- {a}" );
			} else {
				stdout.WriteLine( $"- {a}" );
				stdout.WriteLine( $"+ {b}" );
			}
		}

		return 0;
	}
}
