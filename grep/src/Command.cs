// Original behavior/reference: grep (Ken Thompson)
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Grep;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

/// <summary>
/// grep: search for PATTERN in each FILE or standard input.
/// Supported options:
///   -i   ignore case
///   -v   invert match
///   -n   print line number
///   -c   count matching lines
///   -l   print only names of files with matches
///   -H   print filename
/// Patterns use .NET regular expressions (best-effort POSIX compatibility).
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var ignoreCase = false;
		var invert = false;
		var printNumber = false;
		var countOnly = false;
		var listFiles = false;
		var printFilename = false;
		var patterns = new List<string>();
		var files = new List<string>();

		// simple option parsing
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( !a.StartsWith( '-' ) || a == "-" ) {
				break;
			}

			if ( a == "-i" ) {
				ignoreCase = true;
			} else if ( a == "-v" ) {
				invert = true;
			} else if ( a == "-n" ) {
				printNumber = true;
			} else if ( a == "-c" ) {
				countOnly = true;
			} else if ( a == "-l" ) {
				listFiles = true;
			} else if ( a == "-H" ) {
				printFilename = true;
			} else if ( a == "-e" ) {
				if ( i + 1 < args.Length ) {
					i++;
					patterns.Add( args[ i ] );
				}
			} else {
				// unsupported option: ignore
			}
		}

		// next arg is pattern if not provided by -e
		if ( patterns.Count == 0 && i < args.Length ) {
			patterns.Add( args[ i ] );
			i++;
		}

		for ( ; i < args.Length; i++ ) {
			files.Add( args[ i ] );
		}

		if ( patterns.Count == 0 ) {
			stderr.WriteLine( "grep: missing pattern" );
			return 2;
		}

		var options = RegexOptions.None;
		if ( ignoreCase ) {
			options |= RegexOptions.IgnoreCase;
		}

		var regex = new Regex( patterns[ 0 ], options );

		if ( files.Count == 0 ) {
			files.Add( "-" );
		}

		var exit = 0;
		foreach ( var path in files ) {
			try {
				TextReader reader;
				if ( path == "-" ) {
					reader = stdin;
				} else {
					reader = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
				}

				using ( reader ) {
					string? line;
					long num = 0;
					var matchCount = 0;
					var fileMatched = false;
					while ( ( line = reader.ReadLine() ) is not null ) {
						num++;
						var isMatch = regex.IsMatch( line );
						if ( invert ) {
							isMatch = !isMatch;
						}

						if ( isMatch ) {
							fileMatched = true;
							matchCount++;
							if ( listFiles ) {
								stdout.WriteLine( path );
								break;
							}

							if ( countOnly ) {
								continue;
							}

							var outLine = new StringBuilder();
							if ( printFilename && path != "-" ) {
								outLine.Append( path );
								outLine.Append( ':' );
							}

							if ( printNumber ) {
								outLine.Append( num );
								outLine.Append( ':' );
							}

							outLine.Append( line );
							stdout.WriteLine( outLine.ToString() );
						}
					}

					if ( countOnly ) {
						stdout.WriteLine( matchCount );
					}

					if ( fileMatched ) {
						exit = 0;
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"grep: {path}: {ex.Message}" );
				exit = 2;
			}
		}

		return exit;
	}
}
