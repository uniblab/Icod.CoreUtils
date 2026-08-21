namespace Icod.CoreUtils.Shared;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Shared helpers used by utilities.
/// </summary>
public static class SharedUtils {
	/// <summary>Returns the final path component using the current platform separators.</summary>
	public static string Basename( string path ) {
		if ( string.IsNullOrEmpty( path ) ) {
			return ".";
		}

		var trimmed = path.TrimEnd( System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar );
		if ( trimmed.Length == 0 ) {
			return System.IO.Path.DirectorySeparatorChar.ToString();
		}

		return System.IO.Path.GetFileName( trimmed );
	}

	/// <summary>Parses the legacy short-option specification used by existing commands.</summary>
	/// <remarks>New commands should use <see cref="Icod.CommandFramework.CommandLine.OptionParser"/>.</remarks>
	public static (HashSet<char> flags, Dictionary<char, string?> optionValues, string[] rest) ParseOptions( string[] args, string optSpec ) {
		var flags = new HashSet<char>();
		var optionValues = new Dictionary<char, string?>();
		var rest = new List<string>();
		var i = 0;
		for ( ; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( !a.StartsWith( '-' ) || a == "-" ) {
				break;
			}

			if ( a == "--" ) {
				i++;
				break;
			}

			for ( var j = 1; j < a.Length; j++ ) {
				var opt = a[ j ];
				var specIndex = optSpec.IndexOf( opt );
				if ( specIndex < 0 ) {
					_ = flags.Add( opt );
					continue;
				}

				var expectsValue = specIndex + 1 < optSpec.Length && optSpec[ specIndex + 1 ] == ':';
				if ( expectsValue ) {
					string? val = null;
					if ( j + 1 < a.Length ) {
						val = a[ ( j + 1 ).. ];
						j = a.Length;
					} else {
						if ( i + 1 < args.Length ) {
							i++;
							val = args[ i ];
						}
					}
					optionValues[ opt ] = val;
					break;
				} else {
					flags.Add( opt );
				}
			}
		}

		for ( ; i < args.Length; i++ ) {
			rest.Add( args[ i ] );
		}

		return (flags, optionValues, rest.ToArray());
	}

	/// <summary>Parses NAME=VALUE operands into a case-insensitive dictionary.</summary>
	public static Dictionary<string, string> ParseAssignments( string[] args ) {
		var dict = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var a in args ) {
			var idx = a.IndexOf( '=' );
			if ( idx > 0 ) {
				var k = a[ ..idx ];
				var v = a[ ( idx + 1 ).. ];
				dict[ k ] = v;
			}
		}

		return dict;
	}

	/// <summary>Splits an in-memory line array at a regular-expression or numeric token.</summary>
	public static IEnumerable<string[]> SplitByPatternOrLines( string[] allLines, string token ) {
		if ( string.IsNullOrEmpty( token ) ) {
			yield return allLines;
			yield break;
		}

		if ( token.Length >= 2 && token[ 0 ] == '/' && token[ ^1 ] == '/' ) {
			var pattern = token[ 1..^1 ];
			var rx = new Regex( pattern );
			var start = 0;
			for ( var i = 0; i < allLines.Length; i++ ) {
				if ( rx.IsMatch( allLines[ i ] ) ) {
					var segment = new string[ i - start ];
					Array.Copy( allLines, start, segment, 0, segment.Length );
					yield return segment;
					start = i;
				}
			}

			var last = new string[ allLines.Length - start ];
			Array.Copy( allLines, start, last, 0, last.Length );
			yield return last;
			yield break;
		}

		if ( int.TryParse( token, out var n ) && n >= 1 ) {
			var idx = Math.Min( n - 1, allLines.Length );
			var a = new string[ idx ];
			Array.Copy( allLines, 0, a, 0, idx );
			yield return a;
			var b = new string[ allLines.Length - idx ];
			Array.Copy( allLines, idx, b, 0, b.Length );
			yield return b;
			yield break;
		}

		yield return allLines;
	}
}
