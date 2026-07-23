// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Factor;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

/// <summary>
/// factor: print prime factorization of positive integers.
/// Usage:
///   factor [NUMBER]...
/// If no arguments are given, read numbers from standard input (one per line).
/// Output format:
///   n: f1 f2 f3...
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var inputs = new List<string>();
		if ( args.Length == 0 ) {
			// read from stdin
			var reader = stdin ?? Console.In;
			string? line;
			while ( ( line = reader.ReadLine() ) is not null ) {
				if ( line.Trim().Length > 0 )
					inputs.Add( line.Trim() );
			}
		} else {
			inputs.AddRange( args );
		}

		var exitCode = 0;
		foreach ( var token in inputs ) {
			if ( token.StartsWith( '#' ) || string.IsNullOrWhiteSpace( token ) )
				continue;
			if ( !BigInteger.TryParse( token, out var n ) || n < 0 ) {
				stderr.WriteLine( $"factor: invalid number: {token}" );
				exitCode = 1;
				continue;
			}

			if ( n == 0 ) {
				stdout.WriteLine( "0: 0" );
				continue;
			}

			if ( n == 1 ) {
				stdout.WriteLine( "1: 1" );
				continue;
			}

			var factors = Factorize( n );
			var sb = new StringBuilder();
			for ( var i = 0; i < factors.Count; i++ ) {
				if ( i > 0 )
					sb.Append( ' ' );
				sb.Append( factors[ i ].ToString() );
			}

			stdout.WriteLine( $"{n}: {sb}" );
		}

		return exitCode;
	}

	private static List<BigInteger> Factorize( BigInteger n ) {
		var factors = new List<BigInteger>();
		// handle small primes
		while ( n % 2 == 0 ) {
			factors.Add( 2 );
			n /= 2;
		}

		for ( BigInteger p = 3; p * p <= n; p += 2 ) {
			while ( n % p == 0 ) {
				factors.Add( p );
				n /= p;
			}
		}

		if ( n > 1 )
			factors.Add( n );
		return factors;
	}
}
