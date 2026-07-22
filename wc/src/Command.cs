// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Wc;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

/// <summary>
/// wc: word, line, and byte count.
/// Supported flags:
///   -l  lines
///   -w  words
///   -c  bytes
/// If no flags given, prints all three in the order: lines words bytes.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var showLines = false;
		var showWords = false;
		var showBytes = false;
		var rem = new List<string>();
		foreach ( var a in args ) {
			if ( a == "-l" ) {
				showLines = true;
			} else if ( a == "-w" ) {
				showWords = true;
			} else if ( a == "-c" ) {
				showBytes = true;
			} else {
				rem.Add( a );
			}
		}

		if ( !showLines && !showWords && !showBytes ) {
			showLines = true;
			showWords = true;
			showBytes = true;
		}

		if ( rem.Count == 0 ) {
			rem.Add( "-" );
		}

		var exit = 0;
		long totalLines = 0;
		long totalWords = 0;
		long totalBytes = 0;
		foreach ( var path in rem ) {
			try {
				byte[] data;
				if ( path == "-" ) {
					using var ms = new MemoryStream();
					Console.OpenStandardInput().CopyTo( ms );
					data = ms.ToArray();
				} else {
					data = File.ReadAllBytes( path );
				}

				var text = Encoding.UTF8.GetString( data );
				var lines = 0;
				foreach ( var ch in text ) {
					if ( ch == '\n' ) {
						lines++;
					}
				}

				var words = CountWords( text );
				var bytes = data.Length;
				totalLines += lines;
				totalWords += words;
				totalBytes += bytes;

				var parts = new List<string>();
				if ( showLines ) {
					parts.Add( lines.ToString( CultureInfo.InvariantCulture ) );
				}

				if ( showWords ) {
					parts.Add( words.ToString( CultureInfo.InvariantCulture ) );
				}

				if ( showBytes ) {
					parts.Add( bytes.ToString( CultureInfo.InvariantCulture ) );
				}

				parts.Add( path );
				stdout.WriteLine( string.Join( " ", parts ) );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"wc: {path}: {ex.Message}" );
				exit = 1;
			}
		}

		if ( rem.Count > 1 ) {
			var parts = new List<string>();
			if ( showLines ) {
				parts.Add( totalLines.ToString( CultureInfo.InvariantCulture ) );
			}

			if ( showWords ) {
				parts.Add( totalWords.ToString( CultureInfo.InvariantCulture ) );
			}

			if ( showBytes ) {
				parts.Add( totalBytes.ToString( CultureInfo.InvariantCulture ) );
			}

			parts.Add( "total" );
			stdout.WriteLine( string.Join( " ", parts ) );
		}

		return exit;
	}

	private static int CountWords( string text ) {
		if ( string.IsNullOrEmpty( text ) ) {
			return 0;
		}

		var inWord = false;
		var count = 0;
		foreach ( var ch in text ) {
			if ( char.IsWhiteSpace( ch ) ) {
				if ( inWord ) {
					inWord = false;
				}
			} else {
				if ( !inWord ) {
					inWord = true;
					count++;
				}
			}
		}

		return count;
	}
}
