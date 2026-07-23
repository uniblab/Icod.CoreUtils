// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Seq;

using System;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Minimal `seq` implementation:
///   seq LAST
///   seq FIRST LAST
///   seq FIRST INCREMENT LAST
/// Supports -w to equal-pad output width.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var padWidth = false;
		var operands = new System.Collections.Generic.List<string>();

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-w" ) {
				padWidth = true;
				continue;
			}
			if ( a == "-h" || a == "--help" ) {
				PrintUsage( stdout );
				return 0;
			}
			operands.Add( a );
		}

		if ( operands.Count < 1 || operands.Count > 3 ) {
			stderr.WriteLine( "seq: invalid number of arguments" );
			return 2;
		}

		// Parse numbers as decimal for fractional sequences.
		if ( !TryParseDecimal( operands, out var first, out var incr, out var last, stderr ) )
			return 2;

		if ( incr == 0 ) {
			stderr.WriteLine( "seq: increment must not be zero" );
			return 2;
		}

		// Determine padding width if requested.
		var width = 0;
		if ( padWidth ) {
			var s1 = FormatDecimal( first );
			var s2 = FormatDecimal( last );
			width = Math.Max( s1.Length, s2.Length );
		}

		try {
			// Iterate respecting sign of increment.
			var current = first;
			var cmp = incr > 0
				? new Func<decimal, decimal, bool>( ( c, t ) => c <= t + GetEpsilon( incr ) )
				: new Func<decimal, decimal, bool>( ( c, t ) => c >= t - GetEpsilon( incr ) );

			while ( cmp( current, last ) ) {
				var s = FormatDecimal( current );
				if ( padWidth && width > s.Length )
					stdout.WriteLine( s.PadLeft( width, '0' ) );
				else
					stdout.WriteLine( s );
				current += incr;
			}
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"seq: {ex.Message}" );
			return 1;
		}
	}

	private static bool TryParseDecimal( System.Collections.Generic.List<string> ops, out decimal first, out decimal incr, out decimal last, TextWriter stderr ) {
		first = 1m;
		incr = 1m;
		last = 0m;

		try {
			if ( ops.Count == 1 ) {
				last = decimal.Parse( ops[ 0 ], CultureInfo.InvariantCulture );
			} else if ( ops.Count == 2 ) {
				first = decimal.Parse( ops[ 0 ], CultureInfo.InvariantCulture );
				last = decimal.Parse( ops[ 1 ], CultureInfo.InvariantCulture );
			} else // 3
			  {
				first = decimal.Parse( ops[ 0 ], CultureInfo.InvariantCulture );
				incr = decimal.Parse( ops[ 1 ], CultureInfo.InvariantCulture );
				last = decimal.Parse( ops[ 2 ], CultureInfo.InvariantCulture );
			}
			return true;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"seq: numeric parse error: {ex.Message}" );
			return false;
		}
	}

	private static string FormatDecimal( decimal d ) {
		// Use invariant culture, trim trailing zeros for fractional values similar to GNU seq default.
		var s = d.ToString( CultureInfo.InvariantCulture );
		if ( s.Contains( '.' ) || s.Contains( ',' ) ) {
			// remove trailing zeros and possibly trailing decimal point
			s = s.TrimEnd( '0' ).TrimEnd( '.' );
		}
		return s;
	}

	private static decimal GetEpsilon( decimal step ) {
		// Small epsilon to account for decimal rounding in loop comparisons.
		return Math.Abs( step ) / 1000000m;
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: seq [OPTION]... LAST" );
		stdout.WriteLine( "  seq [OPTION]... FIRST LAST" );
		stdout.WriteLine( "  seq [OPTION]... FIRST INCREMENT LAST" );
		stdout.WriteLine( "  -w    equal-width output (pad with leading zeros)" );
	}
}
