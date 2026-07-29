namespace Icod.CoreUtils.Factor;

using System.Globalization;
using System.Numerics;
using System.Text;

/// <summary>
/// Implements the factor utility.
/// </summary>
public static class Command {
	private static readonly int[] SmallPrimes = new int[] {
		2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47,
		53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107,
		109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167,
		173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229,
		233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283,
		293, 307, 311, 313, 317, 331, 337, 347, 349, 353, 359,
		367, 373, 379, 383, 389, 397, 401, 409, 419, 421, 431,
		433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491,
		499, 503, 509, 521, 523, 541, 547, 557, 563, 569, 571,
		577, 587, 593, 599, 601, 607, 613, 617, 619, 631, 641,
		643, 647, 653, 659, 661, 673, 677, 683, 691, 701, 709,
		719, 727, 733, 739, 743, 751, 757, 761, 769, 773, 787,
		797, 809, 811, 821, 823, 827, 829, 839, 853, 857, 859,
		863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941,
		947, 953, 967, 971, 977, 983, 991, 997
	};
	private static readonly ulong[] DeterministicUInt64Bases = new ulong[] {
		2, 325, 9375, 28178, 450775, 9780504, 1795265022
	};
	private const string VersionText = "factor (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>factor</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>factor</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var operands = new List<string>();
			var useExponents = false;
			var optionsEnded = false;
			for ( var index = 0; index < args.Length; index++ ) {
				var argument = args[ index ];
				if ( !optionsEnded ) {
					if ( "--" == argument ) {
						optionsEnded = true;
						continue;
					}
					if (
						"-h" == argument
						|| "--exponents" == argument
					) {
						useExponents = true;
						continue;
					}
					if ( "--help" == argument ) {
						await PrintUsageAsync(
							stdout,
							cancellationToken
						).ConfigureAwait( false );
						return 0;
					}
					if ( "--version" == argument ) {
						await stdout.WriteLineAsync(
							VersionText.AsMemory(),
							cancellationToken
						).ConfigureAwait( false );
						return 0;
					}
					if (
						argument.Length > 1
						&& '-' == argument[ 0 ]
					) {
						await stderr.WriteAsync(
							System.String.Concat(
								"factor: invalid option -- '",
								argument[ 1 ],
								"'",
								Environment.NewLine,
								"Try 'factor --help' for more information.",
								Environment.NewLine
							).AsMemory(),
							cancellationToken
						).ConfigureAwait( false );
						return 1;
					}
				}
				operands.Add( argument );
			}

			var exitCode = 0;
			if ( 0 < operands.Count ) {
				foreach ( var operand in operands ) {
					exitCode |= await ProcessTokenAsync(
						operand,
						useExponents,
						stdout,
						stderr,
						cancellationToken
					).ConfigureAwait( false );
				}
				return exitCode;
			}

