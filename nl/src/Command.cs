// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.NL;

using System;
using System.IO;
using System.Text;
using System.Globalization;
using Icod.CoreUtils.Shared;

/// <summary>
/// nl: number lines of files. Best-effort POSIX behavior.
/// Supported options:
///   -b &lt;style&gt;   body numbering style: 'a' (all) or 't' (non-empty) (default 't')
///   -v &lt;number&gt;  initial line number (default 1)
///   -w &lt;width&gt;   number field width (default 6)
///   -n &lt;format&gt;  number format: 'rn' (right), 'ln' (left), 'rz' (leading zeros) (default 'rn')
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var (flags, optionValues, rest) = SharedUtils.ParseOptions( args, "b:v:w:n:" );
		var bodyStyle = "t";
		if ( optionValues.TryGetValue( 'b', out var bval ) && !string.IsNullOrEmpty( bval ) ) {
			bodyStyle = bval!;
		}

		var initialNumber = 1L;
		if ( optionValues.TryGetValue( 'v', out var vval ) && !string.IsNullOrEmpty( vval ) && long.TryParse( vval, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vparsed ) ) {
			initialNumber = vparsed;
		}

		var width = 6;
		if ( optionValues.TryGetValue( 'w', out var wval ) && !string.IsNullOrEmpty( wval ) && int.TryParse( wval, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wparsed ) ) {
			width = Math.Max( 1, wparsed );
		}

		var numFormat = "rn";
		if ( optionValues.TryGetValue( 'n', out var nval ) && !string.IsNullOrEmpty( nval ) ) {
			numFormat = nval!;
		}

		if ( rest.Length == 0 ) {
			return ProcessStream( "<stdin>", stdin, stdout, stderr, bodyStyle, initialNumber, width, numFormat );
		}

		var exit = 0;
		var numberSeed = initialNumber;
		foreach ( var path in rest ) {
			if ( path == "-" ) {
				var rc = ProcessStream( path, stdin, stdout, stderr, bodyStyle, numberSeed, width, numFormat );
				if ( rc != 0 ) {
					exit = 1;
				}

				numberSeed += CountLinesFromStream( stdin );
				continue;
			}

			try {
				using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				var rc = ProcessStream( path, sr, stdout, stderr, bodyStyle, numberSeed, width, numFormat );
				if ( rc != 0 ) {
					exit = 1;
				}

				numberSeed += CountLinesInFile( path );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"nl: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int ProcessStream( string sourceName, TextReader reader, TextWriter writer, TextWriter stderr, string bodyStyle, long startNumber, int width, string numFormat ) {
		var lineNumber = startNumber;
		string? line;
		while ( ( line = reader.ReadLine() ) is not null ) {
			var numberThisLine = ShouldNumberLine( line, bodyStyle );
			if ( numberThisLine ) {
				var numStr = FormatNumber( lineNumber, width, numFormat );
				writer.Write( $"{numStr}\t" );
				lineNumber++;
			} else {
				writer.Write( new string( ' ', width ) );
				writer.Write( "\t" );
			}

			writer.WriteLine( line );
		}

		return 0;
	}

	private static bool ShouldNumberLine( string line, string bodyStyle ) {
		if ( bodyStyle == "a" ) {
			return true;
		}

		if ( bodyStyle == "t" ) {
			return line.Length > 0;
		}

		return line.Length > 0;
	}

	private static string FormatNumber( long number, int width, string numFormat ) {
		if ( numFormat == "ln" ) {
			return number.ToString( CultureInfo.InvariantCulture ).PadRight( width );
		}

		if ( numFormat == "rz" ) {
			return number.ToString( CultureInfo.InvariantCulture ).PadLeft( width, '0' );
		}

		return number.ToString( CultureInfo.InvariantCulture ).PadLeft( width );
	}

	private static long CountLinesInFile( string path ) {
		try {
			long count = 0;
			using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
			while ( sr.ReadLine() is not null ) {
				count++;
			}

			return count;
		} catch {
			return 0;
		}
	}

	private static long CountLinesFromStream( TextReader reader ) {
		return 0;
	}
}
