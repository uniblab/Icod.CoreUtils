// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Echo;

using System;
using System.IO;
using System.Text;

/// <summary>
/// Minimal `echo` supporting -n (no newline) and -e (interpret backslash escapes).
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var noNewline = false;
		var interpret = false;
		var start = 0;
		for ( var i = 0; i < args.Length; i++ ) {
			if ( args[ i ] == "-n" ) {
				noNewline = true;
				start++;
				continue;
			}
			if ( args[ i ] == "-e" ) {
				interpret = true;
				start++;
				continue;
			}
			if ( args[ i ] == "-E" ) {
				interpret = false;
				start++;
				continue;
			}
			break;
		}

		var parts = args.Length > start ? args[ start.. ] : Array.Empty<string>();
		var s = string.Join( " ", parts );
		if ( interpret )
			s = InterpretEscapes( s );
		if ( noNewline )
			stdout.Write( s );
		else
			stdout.WriteLine( s );
		return 0;
	}

	private static string InterpretEscapes( string s ) {
		var sb = new StringBuilder();
		for ( var i = 0; i < s.Length; i++ ) {
			if ( s[ i ] == '\\' && i + 1 < s.Length ) {
				i++;
				switch ( s[ i ] ) {
					case 'n':
						sb.Append( '\n' );
						break;
					case 't':
						sb.Append( '\t' );
						break;
					case 'r':
						sb.Append( '\r' );
						break;
					case '\\':
						sb.Append( '\\' );
						break;
					case 'a':
						sb.Append( '\a' );
						break;
					case 'b':
						sb.Append( '\b' );
						break;
					case '0':
						sb.Append( '\0' );
						break;
					default:
						sb.Append( s[ i ] );
						break;
				}
			} else
				sb.Append( s[ i ] );
		}
		return sb.ToString();
	}
}
