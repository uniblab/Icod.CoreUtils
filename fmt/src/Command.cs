// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Fmt;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// fmt: simple paragraph reflow. Default width 75.
/// Usage: fmt [-w width] [file...]
/// </summary>
public static partial class Command {

	private const char SPACE = ' ';
	private static readonly System.Char[] SPACE_CHAR_ARRAY;

	static Command() {
		SPACE_CHAR_ARRAY = [ SPACE ];
	}

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var width = 75;
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			if ( args[ i ] == "-w" && i + 1 < args.Length ) {
				i++;
				if ( !int.TryParse( args[ i ], out width ) ) {
					stderr.WriteLine( $"fmt: invalid width '{args[ i ]}'" );
					return 1;
				}
			} else {
				break;
			}
		}

		var rem = new List<string>();
		for ( ; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		if ( rem.Count == 0 ) {
			return ProcessReader( "<stdin>", Console.In, stdout, stderr, width );
		}

		var exit = 0;
		foreach ( var path in rem ) {
			try {
				using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				var rc = ProcessReader( path, sr, stdout, stderr, width );
				if ( rc != 0 ) {
					exit = rc;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"fmt: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static int ProcessReader( string name, TextReader reader, TextWriter stdout, TextWriter stderr, int width ) {
		try {
			var paragraphs = new List<string>();
			var sb = new StringBuilder();
			string? line;
			while ( ( line = reader.ReadLine() ) is not null ) {
				if ( string.IsNullOrWhiteSpace( line ) ) {
					if ( sb.Length > 0 ) {
						paragraphs.Add( sb.ToString().Trim() );
						sb.Clear();
					}

					paragraphs.Add( string.Empty );
				} else {
					if ( sb.Length > 0 ) {
						sb.Append( ' ' );
					}

					sb.Append( line.Trim() );
				}
			}

			if ( sb.Length > 0 ) {
				paragraphs.Add( sb.ToString().Trim() );
			}

			foreach ( var para in paragraphs ) {
				if ( string.IsNullOrEmpty( para ) ) {
					stdout.WriteLine();
				} else {
					var words = para.Split( SPACE_CHAR_ARRAY, StringSplitOptions.RemoveEmptyEntries );
					var lineBuf = new StringBuilder();
					foreach ( var w in words ) {
						if ( lineBuf.Length + w.Length + ( lineBuf.Length > 0 ? 1 : 0 ) > width ) {
							stdout.WriteLine( lineBuf.ToString() );
							lineBuf.Clear();
						}

						if ( lineBuf.Length > 0 ) {
							lineBuf.Append( ' ' );
						}

						lineBuf.Append( w );
					}

					if ( lineBuf.Length > 0 ) {
						stdout.WriteLine( lineBuf.ToString() );
					}
				}
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"fmt: {name}: {ex.Message}" );
			return 1;
		}
	}
}