			var buffer = new char[ 4096 ];
			var token = new StringBuilder();
			while ( true ) {
				var count = await stdin.ReadAsync(
					buffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}
				for ( var index = 0; index < count; index++ ) {
					var value = buffer[ index ];
					if ( char.IsWhiteSpace( value ) ) {
						if ( 0 < token.Length ) {
							exitCode |= await ProcessTokenAsync(
								token.ToString(),
								useExponents,
								stdout,
								stderr,
								cancellationToken
							).ConfigureAwait( false );
							token.Clear();
						}
					} else {
						token.Append( value );
					}
				}
			}
			if ( 0 < token.Length ) {
				exitCode |= await ProcessTokenAsync(
					token.ToString(),
					useExponents,
					stdout,
					stderr,
					cancellationToken
				).ConfigureAwait( false );
			}
			return exitCode;
		} catch ( OperationCanceledException ) {
			return 130;
		} catch ( IOException exception ) {
			await TryWriteErrorAsync(
				stderr,
				System.String.Concat( "factor: ", exception.Message, Environment.NewLine )
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static async Task<int> ProcessTokenAsync(
		string token,
		bool useExponents,
		TextWriter output,
		TextWriter error,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if (
			!BigInteger.TryParse(
				token,
				NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture,
				out var number
			)
			|| 0 > number
		) {
			await error.WriteAsync(
				System.String.Concat(
					"factor: '",
					token,
					"' is not a valid positive integer",
					Environment.NewLine
				).AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			return 1;
		}

		var outputLine = new StringBuilder();
		outputLine.Append(
			number.ToString( CultureInfo.InvariantCulture )
		);
		outputLine.Append( ':' );
		if ( 1 < number ) {
			var factors = Factorize(
				number,
				cancellationToken
			);
			factors.Sort();
			if ( useExponents ) {
				for ( var index = 0; index < factors.Count; ) {
					var factor = factors[ index ];
					var exponent = 1;
					while (
						index + exponent < factors.Count
						&& factor == factors[ index + exponent ]
					) {
						exponent++;
					}
					outputLine.Append( ' ' );
					outputLine.Append(
						factor.ToString( CultureInfo.InvariantCulture )
					);
					if ( 1 < exponent ) {
						outputLine.Append( '^' );
						outputLine.Append(
							exponent.ToString( CultureInfo.InvariantCulture )
						);
					}
					index += exponent;
				}
			} else {
				foreach ( var factor in factors ) {
					outputLine.Append( ' ' );
					outputLine.Append(
						factor.ToString( CultureInfo.InvariantCulture )
					);
				}
			}
		}
		outputLine.Append( Environment.NewLine );
		await output.WriteAsync(
			outputLine.ToString().AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
		return 0;
	}

	private static List<BigInteger> Factorize(
		BigInteger number,
		CancellationToken cancellationToken
	) {
		var factors = new List<BigInteger>();
		foreach ( var smallPrime in SmallPrimes ) {
			while ( 0 == number % smallPrime ) {
				cancellationToken.ThrowIfCancellationRequested();
				factors.Add( smallPrime );
				number /= smallPrime;
			}
		}
		if ( 1 < number ) {
			FactorRecursive(
				number,
				factors,
				cancellationToken
			);
		}
		return factors;
	}

	private static void FactorRecursive(
		BigInteger number,
		List<BigInteger> factors,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( 1 == number ) {
			return;
		}
		if ( IsPrime( number, cancellationToken ) ) {
			factors.Add( number );
			return;
		}
		var divisor = PollardRho(
			number,
			cancellationToken
		);
		FactorRecursive(
			divisor,
			factors,
			cancellationToken
		);
		FactorRecursive(
			number / divisor,
			factors,
			cancellationToken
		);
	}

	private static bool IsPrime(
		BigInteger number,
		CancellationToken cancellationToken
	) {
		if ( 2 > number ) {
			return false;
		}
		foreach ( var prime in SmallPrimes.Take( 25 ) ) {
			if ( number == prime ) {
				return true;
			}
			if ( 0 == number % prime ) {
				return false;
			}
		}

		var d = number - 1;
		var shifts = 0;
		while ( d.IsEven ) {
			d >>= 1;
			shifts++;
		}
		IEnumerable<BigInteger> bases = number <= ulong.MaxValue
			? DeterministicUInt64Bases.Select( value => new BigInteger( value ) )
			: SmallPrimes.Take( 16 ).Select( value => new BigInteger( value ) )
		;
		foreach ( var basis in bases ) {
			cancellationToken.ThrowIfCancellationRequested();
			var reducedBasis = basis % number;
			if ( BigInteger.Zero == reducedBasis ) {
				continue;
			}
			var value = BigInteger.ModPow(
				reducedBasis,
				d,
				number
			);
			if (
				BigInteger.One == value
				|| number - 1 == value
			) {
				continue;
			}
			var probablyPrime = false;
			for ( var round = 1; round < shifts; round++ ) {
				value = BigInteger.Remainder(
					value * value,
					number
				);
				if ( number - 1 == value ) {
					probablyPrime = true;
					break;
				}
			}
			if ( !probablyPrime ) {
				return false;
			}
		}
		return true;
	}

	private static BigInteger PollardRho(
		BigInteger number,
		CancellationToken cancellationToken
	) {
		if ( number.IsEven ) {
			return 2;
		}
		for ( var constant = BigInteger.One; ; constant++ ) {
			var x = new BigInteger( 2 );
			var y = new BigInteger( 2 );
			var divisor = BigInteger.One;
			var iterations = 0;
			while ( BigInteger.One == divisor ) {
				x = IteratePolynomial( x, constant, number );
				y = IteratePolynomial(
					IteratePolynomial( y, constant, number ),
					constant,
					number
				);
				divisor = BigInteger.GreatestCommonDivisor(
					BigInteger.Abs( x - y ),
					number
				);
				if ( 0 == ( ++iterations & 1023 ) ) {
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
			if ( divisor != number ) {
				return divisor;
			}
		}
	}

	private static BigInteger IteratePolynomial(
		BigInteger value,
		BigInteger constant,
		BigInteger modulus
	) {
		return BigInteger.Remainder(
			( value * value ) + constant,
			modulus
		);
	}

	private static async Task TryWriteErrorAsync(
		TextWriter error,
		string message
	) {
		try {
			await error.WriteAsync( message ).ConfigureAwait( false );
		} catch ( IOException ) {
		}
	}

	private static async Task PrintUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string text = """
Usage: factor [OPTION] [NUMBER]...
Print the prime factors of each specified integer NUMBER.  If none are
specified on the command line, read them from standard input.

  -h, --exponents  print repeated factors in form p^e
      --help       display this help and exit
      --version    output version information and exit
""";
		await output.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
