namespace Icod.CoreUtils.Seq;

using System.Globalization;
using System.Text;

/// <summary>
/// Implements the seq utility.
/// </summary>
public static class Command {
	private const int OutputBufferSize = 32 * 1024;
	private const string VersionText = "seq (Icod.CoreUtils) 1.0";

	private sealed record NumberOperand(
		string Text,
		double Value,
		decimal? DecimalValue,
		int FractionalDigits
	);

	private sealed record PrintfFormat(
		string Prefix,
		string Suffix,
		string Flags,
		int? Width,
		int? Precision,
		char Conversion
	);

	/// <summary>Runs the command synchronously.</summary>
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

	/// <summary>Runs the command asynchronously.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var operands = new List<string>();
			var separator = Environment.NewLine;
			string? formatText = null;
			var equalWidth = false;
			var optionsEnded = false;

			for ( var index = 0; index < args.Length; index++ ) {
				var argument = args[ index ];
				if ( !optionsEnded ) {
					if ( "--" == argument ) {
						optionsEnded = true;
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
						"-w" == argument
						|| "--equal-width" == argument
					) {
						equalWidth = true;
						continue;
					}
					if (
						"-s" == argument
						|| "--separator" == argument
					) {
						if ( ++index >= args.Length ) {
							return await WriteMissingOptionArgumentAsync(
								argument,
								stderr,
								cancellationToken
							).ConfigureAwait( false );
						}
						separator = args[ index ];
						continue;
					}
					if ( argument.StartsWith( "--separator=", StringComparison.Ordinal ) ) {
						separator = argument[ "--separator=".Length.. ];
						continue;
					}
					if (
						argument.StartsWith( "-s", StringComparison.Ordinal )
						&& 2 < argument.Length
					) {
						separator = argument[ 2.. ];
						continue;
					}
					if (
						"-f" == argument
						|| "--format" == argument
					) {
						if ( ++index >= args.Length ) {
							return await WriteMissingOptionArgumentAsync(
								argument,
								stderr,
								cancellationToken
							).ConfigureAwait( false );
						}
						formatText = args[ index ];
						continue;
					}
					if ( argument.StartsWith( "--format=", StringComparison.Ordinal ) ) {
						formatText = argument[ "--format=".Length.. ];
						continue;
					}
					if (
						argument.StartsWith( "-f", StringComparison.Ordinal )
						&& 2 < argument.Length
					) {
						formatText = argument[ 2.. ];
						continue;
					}
					if (
						argument.StartsWith( '-' )
						&& !LooksLikeNumber( argument )
					) {
						await stderr.WriteAsync(
							System.String.Concat(
								"seq: unrecognized option '",
								argument,
								"'",
								Environment.NewLine,
								"Try 'seq --help' for more information.",
								Environment.NewLine
							).AsMemory(),
							cancellationToken
						).ConfigureAwait( false );
						return 1;
					}
				}
				operands.Add( argument );
			}

