// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Cat;

using System;
using System.IO;
using System.Text;
using Icod.CoreUtils.Shared;

/// <summary>
/// cat: concatenates files to standard output.
/// Supported flags (subset): -n (number all lines), -b (number nonempty lines),
/// -E (display $ at end of each line), -T (display TAB as ^I), -s (squeeze repeated empty lines)
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var (flags, _, rest) = SharedUtils.ParseOptions( args, "nbETs" );
		var numberAll = flags.Contains( 'n' );
		var numberNonEmpty = flags.Contains( 'b' );
		var showEnds = flags.Contains( 'E' );
		var showTabs = flags.Contains( 'T' );
		var squeeze = flags.Contains( 's' );

		var lineCounter = 1;
		if ( rest.Length == 0 ) {
			return CopyReader( stdin, stdout, stderr, numberAll, numberNonEmpty, showEnds, showTabs, squeeze, ref lineCounter );
		}

		var exit = 0;
		foreach ( var path in rest ) {
			try {
				if ( path == "-" ) {
					var rc = CopyReader( stdin, stdout, stderr, numberAll, numberNonEmpty, showEnds, showTabs, squeeze, ref lineCounter );
					if ( rc != 0 ) {
						exit = rc;
					}
				} else {
					using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
					var rc = CopyReader( sr, stdout, stderr, numberAll, numberNonEmpty, showEnds, showTabs, squeeze, ref lineCounter );
					if ( rc != 0 ) {
						exit = rc;
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"cat: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int CopyReader( TextReader reader, TextWriter writer, TextWriter stderr, bool numberAll, bool numberNonEmpty, bool showEnds, bool showTabs, bool squeeze, ref int lineCounter ) {
		string? line;
		var prevBlank = false;
		while ( ( line = reader.ReadLine() ) is not null ) {
			var isBlank = line.Length == 0;
			if ( squeeze ) {
				if ( isBlank && prevBlank ) {
					prevBlank = true;
					continue;
				}

				prevBlank = isBlank;
			}

			var numberThis = false;
			if ( numberAll ) {
				numberThis = true;
			} else if ( numberNonEmpty ) {
				if ( !isBlank ) {
					numberThis = true;
				}
			}

			if ( numberThis ) {
				writer.Write( $"{lineCounter,6}\t" );
				lineCounter++;
			}

			if ( showTabs ) {
				line = line.Replace( "\t", "^I" );
			}

			if ( showEnds ) {
				writer.WriteLine( line + "$" );
			} else {
				writer.WriteLine( line );
			}
		}

		return 0;
	}
}
