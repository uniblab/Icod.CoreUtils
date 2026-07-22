// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Test;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// test: evaluate expressions (small portable subset).
/// Supported unary tests:
///   -e FILE   exists
///   -f FILE   regular file
///   -d FILE   directory
///   -z STR	string zero length
/// Supported binary string ops:
///   STR1 = STR2
///   STR1 != STR2
/// If first argument is '[' the last argument must be ']' (POSIX [ ... ]).
/// Returns exit code 0 for true, 1 for false, 2 for syntax error.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args is null || args.Length == 0 ) {
			return 1;
		}

		// Support [ ... ] form
		if ( args.Length >= 1 && args[ 0 ] == "[" ) {
			if ( args.Length < 3 || args[ ^1 ] != "]" ) {
				stderr.WriteLine( "test: missing ']'" );
				return 2;
			}

			// strip leading [ and trailing ]
			var inner = new string[ args.Length - 2 ];
			Array.Copy( args, 1, inner, 0, inner.Length );
			args = inner;
		}

		try {
			if ( args.Length == 1 ) {
				// single arg: string non-empty => true
				return string.IsNullOrEmpty( args[ 0 ] ) ? 1 : 0;
			}

			if ( args.Length == 2 ) {
				var op = args[ 0 ];
				var operand = args[ 1 ];
				if ( op == "-e" ) {
					return File.Exists( operand ) || Directory.Exists( operand ) ? 0 : 1;
				} else if ( op == "-f" ) {
					return File.Exists( operand ) ? 0 : 1;
				} else if ( op == "-d" ) {
					return Directory.Exists( operand ) ? 0 : 1;
				} else if ( op == "-z" ) {
					return operand.Length == 0 ? 0 : 1;
				} else {
					stderr.WriteLine( $"test: unknown unary operator '{op}'" );
					return 2;
				}
			}

			if ( args.Length == 3 ) {
				var left = args[ 0 ];
				var op = args[ 1 ];
				var right = args[ 2 ];
				if ( op == "=" || op == "==" ) {
					return left == right ? 0 : 1;
				} else if ( op == "!=" ) {
					return left != right ? 0 : 1;
				} else {
					stderr.WriteLine( $"test: unknown binary operator '{op}'" );
					return 2;
				}
			}

			stderr.WriteLine( "test: too many arguments or unsupported expression" );
			return 2;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"test: {ex.Message}" );
			return 2;
		}
	}
}