			if ( 0 == operands.Count ) {
				await stderr.WriteAsync(
					System.String.Concat(
						"seq: missing operand",
						Environment.NewLine,
						"Try 'seq --help' for more information.",
						Environment.NewLine
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 1;
			}
			if ( 3 < operands.Count ) {
				await stderr.WriteAsync(
					System.String.Concat(
						"seq: extra operand '",
						operands[ 3 ],
						"'",
						Environment.NewLine,
						"Try 'seq --help' for more information.",
						Environment.NewLine
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 1;
			}
			if (
				equalWidth
				&& null != formatText
			) {
				await stderr.WriteAsync(
					System.String.Concat(
						"seq: format string may not be specified when printing equal width strings",
						Environment.NewLine,
						"Try 'seq --help' for more information.",
						Environment.NewLine
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			var parsed = new List<NumberOperand>( operands.Count );
			foreach ( var operand in operands ) {
				if ( !TryParseOperand( operand, out var number ) ) {
					await stderr.WriteAsync(
						System.String.Concat(
							"seq: invalid floating point argument: '",
							operand,
							"'",
							Environment.NewLine,
							"Try 'seq --help' for more information.",
							Environment.NewLine
						).AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
					return 1;
				}
				parsed.Add( number );
			}

			var first = 1.0;
			var increment = 1.0;
			var last = parsed[ 0 ].Value;
			decimal? firstDecimal = 1m;
			decimal? incrementDecimal = 1m;
			decimal? lastDecimal = parsed[ 0 ].DecimalValue;
			switch ( parsed.Count ) {
				case 2:
					first = parsed[ 0 ].Value;
					last = parsed[ 1 ].Value;
					firstDecimal = parsed[ 0 ].DecimalValue;
					lastDecimal = parsed[ 1 ].DecimalValue;
					break;
				case 3:
					first = parsed[ 0 ].Value;
					increment = parsed[ 1 ].Value;
					last = parsed[ 2 ].Value;
					firstDecimal = parsed[ 0 ].DecimalValue;
					incrementDecimal = parsed[ 1 ].DecimalValue;
					lastDecimal = parsed[ 2 ].DecimalValue;
					break;
			}
			var firstOperandText = 1 < parsed.Count
				? parsed[ 0 ].Text
				: "1"
			;
			var firstIsNegativeZero = IsNegativeZero(
				firstOperandText,
				first
			);
			if ( 0 == increment ) {
				await stderr.WriteAsync(
					System.String.Concat(
						"seq: zero increment",
						Environment.NewLine,
						"Try 'seq --help' for more information.",
						Environment.NewLine
					).AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			PrintfFormat? printfFormat = null;
			if ( null != formatText ) {
				if ( !TryParsePrintfFormat( formatText, out printfFormat ) ) {
					await stderr.WriteAsync(
						System.String.Concat(
							"seq: format '",
							formatText,
							"' has no % directive",
							Environment.NewLine,
							"Try 'seq --help' for more information.",
							Environment.NewLine
						).AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
					return 1;
				}
			}

			var precision = 0;
			if (
				null == printfFormat
				&& parsed.All( value => null != value.DecimalValue )
			) {
				precision = parsed.Max( value => value.FractionalDigits );
			}

			var paddingWidth = 0;
			if ( equalWidth ) {
				var firstText = FormatDefault(
					firstIsNegativeZero ? -0.0 : first,
					precision
				);
				var lastText = FormatDefault( last, precision );
				paddingWidth = Math.Max(
					firstText.Length,
					lastText.Length
				);
			}

			var outputBuffer = new StringBuilder( OutputBufferSize );
			var wroteAny = false;
			if (
				null != firstDecimal
				&& null != incrementDecimal
				&& null != lastDecimal
			) {
				var current = firstDecimal.Value;
				var isFirstValue = true;
				while ( IsWithinRange( current, incrementDecimal.Value, lastDecimal.Value ) ) {
					cancellationToken.ThrowIfCancellationRequested();
					var formattedValue = isFirstValue && firstIsNegativeZero
						? -0.0
						: (double)current
					;
					var text = null == printfFormat
						? isFirstValue && firstIsNegativeZero
							? FormatDefault( formattedValue, precision )
							: FormatDefault( current, precision )
						: FormatPrintf( formattedValue, printfFormat )
					;
					if ( equalWidth ) {
						text = PadWithZeroes( text, paddingWidth );
					}
					await AppendValueAsync(
						text,
						separator,
						wroteAny,
						outputBuffer,
						stdout,
						cancellationToken
					).ConfigureAwait( false );
					wroteAny = true;
					isFirstValue = false;
					current += incrementDecimal.Value;
				}
			} else {
				for ( long index = 0; ; index++ ) {
					cancellationToken.ThrowIfCancellationRequested();
					var current = first + ( increment * index );
					if ( !IsWithinRange( current, increment, last ) ) {
						break;
					}
					var text = null == printfFormat
						? FormatDefault( current, precision )
						: FormatPrintf( current, printfFormat )
					;
					if ( equalWidth ) {
						text = PadWithZeroes( text, paddingWidth );
					}
					await AppendValueAsync(
						text,
						separator,
						wroteAny,
						outputBuffer,
						stdout,
						cancellationToken
					).ConfigureAwait( false );
					wroteAny = true;
					if ( long.MaxValue == index ) {
						break;
					}
				}
			}
			if ( wroteAny ) {
				outputBuffer.Append( Environment.NewLine );
			}
			if ( 0 < outputBuffer.Length ) {
				await stdout.WriteAsync(
					outputBuffer.ToString().AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
			return 0;
		} catch ( OperationCanceledException ) {
			return 130;
		} catch ( IOException exception ) {
			await TryWriteErrorAsync(
				stderr,
				System.String.Concat( "seq: write error: ", exception.Message, Environment.NewLine )
			).ConfigureAwait( false );
			return 1;
		} catch ( OverflowException exception ) {
			await TryWriteErrorAsync(
				stderr,
				System.String.Concat( "seq: ", exception.Message, Environment.NewLine )
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static bool IsNegativeZero(
		string text,
		double value
	) {
		return 0 == value
			&& text.StartsWith( "-", StringComparison.Ordinal )
		;
	}

	private static bool LooksLikeNumber(
		string value
	) {
		if (
			value.StartsWith( "-inf", StringComparison.OrdinalIgnoreCase )
		) {
			return false;
		}
		return double.TryParse(
			value,
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out _
		);
	}

	private static bool TryParseOperand(
		string text,
		out NumberOperand operand
	) {
		operand = null!;
		double value;
		if (
			"inf".Equals( text.TrimStart( '+' ), StringComparison.OrdinalIgnoreCase )
			|| "infinity".Equals( text.TrimStart( '+' ), StringComparison.OrdinalIgnoreCase )
		) {
			value = double.PositiveInfinity;
		} else if (
			"-inf".Equals( text, StringComparison.OrdinalIgnoreCase )
			|| "-infinity".Equals( text, StringComparison.OrdinalIgnoreCase )
		) {
			value = double.NegativeInfinity;
		} else if (
			!double.TryParse(
				text,
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out value
			)
			|| double.IsNaN( value )
		) {
			return false;
		}
		decimal? decimalValue = null;
		var fractionalDigits = 0;
		if (
			0 > text.IndexOfAny( new char[] { 'e', 'E' } )
			&& decimal.TryParse(
				text,
				NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
				CultureInfo.InvariantCulture,
				out var parsedDecimal
			)
		) {
			decimalValue = parsedDecimal;
			var point = text.IndexOf( '.' );
			if ( 0 <= point ) {
				fractionalDigits = text.Length - point - 1;
			}
		}
		operand = new NumberOperand(
			text,
			value,
			decimalValue,
			fractionalDigits
		);
		return true;
	}

	private static bool IsWithinRange(
		decimal value,
		decimal increment,
		decimal last
	) {
		return 0 < increment
			? value <= last
			: value >= last
		;
	}

	private static bool IsWithinRange(
		double value,
		double increment,
		double last
	) {
		var tolerance = Math.Max(
			Math.Abs( increment ) * 1e-12,
			Math.Abs( last ) * 1e-15
		);
		return 0 < increment
			? value <= last + tolerance
			: value >= last - tolerance
		;
	}

	private static string FormatDefault(
		decimal value,
		int precision
	) {
		return value.ToString(
			$"F{precision}",
			CultureInfo.InvariantCulture
		);
	}

	private static string FormatDefault(
		double value,
		int precision
	) {
		if ( double.IsPositiveInfinity( value ) ) {
			return "inf";
		}
		if ( double.IsNegativeInfinity( value ) ) {
			return "-inf";
		}
		if ( 0 == value && 0 > BitConverter.DoubleToInt64Bits( value ) ) {
			return "-" + 0.0.ToString(
				0 < precision ? $"F{precision}" : "G15",
				CultureInfo.InvariantCulture
			);
		}
		if ( 0 < precision ) {
			return value.ToString(
				$"F{precision}",
				CultureInfo.InvariantCulture
			);
		}
		return value.ToString(
			"G15",
			CultureInfo.InvariantCulture
		);
	}

	private static string PadWithZeroes(
		string value,
		int width
	) {
		if ( value.Length >= width ) {
			return value;
		}
		if (
			value.StartsWith( '-' )
			|| value.StartsWith( '+' )
		) {
			return value[ ..1 ]
				+ new string( '0', width - value.Length )
				+ value[ 1.. ]
			;
		}
		return value.PadLeft( width, '0' );
	}

	private static async Task AppendValueAsync(
		string value,
		string separator,
		bool wroteAny,
		StringBuilder buffer,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		if ( wroteAny ) {
			buffer.Append( separator );
		}
		buffer.Append( value );
		if ( buffer.Length < OutputBufferSize ) {
			return;
		}
		await output.WriteAsync(
			buffer.ToString().AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
		buffer.Clear();
	}

	private static bool TryParsePrintfFormat(
		string text,
		out PrintfFormat format
	) {
		format = null!;
		var conversionStart = -1;
		var conversionEnd = -1;
		string? flags = null;
		int? width = null;
		int? precision = null;
		char conversion = default;
		for ( var index = 0; index < text.Length; index++ ) {
			if ( '%' != text[ index ] ) {
				continue;
			}
			if (
				index + 1 < text.Length
				&& '%' == text[ index + 1 ]
			) {
				index++;
				continue;
			}
			if ( 0 <= conversionStart ) {
				return false;
			}
			conversionStart = index;
			index++;
			var flagsStart = index;
			while (
				index < text.Length
				&& "-+ 0#'".Contains( text[ index ] )
			) {
				index++;
			}
			flags = text[ flagsStart..index ];
			var widthStart = index;
			while (
				index < text.Length
				&& char.IsAsciiDigit( text[ index ] )
			) {
				index++;
			}
			if ( index > widthStart ) {
				width = int.Parse(
					text[ widthStart..index ],
					CultureInfo.InvariantCulture
				);
			}
			if (
				index < text.Length
				&& '.' == text[ index ]
			) {
				index++;
				var precisionStart = index;
				while (
					index < text.Length
					&& char.IsAsciiDigit( text[ index ] )
				) {
					index++;
				}
				precision = index == precisionStart
					? 0
					: int.Parse(
						text[ precisionStart..index ],
						CultureInfo.InvariantCulture
					)
				;
			}
			if (
				index < text.Length
				&& 'L' == text[ index ]
			) {
				index++;
			}
			if (
				index >= text.Length
				|| !"eEfFgGaA".Contains( text[ index ] )
			) {
				return false;
			}
			conversion = text[ index ];
			conversionEnd = index;
		}
		if ( 0 > conversionStart ) {
			return false;
		}
		format = new PrintfFormat(
			UnescapePercents( text[ ..conversionStart ] ),
			UnescapePercents( text[ ( conversionEnd + 1 ).. ] ),
			flags ?? string.Empty,
			width,
			precision,
			conversion
		);
		return true;
	}

	private static string UnescapePercents(
		string value
	) {
		return value.Replace(
			"%%",
			"%",
			StringComparison.Ordinal
		);
	}

	private static string FormatPrintf(
		double value,
		PrintfFormat format
	) {
		var precision = format.Precision ?? 6;
		string numeric;
		if ( double.IsInfinity( value ) ) {
			var upper = char.IsUpper( format.Conversion );
			numeric = double.IsNegativeInfinity( value )
				? upper ? "-INF" : "-inf"
				: upper ? "INF" : "inf"
			;
		} else {
			switch ( format.Conversion ) {
			case 'f':
			case 'F':
				numeric = value.ToString(
					$"F{precision}",
					CultureInfo.InvariantCulture
				);
				break;
			case 'e':
			case 'E':
				numeric = value.ToString(
					$"E{precision}",
					CultureInfo.InvariantCulture
				);
				numeric = NormalizeExponent(
					numeric,
					format.Conversion
				);
				break;
			case 'g':
			case 'G':
				var significantDigits = Math.Max( 1, precision );
				numeric = value.ToString(
					$"G{significantDigits}",
					CultureInfo.InvariantCulture
				);
				if ( 'G' == format.Conversion ) {
					numeric = numeric.ToUpperInvariant();
				}
				break;
			case 'a':
			case 'A':
				numeric = FormatHexFloat(
					value,
					format.Precision,
					'A' == format.Conversion
				);
				break;
				default:
					throw new InvalidOperationException();
			}
		}

		if (
			0 <= value
			&& !double.IsNaN( value )
			&& 0 <= BitConverter.DoubleToInt64Bits( value )
		) {
			if ( format.Flags.Contains( '+' ) ) {
				numeric = "+" + numeric;
			} else if ( format.Flags.Contains( ' ' ) ) {
				numeric = " " + numeric;
			}
		}
		if (
			format.Flags.Contains( '#' )
			&& !numeric.Contains( '.' )
			&& !numeric.Contains( "inf", StringComparison.OrdinalIgnoreCase )
			&& !numeric.Contains( "nan", StringComparison.OrdinalIgnoreCase )
		) {
			var exponentIndex = numeric.IndexOfAny( new char[] { 'e', 'E', 'p', 'P' } );
			numeric = 0 <= exponentIndex
				? numeric.Insert( exponentIndex, "." )
				: numeric + "."
			;
		}
		if (
			null != format.Width
			&& numeric.Length < format.Width.Value
		) {
			var padding = format.Width.Value - numeric.Length;
			if ( format.Flags.Contains( '-' ) ) {
				numeric = numeric.PadRight( format.Width.Value, ' ' );
			} else if ( format.Flags.Contains( '0' ) ) {
				numeric = PadNumericWithZeroes( numeric, padding );
			} else {
				numeric = numeric.PadLeft( format.Width.Value, ' ' );
			}
		}
		return format.Prefix + numeric + format.Suffix;
	}

	private static string PadNumericWithZeroes(
		string value,
		int padding
	) {
		var prefixLength = 0;
		if (
			value.StartsWith( '+' )
			|| value.StartsWith( '-' )
			|| value.StartsWith( ' ' )
		) {
			prefixLength = 1;
		}
		if (
			value.AsSpan( prefixLength ).StartsWith( "0x", StringComparison.OrdinalIgnoreCase )
		) {
			prefixLength += 2;
		}
		return value[ ..prefixLength ]
			+ new string( '0', padding )
			+ value[ prefixLength.. ]
		;
	}

	private static string NormalizeExponent(
		string value,
		char conversion
	) {
		var exponentMarker = value.IndexOf( 'E' );
		if ( 0 > exponentMarker ) {
			return value;
		}
		var exponent = value[ ( exponentMarker + 1 ).. ];
		var sign = exponent[ 0 ];
		var digits = exponent[ 1.. ].TrimStart( '0' );
		if ( 0 == digits.Length ) {
			digits = "0";
		}
		digits = digits.PadLeft( 2, '0' );
		return value[ ..exponentMarker ]
			+ conversion
			+ sign
			+ digits
		;
	}

	private static string FormatHexFloat(
		double value,
		int? requestedPrecision,
		bool uppercase
	) {
		if ( double.IsNaN( value ) ) {
			return uppercase ? "NAN" : "nan";
		}
		if ( double.IsPositiveInfinity( value ) ) {
			return uppercase ? "INF" : "inf";
		}
		if ( double.IsNegativeInfinity( value ) ) {
			return uppercase ? "-INF" : "-inf";
		}
		var bits = BitConverter.DoubleToInt64Bits( value );
		var negative = 0 > bits;
		bits &= long.MaxValue;
		if ( 0 == bits ) {
			var zero = requestedPrecision is null or 0
				? "0x0p+0"
				: $"0x0.{new string( '0', requestedPrecision.Value )}p+0"
			;
			return ApplyHexCase(
				( negative ? "-" : string.Empty ) + zero,
				uppercase
			);
		}

		var rawExponent = (int)( ( bits >> 52 ) & 0x7FF );
		var fraction = (ulong)( bits & 0x000F_FFFF_FFFF_FFFFL );
		var exponent = rawExponent - 1023;
		if ( 0 == rawExponent ) {
			exponent = -1022;
			while ( 0 == ( fraction & ( 1UL << 52 ) ) ) {
				fraction <<= 1;
				exponent--;
			}
			fraction &= 0x000F_FFFF_FFFF_FFFFUL;
		}

		var significand = ( 1UL << 52 ) | fraction;
		exponent -= 3;
		int leadingDigit;
		string fractionText;
		if ( null == requestedPrecision ) {
			leadingDigit = (int)( significand >> 49 );
			var fractionBits = ( significand & ( ( 1UL << 49 ) - 1 ) ) << 3;
			fractionText = fractionBits
				.ToString( "x13", CultureInfo.InvariantCulture )
				.TrimEnd( '0' )
			;
		} else {
			var precision = requestedPrecision.Value;
			if ( 13 <= precision ) {
				leadingDigit = (int)( significand >> 49 );
				var fractionBits = ( significand & ( ( 1UL << 49 ) - 1 ) ) << 3;
				fractionText = fractionBits
					.ToString( "x13", CultureInfo.InvariantCulture )
					.PadRight( precision, '0' )
				;
			} else {
				var discardedBitCount = 49 - ( precision * 4 );
				var kept = significand >> discardedBitCount;
				var discardedMask = ( 1UL << discardedBitCount ) - 1;
				var discarded = significand & discardedMask;
				var halfway = 1UL << ( discardedBitCount - 1 );
				if (
					discarded > halfway
					|| ( discarded == halfway && 0 != ( kept & 1 ) )
				) {
					kept++;
				}
				var totalKeptBits = 4 + ( precision * 4 );
				if ( kept == ( 1UL << totalKeptBits ) ) {
					kept >>= 1;
					exponent++;
				}
				leadingDigit = (int)( kept >> ( precision * 4 ) );
				var fractionMask = 0 == precision
					? 0UL
					: ( 1UL << ( precision * 4 ) ) - 1
				;
				fractionText = 0 == precision
					? string.Empty
					: ( kept & fractionMask ).ToString(
						$"x{precision}",
						CultureInfo.InvariantCulture
					)
				;
			}
		}

		var result = new StringBuilder();
		if ( negative ) {
			result.Append( '-' );
		}
		result.Append( "0x" );
		result.Append( leadingDigit.ToString( "x", CultureInfo.InvariantCulture ) );
		if ( 0 < fractionText.Length ) {
			result.Append( '.' );
			result.Append( fractionText );
		}
		result.Append( 'p' );
		result.Append( 0 <= exponent ? '+' : '-' );
		result.Append( Math.Abs( exponent ).ToString( CultureInfo.InvariantCulture ) );
		return ApplyHexCase(
			result.ToString(),
			uppercase
		);
	}

	private static string ApplyHexCase(
		string value,
		bool uppercase
	) {
		return uppercase
			? value.ToUpperInvariant()
			: value
		;
	}

	private static async Task<int> WriteMissingOptionArgumentAsync(
		string option,
		TextWriter error,
		CancellationToken cancellationToken
	) {
		await error.WriteAsync(
			System.String.Concat(
				"seq: option '", option, "' requires an argument", Environment.NewLine,
				"Try 'seq --help' for more information.", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
		return 1;
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
Usage: seq [OPTION]... LAST
  or:  seq [OPTION]... FIRST LAST
  or:  seq [OPTION]... FIRST INCREMENT LAST
Print numbers from FIRST to LAST, in steps of INCREMENT.

  -f, --format=FORMAT      use printf-style floating point FORMAT
  -s, --separator=STRING   use STRING to separate numbers (default: \n)
  -w, --equal-width        equalize width by padding with leading zeroes
      --help               display this help and exit
      --version            output version information and exit
""";
		await output.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
