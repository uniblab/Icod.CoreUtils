// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Csplit;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// csplit: split a FILE into sections determined by PATTERN.
/// Simplified implementation:
///   - supports -l &lt;N&gt; to split input into files with N lines each
///   - if no -l provided, splits at numeric line addresses (each pattern is a decimal number)
/// Output files are named 'xx00', 'xx01', ...
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var lineCount = 0;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( !args[ i ].StartsWith( '-' ) ) {
				break;
			}

			if ( args[ i ] == "-l" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out lineCount ) ) {
					stderr.WriteLine( $"csplit: invalid line count '{args[ i ]}'" );
					return 1;
				}
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			stderr.WriteLine( "csplit: missing file operand" );
			return 1;
		}

		var file = rem[ 0 ];
		if ( !File.Exists( file ) ) {
			stderr.WriteLine( $"csplit: cannot open '{file}'" );
			return 1;
		}

		try {
			using var sr = new StreamReader( file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
			if ( lineCount > 0 ) {
				return SplitByLineCount( sr, lineCount, stderr );
			} else {
				// Interpret remaining rem entries (after file) as numeric breakpoints
				if ( rem.Count < 2 ) {
					stderr.WriteLine( "csplit: missing pattern(s)" );
					return 1;
				}

				var patterns = rem.GetRange( 1, rem.Count - 1 );
				return SplitByAddresses( sr, patterns, stderr );
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"csplit: {ex.Message}" );
			return 1;
		}
	}

	private static int SplitByLineCount( StreamReader sr, int linesPerFile, TextWriter stderr ) {
		var idx = 0;
		while ( !sr.EndOfStream ) {
			var outName = $"xx{idx:D2}";
			try {
				using var outFs = new FileStream( outName, FileMode.Create, FileAccess.Write );
				using var sw = new StreamWriter( outFs, Encoding.UTF8 );
				for ( var j = 0; j < linesPerFile && !sr.EndOfStream; j++ ) {
					var line = sr.ReadLine();
					sw.WriteLine( line );
				}

				idx++;
			} catch ( Exception ex ) {
				stderr.WriteLine( $"csplit: {outName}: {ex.Message}" );
				return 1;
			}
		}

		return 0;
	}

	private static int SplitByAddresses( StreamReader sr, System.Collections.Generic.List<string> patterns, TextWriter stderr ) {
		// Patterns are decimal line numbers where to split before that line.
		var lines = new System.Collections.Generic.List<string>();
		while ( !sr.EndOfStream ) {
			lines.Add( sr.ReadLine() ?? string.Empty );
		}

		var currentIndex = 0;
		var fileIndex = 0;
		foreach ( var pat in patterns ) {
			if ( !int.TryParse( pat, out var addr ) ) {
				stderr.WriteLine( $"csplit: invalid pattern '{pat}'" );
				return 1;
			}

			var outName = $"xx{fileIndex:D2}";
			try {
				using var outFs = new FileStream( outName, FileMode.Create, FileAccess.Write );
				using var sw = new StreamWriter( outFs, Encoding.UTF8 );
				for ( var j = currentIndex; j < Math.Min( addr - 1, lines.Count ); j++ ) {
					sw.WriteLine( lines[ j ] );
				}

				currentIndex = Math.Min( addr - 1, lines.Count );
				fileIndex++;
			} catch ( Exception ex ) {
				stderr.WriteLine( $"csplit: {outName}: {ex.Message}" );
				return 1;
			}
		}

		// Write remainder
		var outNameLast = $"xx{fileIndex:D2}";
		try {
			using var outFs = new FileStream( outNameLast, FileMode.Create, FileAccess.Write );
			using var sw = new StreamWriter( outFs, Encoding.UTF8 );
			for ( var j = currentIndex; j < lines.Count; j++ ) {
				sw.WriteLine( lines[ j ] );
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"csplit: {outNameLast}: {ex.Message}" );
			return 1;
		}

		return 0;
	}
}
