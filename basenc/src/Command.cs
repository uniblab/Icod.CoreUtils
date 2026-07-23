// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Basenc;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// basenc: minimal base64 encode/decode utility.
/// Supported options:
///   -d, --decode    decode from base64
///   -? --help       display this help and exit
/// Behavior:
///   basenc [FILE...]
/// If no files are specified, read from standard input.
/// Encoded output is written as UTF-8 text. Decoded output is written as raw bytes.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var decode = false;
		var files = new List<string>();

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a is "-?" or "--help" ) {
				PrintUsage( stdout );
				return 0;
			}

			if ( a == "-d" || a == "--decode" ) {
				decode = true;
				continue;
			}

			// treat anything else as filename
			files.Add( a );
		}

		if ( files.Count == 0 ) {
			files.Add( "-" );
		}

		var exitCode = 0;
		foreach ( var path in files ) {
			try {
				if ( decode ) {
					// Read textual base64 input, then write decoded bytes.
					string inputText;
					if ( path == "-" ) {
						// read all from stdin TextReader
						inputText = stdin is not null ? stdin.ReadToEnd() : Console.In.ReadToEnd();
					} else {
						inputText = File.ReadAllText( path, Encoding.UTF8 );
					}

					// Strip whitespace that may be present
					var b64 = RemoveWhitespace( inputText );
					byte[] data;
					try {
						data = Convert.FromBase64String( b64 );
					} catch ( FormatException ex ) {
						stderr.WriteLine( $"basenc: {path}: invalid base64 data: {ex.Message}" );
						exitCode = 1;
						continue;
					}

					// Write raw bytes to stdout stream if available, otherwise write as binary via Console.OpenStandardOutput
					if ( stdout is null ) {
						// should not happen due to default above
						Console.OpenStandardOutput().Write( data, 0, data.Length );
					} else if ( stdout is StreamWriter sw ) {
						sw.Flush();
						var outStream = sw.BaseStream;
						outStream.Write( data, 0, data.Length );
						outStream.Flush();
					} else {
						// fallback: write to underlying standard output stream
						var outStream = Console.OpenStandardOutput();
						outStream.Write( data, 0, data.Length );
						outStream.Flush();
					}
				} else {
					// encode: read raw bytes and write base64 text
					byte[] data;
					if ( path == "-" ) {
						Stream inStream;
						if ( stdin is StreamReader sr ) {
							inStream = sr.BaseStream;
						} else {
							inStream = Console.OpenStandardInput();
						}

						using ( inStream ) {
							using var ms = new MemoryStream();
							inStream.CopyTo( ms );
							data = ms.ToArray();
						}
					} else {
						data = File.ReadAllBytes( path );
					}

					var encoded = Convert.ToBase64String( data );
					stdout.WriteLine( encoded );
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"basenc: {path}: {ex.Message}" );
				exitCode = 1;
			}
		}

		return exitCode;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: basenc [-d] [file...]" );
		stdout.WriteLine( "  -d, --decode    decode from base64" );
		stdout.WriteLine( "  -?, --help      display this help and exit" );
	}

	private static string RemoveWhitespace( string s ) {
		var sb = new StringBuilder( s.Length );
		foreach ( var c in s ) {
			if ( !char.IsWhiteSpace( c ) ) {
				sb.Append( c );
			}
		}
		return sb.ToString();
	}
}
