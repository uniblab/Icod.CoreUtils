// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Expr;

using System;
using System.IO;
using System.Globalization;

/// <summary>
/// expr: evaluate expressions. This port implements a small, safe subset:
///   expr INTEGER OP INTEGER
/// where OP is + - \* / % = : \&lt; \&gt; (comparison), and string equality '='.
/// More advanced features (regex match, complex expressions) are not implemented.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			stderr.WriteLine( "expr: missing expression" );
			return 2;
		}

		// Support the common simple form: <left> <op> <right>
		if ( args.Length < 3 ) {
			stderr.WriteLine( "expr: expression too short" );
			return 2;
		}

		var left = args[ 0 ];
		var op = args[ 1 ];
		var right = args[ 2 ];

		// Try integer arithmetic first
		if ( long.TryParse( left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var li ) && long.TryParse( right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ri ) ) {
			try {
				switch ( op ) {
					case "+":
						stdout.WriteLine( ( li + ri ).ToString( CultureInfo.InvariantCulture ) );
						return 0;
					case "-":
						stdout.WriteLine( ( li - ri ).ToString( CultureInfo.InvariantCulture ) );
						return 0;
					case "*":
					case "x":
					case "X":
						stdout.WriteLine( ( li * ri ).ToString( CultureInfo.InvariantCulture ) );
						return 0;
					case "/":
						if ( ri == 0 ) {
							stderr.WriteLine( "expr: division by zero" );
							return 2;
						}

						stdout.WriteLine( ( li / ri ).ToString( CultureInfo.InvariantCulture ) );
						return 0;
					case "%":
						if ( ri == 0 ) {
							stderr.WriteLine( "expr: division by zero" );
							return 2;
						}

						stdout.WriteLine( ( li % ri ).ToString( CultureInfo.InvariantCulture ) );
						return 0;
					case "=":
						stdout.WriteLine( li == ri ? "1" : "0" );
						return 0;
					case "<":
						stdout.WriteLine( li < ri ? "1" : "0" );
						return 0;
					case ">":
						stdout.WriteLine( li > ri ? "1" : "0" );
						return 0;
					default:
						stderr.WriteLine( $"expr: unsupported operator '{op}'" );
						return 2;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"expr: {ex.Message}" );
				return 2;
			}
		}

		// If operator is string equality
		if ( op == "=" ) {
			stdout.WriteLine( left == right ? "1" : "0" );
			return 0;
		}

		stderr.WriteLine( "expr: unsupported or complex expression (only simple INTEGER OP INTEGER or string '=' supported)" );
		return 2;
	}
}
