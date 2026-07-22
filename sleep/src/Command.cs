// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sleep;

using System;
using System.Globalization;
using System.IO;
using System.Threading;

/// <summary>
/// sleep: delay for a specified amount of time (seconds, fractional supported).
/// Usage: sleep NUMBER...  (sum of numbers interpreted as seconds)
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 0 ) {
			return 0;
		}

		double total = 0.0;
		foreach ( var a in args ) {
			if ( double.TryParse( a, NumberStyles.Float, CultureInfo.InvariantCulture, out var v ) ) {
				total += v;
			} else {
				stderr.WriteLine( $"sleep: invalid time interval '{a}'" );
				return 1;
			}
		}

		try {
			var ms = (int)Math.Round( total * 1000.0 );
			if ( ms < 0 ) {
				ms = 0;
			}

			Thread.Sleep( ms );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"sleep: {ex.Message}" );
			return 1;
		}
	}
}
