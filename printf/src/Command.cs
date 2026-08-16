namespace Icod.CoreUtils.Printf;

using System.Globalization;
using System.Numerics;
using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Formatting;

/// <summary>Implements GNU-style <c>printf FORMAT [ARGUMENT]...</c>.</summary>
public static class Command {
	private const string VersionText = "printf (Icod.CoreUtils) 1.0";

	private sealed record Directive(
		int? ArgumentPosition,
		string Flags,
		int? Width,
		bool WidthFromArgument,
		int? WidthPosition,
		int? Precision,
		bool HasPrecision,
		bool PrecisionFromArgument,
		int? PrecisionPosition,
		char Conversion
	);

	private sealed class PassState {
		public int SequentialIndex { get; set; }
		public int HighestRelativeIndex { get; set; }
		public bool HadConversion { get; set; }
		public bool HadError { get; set; }
		public bool StopOutput { get; set; }
	}

	/// <summary>
	/// Executes <c>printf</c> synchronously with optional standard-stream substitution.
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
			new CommandContext(
				"printf",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error
			)
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>printf</c> asynchronously using caller-supplied standard streams.
	/// </summary>
	/// <remarks>
	/// The supplied standard streams are required for this overload and remain caller-owned. Cancellation is reported through the command status policy rather than by disposing those streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="standardOutput">The caller-owned writer used for standard output.</param>
	/// <param name="standardError">The caller-owned writer used for diagnostics.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static Task<int> RunAsync(
		string[] args,
		TextWriter standardOutput,
		TextWriter standardError,
		CancellationToken cancellationToken = default
	) {
		return RunAsync(
			args,
			new CommandContext(
				"printf",
				TextReader.Null,
				standardOutput,
				standardError,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>
	/// Executes <c>printf</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			if ( 1 == args.Length && "--help" == args[ 0 ] ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( 1 == args.Length && "--version" == args[ 0 ] ) {
				await context.StandardOutput.WriteLineAsync( VersionText.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var first = 0;
			if ( 0 < args.Length && "--" == args[ 0 ] ) {
				first++;
			}
			if ( first >= args.Length ) {
				await context.Diagnostics.ErrorAsync( "missing operand", context.CancellationToken ).ConfigureAwait( false );
				await context.Diagnostics.ErrorAsync( "Try 'printf --help' for more information.", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			GnuEscapeDecodeResult decoded;
			try {
				decoded = GnuEscapeDecoder.Decode( args[ first ], allowBareOctal: true, allowStopOutput: true );
			} catch ( FormatException ex ) {
				await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var operands = args.Skip( first + 1 ).ToArray();
			var format = decoded.Text;
			var baseIndex = 0;
			var overallError = false;
			var firstPass = true;
			do {
				var state = new PassState();
				await FormatPassAsync( format, operands, baseIndex, state, context ).ConfigureAwait( false );
				overallError |= state.HadError;
				if ( state.StopOutput || decoded.StopOutput ) {
					break;
				}
				var consumed = Math.Max( state.SequentialIndex, state.HighestRelativeIndex );
				if ( !state.HadConversion ) {
					if ( firstPass && 0 < operands.Length ) {
						await context.Diagnostics.WarningAsync(
							string.Concat( "ignoring excess arguments, starting with '", operands[ 0 ], "'" ),
							context.CancellationToken
						).ConfigureAwait( false );
					}
					break;
				}
				if ( 0 == consumed ) { break; }
				baseIndex += consumed;
				firstPass = false;
			} while ( firstPass || baseIndex < operands.Length );
			await context.StandardOutput.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			return overallError ? CommandExitCodes.Failure : CommandExitCodes.Success;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( OverflowException ) {
			await WriteDiagnosticWithoutCancellationAsync( context, "numeric value is too large" ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( IOException ex ) {
			await WriteDiagnosticWithoutCancellationAsync( context, ex.Message ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task FormatPassAsync(
		string format,
		IReadOnlyList<string> operands,
		int baseIndex,
		PassState state,
		CommandContext context
	) {
		var literalStart = 0;
		for ( var index = 0; index < format.Length; index++ ) {
			if ( '%' != format[ index ] ) {
				continue;
			}
			if ( index > literalStart ) {
				await context.StandardOutput.WriteAsync( format.AsMemory( literalStart, index - literalStart ), context.CancellationToken ).ConfigureAwait( false );
			}
			if ( index + 1 < format.Length && '%' == format[ index + 1 ] ) {
				await context.StandardOutput.WriteAsync( "%".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				index++;
				literalStart = index + 1;
				continue;
			}
			if ( !TryParseDirective( format, ref index, out var directive, out var error ) ) {
				await context.Diagnostics.ErrorAsync( error ?? "invalid conversion specification", context.CancellationToken ).ConfigureAwait( false );
				state.HadError = true;
				state.StopOutput = true;
				return;
			}
			state.HadConversion = true;
			var rendered = await RenderDirectiveAsync( directive, operands, baseIndex, state, context ).ConfigureAwait( false );
			if ( null != rendered ) {
				await context.StandardOutput.WriteAsync( rendered.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			}
			if ( state.StopOutput ) {
				return;
			}
			literalStart = index + 1;
		}
		if ( literalStart < format.Length ) {
			await context.StandardOutput.WriteAsync( format.AsMemory( literalStart ), context.CancellationToken ).ConfigureAwait( false );
		}
	}

	private static bool TryParseDirective(
		string format,
		ref int index,
		out Directive directive,
		out string? error
	) {
		directive = null!;
		error = null;
		var cursor = index + 1;
		if ( cursor >= format.Length ) {
			error = "'%' at the end of the format string";
			return false;
		}
		var argumentPosition = ParsePosition( format, ref cursor );
		var flagsStart = cursor;
		while ( cursor < format.Length && "-+ #0'".Contains( format[ cursor ] ) ) {
			cursor++;
		}
		var flags = format.Substring( flagsStart, cursor - flagsStart );
		int? width = null;
		var widthFromArgument = false;
		int? widthPosition = null;
		if ( cursor < format.Length && '*' == format[ cursor ] ) {
			widthFromArgument = true;
			cursor++;
			widthPosition = ParsePosition( format, ref cursor );
		} else {
			width = ParseUnsignedInt( format, ref cursor );
		}
		var hasPrecision = false;
		int? precision = null;
		var precisionFromArgument = false;
		int? precisionPosition = null;
		if ( cursor < format.Length && '.' == format[ cursor ] ) {
			hasPrecision = true;
			cursor++;
			if ( cursor < format.Length && '*' == format[ cursor ] ) {
				precisionFromArgument = true;
				cursor++;
				precisionPosition = ParsePosition( format, ref cursor );
			} else {
				precision = ParseUnsignedInt( format, ref cursor ) ?? 0;
			}
		}
		while ( cursor < format.Length && "hljztL".Contains( format[ cursor ] ) ) {
			cursor++;
		}
		if ( cursor >= format.Length || !"diouxXfFeEgGaAcCsSbq".Contains( format[ cursor ] ) ) {
			error = cursor >= format.Length
				? "missing conversion character"
				: string.Concat( "invalid conversion specification '%", format[ cursor ], "'" );
			return false;
		}
		index = cursor;
		directive = new Directive(
			argumentPosition,
			flags,
			width,
			widthFromArgument,
			widthPosition,
			precision,
			hasPrecision,
			precisionFromArgument,
			precisionPosition,
			format[ cursor ]
		);
		return true;
	}

	private static int? ParsePosition( string format, ref int cursor ) {
		var saved = cursor;
		var number = ParseUnsignedInt( format, ref cursor );
		if ( number.HasValue && 0 < number.Value && cursor < format.Length && '$' == format[ cursor ] ) {
			cursor++;
			return number.Value;
		}
		cursor = saved;
		return null;
	}

	private static int? ParseUnsignedInt( string format, ref int cursor ) {
		if ( cursor >= format.Length || !char.IsAsciiDigit( format[ cursor ] ) ) {
			return null;
		}
		var value = 0;
		while ( cursor < format.Length && char.IsAsciiDigit( format[ cursor ] ) ) {
			value = checked( value * 10 + format[ cursor ] - '0' );
			cursor++;
		}
		return value;
	}

	private static async Task<string?> RenderDirectiveAsync(
		Directive directive,
		IReadOnlyList<string> operands,
		int baseIndex,
		PassState state,
		CommandContext context
	) {
		var flags = directive.Flags;
		var width = directive.Width;
		if ( directive.WidthFromArgument ) {
			var widthText = GetArgument( operands, baseIndex, directive.WidthPosition, state );
			if ( !TryParseInt32( widthText, out var parsedWidth ) ) {
				await WarnInvalidNumberAsync( widthText, context ).ConfigureAwait( false );
				state.HadError = true;
				parsedWidth = 0;
			}
			if ( int.MinValue == parsedWidth ) {
				await WarnInvalidNumberAsync( widthText, context ).ConfigureAwait( false );
				state.HadError = true;
				parsedWidth = 0;
			} else if ( 0 > parsedWidth ) {
				flags = string.Concat( flags, "-" );
				parsedWidth = -parsedWidth;
			}
			width = parsedWidth;
		}
		var precision = directive.Precision;
		var hasPrecision = directive.HasPrecision;
		if ( directive.PrecisionFromArgument ) {
			var precisionText = GetArgument( operands, baseIndex, directive.PrecisionPosition, state );
			if ( !TryParseInt32( precisionText, out var parsedPrecision ) ) {
				await WarnInvalidNumberAsync( precisionText, context ).ConfigureAwait( false );
				state.HadError = true;
				parsedPrecision = 0;
			}
			if ( 0 > parsedPrecision ) {
				hasPrecision = false;
				precision = null;
			} else {
				hasPrecision = true;
				precision = parsedPrecision;
			}
		}
		var argument = GetArgument( operands, baseIndex, directive.ArgumentPosition, state );
		var conversion = directive.Conversion;
		string result;
		switch ( conversion ) {
			case 's':
			case 'S':
				result = argument;
				if ( hasPrecision && precision.HasValue && result.Length > precision.Value ) {
					result = result.Substring( 0, precision.Value );
				}
				return ApplyTextWidth( result, width, flags );
			case 'c':
			case 'C':
				result = 0 == argument.Length ? "\0" : GetFirstRune( argument );
				return ApplyTextWidth( result, width, flags );
			case 'b':
				try {
					var decoded = GnuEscapeDecoder.Decode( argument, allowBareOctal: true, allowStopOutput: true );
					result = decoded.Text;
					if ( hasPrecision && precision.HasValue && result.Length > precision.Value ) {
						result = result.Substring( 0, precision.Value );
					}
					state.StopOutput = decoded.StopOutput;
					return ApplyTextWidth( result, width, flags );
				} catch ( FormatException ex ) {
					await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
					state.HadError = true;
					return string.Empty;
				}
			case 'q':
				result = ShellQuote( argument );
				if ( hasPrecision && precision.HasValue && result.Length > precision.Value ) {
					result = result.Substring( 0, precision.Value );
				}
				return ApplyTextWidth( result, width, flags );
			case 'd':
			case 'i':
			case 'u':
			case 'o':
			case 'x':
			case 'X':
				if ( !TryParseInteger( argument, out var integer ) ) {
					await WarnInvalidNumberAsync( argument, context ).ConfigureAwait( false );
					state.HadError = true;
					integer = BigInteger.Zero;
				}
				return FormatInteger( integer, conversion, flags, width, hasPrecision ? precision : null );
			default:
				if ( !TryParseDouble( argument, out var number ) ) {
					await WarnInvalidNumberAsync( argument, context ).ConfigureAwait( false );
					state.HadError = true;
					number = 0.0;
				}
				return FormatFloating( number, conversion, flags, width, hasPrecision ? precision : null );
		}
	}

	private static string GetArgument(
		IReadOnlyList<string> operands,
		int baseIndex,
		int? position,
		PassState state
	) {
		int relative;
		if ( position.HasValue ) {
			relative = position.Value - 1;
			state.HighestRelativeIndex = Math.Max( state.HighestRelativeIndex, position.Value );
		} else {
			relative = state.SequentialIndex++;
		}
		var absolute = baseIndex + relative;
		return 0 <= absolute && absolute < operands.Count ? operands[ absolute ] : string.Empty;
	}

	private static bool TryParseInt32( string text, out int value ) {
		return int.TryParse( text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value );
	}

	private static bool TryParseInteger( string text, out BigInteger value ) {
		value = BigInteger.Zero;
		if ( string.IsNullOrEmpty( text ) ) {
			return true;
		}
		if ( '\'' == text[ 0 ] || '"' == text[ 0 ] ) {
			value = GetFirstRuneValue( text.Substring( 1 ) );
			return true;
		}
		var sign = 1;
		var index = 0;
		if ( '+' == text[ index ] || '-' == text[ index ] ) {
			sign = '-' == text[ index ] ? -1 : 1;
			index++;
		}
		var radix = 10;
		if ( index + 1 < text.Length && '0' == text[ index ] && ( 'x' == text[ index + 1 ] || 'X' == text[ index + 1 ] ) ) {
			radix = 16;
			index += 2;
		} else if ( index + 1 < text.Length && '0' == text[ index ] ) {
			radix = 8;
			index++;
		}
		if ( index >= text.Length ) {
			value = BigInteger.Zero;
			return true;
		}
		for ( ; index < text.Length; index++ ) {
			var digit = DigitValue( text[ index ] );
			if ( 0 > digit || digit >= radix ) {
				return false;
			}
			value = value * radix + digit;
		}
		value *= sign;
		return true;
	}

	private static bool TryParseDouble( string text, out double value ) {
		if ( string.IsNullOrEmpty( text ) ) {
			value = 0;
			return true;
		}
		if ( '\'' == text[ 0 ] || '"' == text[ 0 ] ) {
			value = GetFirstRuneValue( text.Substring( 1 ) );
			return true;
		}
		return double.TryParse( text, NumberStyles.Float, CultureInfo.CurrentCulture, out value )
			|| double.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out value );
	}

	private static string FormatInteger(
		BigInteger value,
		char conversion,
		string flags,
		int? width,
		int? precision
	) {
		var unsigned = 'u' == conversion || 'o' == conversion || 'x' == conversion || 'X' == conversion;
		if ( unsigned && BigInteger.Zero > value ) {
			var modulus = BigInteger.One << 64;
			value = ( value % modulus + modulus ) % modulus;
		}
		var negative = !unsigned && BigInteger.Zero > value;
		var magnitude = BigInteger.Abs( value );
		var radix = 'o' == conversion ? 8 : ( 'x' == conversion || 'X' == conversion ? 16 : 10 );
		var digits = ToRadix( magnitude, radix, 'X' == conversion );
		if ( precision.HasValue ) {
			if ( 0 == precision.Value && BigInteger.Zero == magnitude ) {
				digits = string.Empty;
			} else {
				digits = digits.PadLeft( precision.Value, '0' );
			}
		}
		if ( flags.Contains( '\'' ) && 10 == radix ) {
			digits = GroupDigits( digits );
		}
		var sign = negative ? "-" : ( flags.Contains( '+' ) ? "+" : ( flags.Contains( ' ' ) ? " " : string.Empty ) );
		var prefix = string.Empty;
		if ( flags.Contains( '#' ) ) {
			if ( 8 == radix && !digits.StartsWith( "0", StringComparison.Ordinal ) ) {
				prefix = "0";
			} else if ( 16 == radix && BigInteger.Zero != magnitude ) {
				prefix = 'X' == conversion ? "0X" : "0x";
			}
		}
		var content = string.Concat( sign, prefix, digits );
		if ( width.HasValue && content.Length < width.Value ) {
			var padding = width.Value - content.Length;
			if ( flags.Contains( '-' ) ) {
				content = content.PadRight( width.Value );
			} else if ( flags.Contains( '0' ) && !precision.HasValue ) {
				content = string.Concat( sign, prefix, new string( '0', padding ), digits );
			} else {
				content = content.PadLeft( width.Value );
			}
		}
		return content;
	}

	private static string FormatFloating(
		double value,
		char conversion,
		string flags,
		int? width,
		int? precision
	) {
		var effectivePrecision = precision ?? 6;
		string content;
		if ( 'a' == conversion || 'A' == conversion ) {
			content = FormatHexFloat( value, effectivePrecision, 'A' == conversion );
		} else {
			var specifier = conversion switch {
				'f' or 'F' => "F",
				'e' => "e",
				'E' => "E",
				'g' => "g",
				'G' => "G",
				_ => "G"
			};
			var p = ( 'g' == conversion || 'G' == conversion ) && 0 == effectivePrecision ? 1 : effectivePrecision;
			content = value.ToString( string.Concat( specifier, p.ToString( CultureInfo.InvariantCulture ) ), CultureInfo.CurrentCulture );
			if ( flags.Contains( '#' ) && !content.Contains( CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal ) ) {
				content = string.Concat( content, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator );
			}
		}
		if ( 0 <= value && !double.IsNaN( value ) ) {
			if ( flags.Contains( '+' ) ) {
				content = string.Concat( "+", content );
			} else if ( flags.Contains( ' ' ) ) {
				content = string.Concat( " ", content );
			}
		}
		if ( width.HasValue && content.Length < width.Value ) {
			if ( flags.Contains( '-' ) ) {
				content = content.PadRight( width.Value );
			} else if ( flags.Contains( '0' ) ) {
				var signLength = content.StartsWith( "+", StringComparison.Ordinal ) || content.StartsWith( '-' ) || content.StartsWith( " ", StringComparison.Ordinal ) ? 1 : 0;
				content = string.Concat( content.Substring( 0, signLength ), new string( '0', width.Value - content.Length ), content.Substring( signLength ) );
			} else {
				content = content.PadLeft( width.Value );
			}
		}
		return content;
	}

	private static string FormatHexFloat( double value, int precision, bool upper ) {
		if ( double.IsNaN( value ) ) { return upper ? "NAN" : "nan"; }
		if ( double.IsPositiveInfinity( value ) ) { return upper ? "INF" : "inf"; }
		if ( double.IsNegativeInfinity( value ) ) { return upper ? "-INF" : "-inf"; }
		if ( 0.0 == value ) { return upper ? "0X0P+0" : "0x0p+0"; }
		var negative = 0 > value;
		value = Math.Abs( value );
		var exponent = Math.ILogB( value );
		var fraction = Math.ScaleB( value, -exponent );
		var builder = new StringBuilder();
		if ( negative ) { builder.Append( '-' ); }
		builder.Append( upper ? "0X1" : "0x1" );
		if ( 0 < precision ) {
			builder.Append( '.' );
			var remainder = fraction - 1.0;
			for ( var i = 0; i < precision; i++ ) {
				remainder *= 16.0;
				var digit = Math.Clamp( (int)Math.Floor( remainder ), 0, 15 );
				builder.Append( "0123456789abcdef"[ digit ] );
				remainder -= digit;
			}
		}
		builder.Append( upper ? 'P' : 'p' );
		builder.Append( 0 <= exponent ? '+' : '-' );
		builder.Append( Math.Abs( exponent ).ToString( CultureInfo.InvariantCulture ) );
		return upper ? builder.ToString().ToUpperInvariant() : builder.ToString();
	}

	private static string ApplyTextWidth( string value, int? width, string flags ) {
		if ( !width.HasValue || value.Length >= width.Value ) {
			return value;
		}
		return flags.Contains( '-' ) ? value.PadRight( width.Value ) : value.PadLeft( width.Value );
	}

	private static string ShellQuote( string value ) {
		if ( 0 == value.Length ) {
			return "''";
		}
		var safe = value.All( character => char.IsAsciiLetterOrDigit( character ) || "_@%+=:,./-".Contains( character ) );
		if ( safe ) {
			return value;
		}
		if ( value.All( character => 0x20 <= character && 0x7e >= character && '\'' != character ) ) {
			return string.Concat( "'", value, "'" );
		}
		var builder = new StringBuilder( "$'" );
		foreach ( var character in value ) {
			switch ( character ) {
				case '\\': builder.Append( "\\\\" ); break;
				case '\'': builder.Append( "\\'" ); break;
				case '\n': builder.Append( "\\n" ); break;
				case '\r': builder.Append( "\\r" ); break;
				case '\t': builder.Append( "\\t" ); break;
				case '\a': builder.Append( "\\a" ); break;
				case '\b': builder.Append( "\\b" ); break;
				case '\f': builder.Append( "\\f" ); break;
				case '\v': builder.Append( "\\v" ); break;
				default:
					if ( char.IsControl( character ) ) {
						builder.Append( "\\x" );
						builder.Append( ( (int)character ).ToString( "x2", CultureInfo.InvariantCulture ) );
					} else {
						builder.Append( character );
					}
					break;
			}
		}
		builder.Append( '\'' );
		return builder.ToString();
	}

	private static int GetFirstRuneValue( string value ) {
		foreach ( var rune in value.EnumerateRunes() ) {
			return rune.Value;
		}
		return 0;
	}

	private static string GetFirstRune( string value ) {
		foreach ( var rune in value.EnumerateRunes() ) {
			return rune.ToString();
		}
		return "\0";
	}

	private static string GroupDigits( string digits ) {
		if ( 0 == digits.Length ) { return digits; }
		var separator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
		if ( string.IsNullOrEmpty( separator ) ) { return digits; }
		var builder = new StringBuilder();
		var initial = digits.Length % 3;
		if ( 0 == initial ) { initial = 3; }
		builder.Append( digits.AsSpan( 0, initial ) );
		for ( var index = initial; index < digits.Length; index += 3 ) {
			builder.Append( separator );
			builder.Append( digits.AsSpan( index, 3 ) );
		}
		return builder.ToString();
	}

	private static string ToRadix( BigInteger value, int radix, bool upper ) {
		if ( BigInteger.Zero == value ) { return "0"; }
		var digits = new StringBuilder();
		while ( BigInteger.Zero < value ) {
			value = BigInteger.DivRem( value, radix, out var remainder );
			digits.Append( "0123456789abcdef"[ (int)remainder ] );
		}
		var array = digits.ToString().ToCharArray();
		Array.Reverse( array );
		var result = new string( array );
		return upper ? result.ToUpperInvariant() : result;
	}

	private static int DigitValue( char value ) {
		if ( '0' <= value && '9' >= value ) { return value - '0'; }
		if ( 'a' <= value && 'f' >= value ) { return value - 'a' + 10; }
		if ( 'A' <= value && 'F' >= value ) { return value - 'A' + 10; }
		return -1;
	}

	private static async Task WarnInvalidNumberAsync( string value, CommandContext context ) {
		await context.Diagnostics.ErrorAsync(
			string.Concat( "'", value, "': expected a numeric value" ),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		const string help = """
Usage: printf FORMAT [ARGUMENT]...
  or:  printf OPTION
Print ARGUMENT(s) according to FORMAT, reusing FORMAT as necessary.

  --help        display this help and exit
  --version     output version information and exit

FORMAT supports C escapes and the conversions diouxXfFeEgGaAcCsSbq.
%b expands escapes in its argument; %q prints a reusable shell representation.
""";
		await writer.WriteLineAsync( help.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteDiagnosticWithoutCancellationAsync( CommandContext context, string message ) {
		try {
			await context.StandardError.WriteLineAsync( string.Concat( context.ProgramName, ": ", message ) ).ConfigureAwait( false );
		} catch ( IOException ) {
			// There is no remaining diagnostic channel.
		}
	}
}
