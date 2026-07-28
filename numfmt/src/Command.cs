namespace Icod.CoreUtils.NumFmt;

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Numerics;

/// <summary>Implements <c>numfmt [OPTION]... [NUMBER]...</c>.</summary>
public static partial class Command {
	private const string VersionText = "numfmt (Icod.CoreUtils) 1.0";
	private static readonly char[] ScaleSuffixes = [ 'K', 'M', 'G', 'T', 'P', 'E', 'Z', 'Y', 'R', 'Q' ];

	private enum ScaleMode { None, Auto, Si, Iec, IecI }
	private enum InvalidMode { Abort, Fail, Warn, Ignore }
	private sealed record FieldRange( int Start, int? End ) {
		public bool Contains( int field ) { return field >= this.Start && ( !this.End.HasValue || field <= this.End.Value ); }
	}
	private sealed record NumberFormat(
		string Prefix,
		string Suffix,
		bool LeftAlign,
		bool ZeroPad,
		bool Grouping,
		int? Width,
		int? Precision
	);
	private sealed class Options {
		public bool Debug { get; set; }
		public string? Delimiter { get; set; }
		public List<FieldRange> Fields { get; } = [ new FieldRange( 1, 1 ) ];
		public bool FieldsSpecified { get; set; }
		public NumberFormat? Format { get; set; }
		public ScaleMode From { get; set; }
		public BigInteger FromUnit { get; set; } = BigInteger.One;
		public bool Grouping { get; set; }
		public bool GroupingSpecified { get; set; }
		public ulong Header { get; set; }
		public InvalidMode Invalid { get; set; }
		public int? Padding { get; set; }
		public RationalRoundingMode Rounding { get; set; } = RationalRoundingMode.FromZero;
		public string Suffix { get; set; } = string.Empty;
		public string UnitSeparator { get; set; } = string.Empty;
		public bool UnitSeparatorSpecified { get; set; }
		public ScaleMode To { get; set; }
		public BigInteger ToUnit { get; set; } = BigInteger.One;
		public bool ZeroTerminated { get; set; }
		public List<string> Operands { get; } = [];
	}
	private sealed record ParsedNumber(
		BigRational Value,
		int FractionDigits,
		bool HadScaleSuffix
	);

	/// <summary>Runs the command synchronously for compatibility with legacy callers.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync(
			args,
			new CommandContext(
				"numfmt",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error
			)
		).GetAwaiter().GetResult();
	}

	/// <summary>Runs the command using injected text streams.</summary>
	public static Task<int> RunAsync(
		string[] args,
		TextReader standardInput,
		TextWriter standardOutput,
		TextWriter standardError,
		CancellationToken cancellationToken = default
	) {
		return RunAsync(
			args,
			new CommandContext(
				"numfmt",
				standardInput,
				standardOutput,
				standardError,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>Runs the command using an injected command context.</summary>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			if ( !TryParseOptions( args, out var options, out var optionError, out var showHelp, out var showVersion ) ) {
				await context.Diagnostics.ErrorAsync( optionError ?? "invalid arguments", context.CancellationToken ).ConfigureAwait( false );
				await context.Diagnostics.ErrorAsync( "Try 'numfmt --help' for more information.", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( showHelp ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( showVersion ) {
				await context.StandardOutput.WriteLineAsync( VersionText.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var status = options.Operands.Count > 0
				? await ProcessOperandsAsync( options, context ).ConfigureAwait( false )
				: await ProcessInputAsync( options, context ).ConfigureAwait( false );
			await context.StandardOutput.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			return status;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( IOException ex ) {
			await WriteDiagnosticWithoutCancellationAsync( context, ex.Message ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static bool TryParseOptions(
		IReadOnlyList<string> args,
		out Options options,
		out string? error,
		out bool showHelp,
		out bool showVersion
	) {
		options = new Options();
		error = null;
		showHelp = false;
		showVersion = false;
		var parsingOptions = true;
		for ( var index = 0; index < args.Count; index++ ) {
			var token = args[ index ] ?? string.Empty;
			if ( parsingOptions && "--" == token ) {
				parsingOptions = false;
				continue;
			}
			if ( parsingOptions && token.StartsWith( "--", StringComparison.Ordinal ) && 2 < token.Length ) {
				var equals = token.IndexOf( '=' );
				var name = 0 <= equals ? token.Substring( 2, equals - 2 ) : token.Substring( 2 );
				var attached = 0 <= equals ? token.Substring( equals + 1 ) : null;
				string? RequireValue() {
					if ( null != attached ) { return attached; }
					if ( index + 1 >= args.Count ) { return null; }
					return args[ ++index ];
				}
				switch ( name ) {
					case "help": showHelp = true; break;
					case "version": showVersion = true; break;
					case "debug": options.Debug = true; break;
					case "zero-terminated": options.ZeroTerminated = true; break;
					case "grouping": options.Grouping = true; options.GroupingSpecified = true; break;
					case "delimiter":
						if ( !SetDelimiter( RequireValue(), options, out error ) ) { return false; }
						break;
					case "field":
						if ( !SetFields( RequireValue(), options, out error ) ) { return false; }
						break;
					case "format":
						if ( !TryParseNumberFormat( RequireValue(), out var format, out error ) ) { return false; }
						options.Format = format;
						break;
					case "from":
						if ( !TryParseScale( RequireValue(), allowAuto: true, out var from ) ) { error = "invalid --from value"; return false; }
						options.From = from;
						break;
					case "to":
						if ( !TryParseScale( RequireValue(), allowAuto: false, out var to ) ) { error = "invalid --to value"; return false; }
						options.To = to;
						break;
					case "from-unit":
						if ( !TryParseUnit( RequireValue(), out var fromUnit ) ) { error = "invalid --from-unit value"; return false; }
						options.FromUnit = fromUnit;
						break;
					case "to-unit":
						if ( !TryParseUnit( RequireValue(), out var toUnit ) ) { error = "invalid --to-unit value"; return false; }
						options.ToUnit = toUnit;
						break;
					case "round":
						if ( !TryParseRounding( RequireValue(), out var rounding ) ) { error = "invalid --round value"; return false; }
						options.Rounding = rounding;
						break;
					case "invalid":
						if ( !TryParseInvalid( RequireValue(), out var invalid ) ) { error = "invalid --invalid value"; return false; }
						options.Invalid = invalid;
						break;
					case "padding":
						if ( !int.TryParse( RequireValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var padding ) || 0 == padding || int.MinValue == padding ) { error = "invalid --padding value"; return false; }
						options.Padding = padding;
						break;
					case "suffix": {
						var suffixValue = RequireValue();
						if ( null == suffixValue ) { error = "option '--suffix' requires an argument"; return false; }
						options.Suffix = suffixValue;
						break;
					}
					case "unit-separator": {
						var separatorValue = RequireValue();
						if ( null == separatorValue ) { error = "option '--unit-separator' requires an argument"; return false; }
						options.UnitSeparator = separatorValue;
						options.UnitSeparatorSpecified = true;
						break;
					}
					case "header":
						if ( null == attached ) { options.Header = 1; }
						else if ( !ulong.TryParse( attached, NumberStyles.None, CultureInfo.InvariantCulture, out var header ) || 0 == header ) { error = "invalid --header value"; return false; }
						else { options.Header = header; }
						break;
					default: error = string.Concat( "unrecognized option '--", name, "'" ); return false;
				}
				continue;
			}
			if ( parsingOptions && token.StartsWith( "-", StringComparison.Ordinal ) && "-" != token ) {
				if ( "-z" == token ) { options.ZeroTerminated = true; continue; }
				if ( token.StartsWith( "-d", StringComparison.Ordinal ) ) {
					var value = 2 < token.Length ? token.Substring( 2 ) : ( index + 1 < args.Count ? args[ ++index ] : null );
					if ( !SetDelimiter( value, options, out error ) ) { return false; }
					continue;
				}
				error = string.Concat( "invalid option -- '", token, "'" );
				return false;
			}
			options.Operands.Add( token );
		}
		if ( options.GroupingSpecified && null != options.Format ) { error = "--grouping cannot be combined with --format"; return false; }
		if ( options.Format is { Grouping: true } ) { options.Grouping = true; }
		if ( options.Grouping && ScaleMode.None != options.To ) { error = "grouping cannot be combined with --to"; return false; }
		return true;
	}

	private static bool SetDelimiter( string? value, Options options, out string? error ) {
		error = null;
		if ( null == value ) { error = "option '--delimiter' requires an argument"; return false; }
		if ( 1 < CountRunes( value ) ) { error = "the delimiter must be a single character"; return false; }
		options.Delimiter = value;
		return true;
	}

	private static int CountRunes( string value ) {
		var count = 0;
		foreach ( var unused in value.EnumerateRunes() ) { count++; }
		return count;
	}

	private static bool SetFields( string? value, Options options, out string? error ) {
		error = null;
		if ( null == value ) { error = "option '--field' requires an argument"; return false; }
		if ( options.FieldsSpecified ) { error = "multiple field specifications"; return false; }
		if ( !TryParseFields( value, out var fields ) ) { error = string.Concat( "invalid field specification '", value, "'" ); return false; }
		options.Fields.Clear();
		options.FieldsSpecified = true;
		options.Fields.AddRange( fields );
		return true;
	}

	private static bool TryParseFields( string text, out List<FieldRange> fields ) {
		fields = [];
		if ( "-" == text ) { fields.Add( new FieldRange( 1, null ) ); return true; }
		foreach ( var part in text.Split( ',', StringSplitOptions.None ) ) {
			if ( 0 == part.Length ) { return false; }
			var dash = part.IndexOf( '-' );
			if ( 0 > dash ) {
				if ( !int.TryParse( part, NumberStyles.None, CultureInfo.InvariantCulture, out var single ) || 1 > single ) { return false; }
				fields.Add( new FieldRange( single, single ) );
				continue;
			}
			if ( part.IndexOf( '-', dash + 1 ) >= 0 ) { return false; }
			var left = part.Substring( 0, dash );
			var right = part.Substring( dash + 1 );
			var start = 1;
			int? end = null;
			if ( 0 < left.Length && ( !int.TryParse( left, NumberStyles.None, CultureInfo.InvariantCulture, out start ) || 1 > start ) ) { return false; }
			if ( 0 < right.Length ) {
				if ( !int.TryParse( right, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedEnd ) || parsedEnd < start ) { return false; }
				end = parsedEnd;
			}
			fields.Add( new FieldRange( start, end ) );
		}
		return 0 < fields.Count;
	}

	private static bool TryParseNumberFormat( string? text, out NumberFormat? format, out string? error ) {
		format = null;
		error = null;
		if ( null == text ) { error = "option '--format' requires an argument"; return false; }
		var matches = PercentDirectiveRegex().Matches( text );
		if ( 1 != matches.Count ) { error = "format must contain exactly one %f directive"; return false; }
		var match = matches[ 0 ];
		var escapedPercentCount = 0;
		for ( var index = 0; index < text.Length - 1; index++ ) { if ( '%' == text[ index ] && '%' == text[ index + 1 ] ) { escapedPercentCount++; index++; } }
		var unescaped = text.Count( character => '%' == character ) - escapedPercentCount * 2;
		if ( 1 != unescaped ) { error = "format must contain exactly one %f directive"; return false; }
		var flags = match.Groups[ "flags" ].Value;
		int? width = null;
		if ( match.Groups[ "width" ].Success ) {
			if ( !int.TryParse( match.Groups[ "width" ].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWidth ) || 10000 < parsedWidth ) { error = "format width is too large"; return false; }
			width = parsedWidth;
		}
		int? precision = null;
		if ( match.Groups[ "precision" ].Success ) {
			if ( !int.TryParse( match.Groups[ "precision" ].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPrecision ) || 10000 < parsedPrecision ) { error = "format precision is too large"; return false; }
			precision = parsedPrecision;
		}
		format = new NumberFormat(
			text.Substring( 0, match.Index ).Replace( "%%", "%", StringComparison.Ordinal ),
			text.Substring( match.Index + match.Length ).Replace( "%%", "%", StringComparison.Ordinal ),
			flags.Contains( '-' ),
			flags.Contains( '0' ),
			flags.Contains( '\'' ),
			width,
			precision
		);
		return true;
	}

	[GeneratedRegex( @"%(?<flags>[-0']*)(?<width>\d+)?(?:\.(?<precision>\d+))?f", RegexOptions.CultureInvariant )]
	private static partial Regex PercentDirectiveRegex();

	private static bool TryParseScale( string? text, bool allowAuto, out ScaleMode mode ) {
		mode = text switch {
			"none" => ScaleMode.None,
			"auto" when allowAuto => ScaleMode.Auto,
			"si" => ScaleMode.Si,
			"iec" => ScaleMode.Iec,
			"iec-i" => ScaleMode.IecI,
			_ => (ScaleMode)( -1 )
		};
		return 0 <= (int)mode;
	}

	private static bool TryParseUnit( string? text, out BigInteger unit ) {
		unit = BigInteger.Zero;
		if ( string.IsNullOrEmpty( text ) ) { return false; }
		var suffixLength = text.EndsWith( "i", StringComparison.OrdinalIgnoreCase ) ? 2 : 1;
		var power = 0;
		var baseValue = 1000;
		var numeric = text;
		if ( 1 < text.Length ) {
			var suffixIndex = text.Length - suffixLength;
			power = GetSuffixPower( text[ suffixIndex ] );
			if ( 0 < power ) {
				baseValue = 2 == suffixLength ? 1024 : 1000;
				numeric = text.Substring( 0, suffixIndex );
			}
		}
		if ( !BigInteger.TryParse( numeric, NumberStyles.None, CultureInfo.InvariantCulture, out unit ) || BigInteger.Zero >= unit ) { return false; }
		if ( 0 < power ) { unit *= BigInteger.Pow( baseValue, power ); }
		return true;
	}

	private static bool TryParseRounding( string? text, out RationalRoundingMode mode ) {
		mode = text switch {
			"up" => RationalRoundingMode.Up,
			"down" => RationalRoundingMode.Down,
			"from-zero" => RationalRoundingMode.FromZero,
			"towards-zero" => RationalRoundingMode.TowardsZero,
			"nearest" => RationalRoundingMode.Nearest,
			_ => (RationalRoundingMode)( -1 )
		};
		return 0 <= (int)mode;
	}

	private static bool TryParseInvalid( string? text, out InvalidMode mode ) {
		mode = text switch {
			"abort" => InvalidMode.Abort,
			"fail" => InvalidMode.Fail,
			"warn" => InvalidMode.Warn,
			"ignore" => InvalidMode.Ignore,
			_ => (InvalidMode)( -1 )
		};
		return 0 <= (int)mode;
	}

	private static async Task<int> ProcessOperandsAsync( Options options, CommandContext context ) {
		var failure = false;
		var terminator = options.ZeroTerminated ? "\0" : Environment.NewLine;
		foreach ( var operand in options.Operands ) {
			var conversion = await ConvertTokenAsync( operand, options, context ).ConfigureAwait( false );
			if ( conversion.Failed ) {
				failure = true;
				if ( InvalidMode.Abort == options.Invalid ) { return CommandExitCodes.UsageError; }
			}
			await context.StandardOutput.WriteAsync( string.Concat( conversion.Text, terminator ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		}
		return failure && InvalidMode.Fail == options.Invalid ? CommandExitCodes.UsageError : CommandExitCodes.Success;
	}

	private static async Task<int> ProcessInputAsync( Options options, CommandContext context ) {
		var inputTerminator = options.ZeroTerminated ? '\0' : '\n';
		var outputTerminator = options.ZeroTerminated ? "\0" : Environment.NewLine;
		var failure = false;
		ulong recordIndex = 0;
		await foreach ( var inputRecord in ReadRecordsAsync( context.StandardInput, inputTerminator, context.CancellationToken ) ) {
			var record = inputRecord.Text;
			if ( !options.ZeroTerminated && record.EndsWith( "\r", StringComparison.Ordinal ) ) { record = record.Substring( 0, record.Length - 1 ); }
			string output;
			if ( recordIndex < options.Header ) {
				output = record;
			} else {
				var transformed = await ConvertRecordAsync( record, options, context ).ConfigureAwait( false );
				output = transformed.Text;
				if ( transformed.Failed ) {
					failure = true;
					if ( InvalidMode.Abort == options.Invalid ) { return CommandExitCodes.UsageError; }
				}
			}
			await context.StandardOutput.WriteAsync(
				( inputRecord.Terminated ? string.Concat( output, outputTerminator ) : output ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
			recordIndex++;
		}
		return failure && InvalidMode.Fail == options.Invalid ? CommandExitCodes.UsageError : CommandExitCodes.Success;
	}

	private static async IAsyncEnumerable<(string Text, bool Terminated)> ReadRecordsAsync(
		TextReader reader,
		char terminator,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		var buffer = new char[ 4096 ];
		var record = new StringBuilder();
		while ( true ) {
			var count = await reader.ReadAsync( buffer.AsMemory(), cancellationToken ).ConfigureAwait( false );
			if ( 0 == count ) { break; }
			for ( var index = 0; index < count; index++ ) {
				if ( terminator == buffer[ index ] ) {
					yield return ( record.ToString(), true );
					record.Clear();
				} else {
					record.Append( buffer[ index ] );
				}
			}
		}
		if ( 0 < record.Length ) { yield return ( record.ToString(), false ); }
	}

	private static async Task<(string Text, bool Failed)> ConvertRecordAsync( string record, Options options, CommandContext context ) {
		if ( null != options.Delimiter ) {
			if ( 0 == options.Delimiter.Length ) { return await ConvertTokenAsync( record, options, context ).ConfigureAwait( false ); }
			var delimiter = options.Delimiter;
			var fields = record.Split( delimiter, StringSplitOptions.None );
			var failed = false;
			for ( var index = 0; index < fields.Length; index++ ) {
				if ( !IsSelected( index + 1, options.Fields ) ) { continue; }
				var result = await ConvertTokenAsync( fields[ index ], options, context ).ConfigureAwait( false );
				fields[ index ] = result.Text;
				failed |= result.Failed;
				if ( result.Failed && InvalidMode.Abort == options.Invalid ) { break; }
			}
			return ( string.Join( delimiter, fields ), failed );
		}
		var matches = NonWhitespaceRegex().Matches( record );
		if ( 0 == matches.Count ) { return ( record, false ); }
		var builder = new StringBuilder( record.Length );
		var cursor = 0;
		var anyFailed = false;
		for ( var index = 0; index < matches.Count; index++ ) {
			var match = matches[ index ];
			builder.Append( record.AsSpan( cursor, match.Index - cursor ) );
			if ( IsSelected( index + 1, options.Fields ) ) {
				var result = await ConvertTokenAsync( match.Value, options, context ).ConfigureAwait( false );
				var value = result.Text;
				if ( !options.Padding.HasValue && null == options.Format && value.Length < match.Length ) { value = value.PadLeft( match.Length ); }
				builder.Append( value );
				anyFailed |= result.Failed;
			} else {
				builder.Append( match.Value );
			}
			cursor = match.Index + match.Length;
		}
		builder.Append( record.AsSpan( cursor ) );
		return ( builder.ToString(), anyFailed );
	}

	[GeneratedRegex( @"\S+", RegexOptions.CultureInvariant )]
	private static partial Regex NonWhitespaceRegex();

	private static bool IsSelected( int field, IEnumerable<FieldRange> ranges ) {
		return ranges.Any( range => range.Contains( field ) );
	}

	private static async Task<(string Text, bool Failed)> ConvertTokenAsync( string token, Options options, CommandContext context ) {
		if ( !TryParseNumber( token, options, out var parsed, out var error ) ) {
			var diagnose = InvalidMode.Abort == options.Invalid || InvalidMode.Fail == options.Invalid || InvalidMode.Warn == options.Invalid || options.Debug;
			if ( diagnose ) {
				await context.Diagnostics.ErrorAsync( error ?? string.Concat( "invalid number: '", token, "'" ), context.CancellationToken ).ConfigureAwait( false );
			}
			return ( token, true );
		}
		var value = parsed.Value * options.FromUnit;
		var scaleSuffix = string.Empty;
		var scalePower = 0;
		var scaleBase = ScaleMode.Si == options.To ? 1000 : 1024;
		if ( ScaleMode.None != options.To ) {
			var magnitude = value.Numerator.Sign < 0 ? -value : value;
			while ( scalePower < ScaleSuffixes.Length && magnitude >= new BigRational( BigInteger.Pow( scaleBase, scalePower + 1 ), BigInteger.One ) ) { scalePower++; }
			if ( 0 < scalePower ) { value /= BigInteger.Pow( scaleBase, scalePower ); }
		}
		value /= options.ToUnit;
		var precision = DeterminePrecision( value, parsed, options );
		if ( ScaleMode.None != options.To && scalePower < ScaleSuffixes.Length ) {
			var roundedAtPrecision = BigInteger.Abs(
				( value * BigInteger.Pow( 10, precision ) ).Round( options.Rounding )
			);
			if ( roundedAtPrecision >= new BigInteger( scaleBase ) * BigInteger.Pow( 10, precision ) ) {
				value /= scaleBase;
				scalePower++;
				precision = DeterminePrecision( value, parsed, options );
			}
		}
		if ( 0 < scalePower ) {
			scaleSuffix = ScaleSuffixes[ scalePower - 1 ].ToString();
			if ( ScaleMode.Si == options.To && 1 == scalePower ) { scaleSuffix = "k"; }
			if ( ScaleMode.IecI == options.To ) { scaleSuffix = string.Concat( scaleSuffix, "i" ); }
		}
		var number = value.ToFixedString( precision, options.Rounding );
		if ( null == options.Format && ScaleMode.None == options.To && BigInteger.One == options.ToUnit ) {
			number = value.ToDecimalString(
				parsed.HadScaleSuffix ? 0 : parsed.FractionDigits,
				Math.Max( parsed.FractionDigits, 18 ),
				options.Rounding,
				trimTrailingZeroes: parsed.HadScaleSuffix
			);
		}
		var useGrouping = options.Grouping || options.Format is { Grouping: true };
		if ( useGrouping ) { number = ApplyGrouping( number ); }
		var unit = string.Concat( scaleSuffix, options.Suffix );
		var outputSeparator = 0 < scalePower ? options.UnitSeparator : string.Empty;
		var rendered = string.Concat( number, outputSeparator, unit );
		if ( null != options.Format ) {
			rendered = ApplyNumberFormat( number, unit, outputSeparator, options.Format );
		} else if ( options.Padding.HasValue ) {
			rendered = ApplyPadding( rendered, options.Padding.Value, zeroPad: false );
		}
		if ( options.Debug && ScaleMode.None == options.From && ScaleMode.None == options.To && BigInteger.One == options.FromUnit && BigInteger.One == options.ToUnit ) {
			await context.StandardError.WriteLineAsync(
				string.Concat( context.ProgramName, ": no conversion option specified for '", token, "'" ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return ( rendered, false );
	}

	private static bool TryParseNumber( string token, Options options, out ParsedNumber parsed, out string? error ) {
		parsed = null!;
		error = null;
		var valueText = token;
		var removedCustomSuffix = false;
		if ( 0 < options.Suffix.Length && valueText.EndsWith( options.Suffix, StringComparison.Ordinal ) ) {
			valueText = valueText.Substring( 0, valueText.Length - options.Suffix.Length );
			removedCustomSuffix = true;
		}
		if ( removedCustomSuffix && 0 < options.UnitSeparator.Length && valueText.EndsWith( options.UnitSeparator, StringComparison.Ordinal ) ) {
			valueText = valueText.Substring( 0, valueText.Length - options.UnitSeparator.Length );
		}
		if ( options.Grouping ) {
			var separator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
			if ( 0 < separator.Length ) { valueText = valueText.Replace( separator, string.Empty, StringComparison.Ordinal ); }
		}
		valueText = valueText.TrimEnd();
		var scalePower = 0;
		var scaleBase = 1000;
		var hadScale = false;
		if ( ScaleMode.None != options.From && 0 < valueText.Length ) {
			var last = valueText[ ^1 ];
			var hasI = 'i' == last || 'I' == last;
			var suffixIndex = hasI ? valueText.Length - 2 : valueText.Length - 1;
			if ( 0 <= suffixIndex ) {
				scalePower = GetSuffixPower( valueText[ suffixIndex ] );
				if ( 0 < scalePower ) {
					hadScale = true;
					if ( ScaleMode.IecI == options.From && !hasI ) { error = string.Concat( "missing 'i' suffix in input: '", token, "'" ); return false; }
					if ( ScaleMode.Si == options.From && hasI ) { error = string.Concat( "invalid suffix in input '", token, "'" ); return false; }
					if ( ScaleMode.Iec == options.From && hasI ) { error = string.Concat( "invalid suffix in input '", token, "'" ); return false; }
					scaleBase = options.From switch { ScaleMode.Iec or ScaleMode.IecI => 1024, ScaleMode.Auto when hasI => 1024, _ => 1000 };
					valueText = valueText.Substring( 0, suffixIndex );
					if ( !RemoveScaleSeparator( valueText, options, out var numberText ) ) {
						error = string.Concat( "invalid suffix in input '", token, "'" );
						return false;
					}
					valueText = numberText;
				}
			}
		}
		if ( ScaleMode.None == options.From && 0 < valueText.Length && GetSuffixPower( valueText[ ^1 ] ) > 0 ) {
			error = string.Concat( "invalid suffix in input '", token, "'" );
			return false;
		}
		if ( !BigRational.TryParseDecimal( valueText, out var value, out var fractionDigits ) ) {
			error = string.Concat( "invalid number: '", token, "'" );
			return false;
		}
		if ( 0 < scalePower ) { value *= BigInteger.Pow( scaleBase, scalePower ); }
		parsed = new ParsedNumber( value, fractionDigits, hadScale );
		return true;
	}

	private static bool RemoveScaleSeparator( string source, Options options, out string number ) {
		number = source;
		if ( options.UnitSeparatorSpecified ) {
			if ( 0 == options.UnitSeparator.Length ) {
				return 0 == source.Length || !char.IsWhiteSpace( source[ ^1 ] );
			}
			if ( source.EndsWith( options.UnitSeparator, StringComparison.Ordinal ) ) {
				number = source.Substring( 0, source.Length - options.UnitSeparator.Length );
				return true;
			}
		}
		if ( 0 < source.Length && char.IsWhiteSpace( source[ ^1 ] ) ) {
			number = source.Substring( 0, source.Length - 1 );
			return 0 == number.Length || !char.IsWhiteSpace( number[ ^1 ] );
		}
		return true;
	}

	private static int DeterminePrecision( BigRational value, ParsedNumber parsed, Options options ) {
		if ( options.Format?.Precision is int specified ) { return specified; }
		if ( ScaleMode.None != options.To ) {
			var magnitude = value.Numerator.Sign < 0 ? -value : value;
			return magnitude < new BigRational( 10, 1 ) ? 1 : 0;
		}
		if ( BigInteger.One != options.ToUnit ) { return 0; }
		return parsed.HadScaleSuffix ? 0 : parsed.FractionDigits;
	}

	private static string ApplyNumberFormat( string number, string unit, string separator, NumberFormat format ) {
		var formatted = number;
		if ( format.Width.HasValue ) { formatted = ApplyPadding( formatted, format.LeftAlign ? -format.Width.Value : format.Width.Value, format.ZeroPad ); }
		return string.Concat( format.Prefix, formatted, 0 < unit.Length ? separator : string.Empty, unit, format.Suffix );
	}

	private static string ApplyPadding( string value, int width, bool zeroPad ) {
		var length = Math.Abs( width );
		if ( value.Length >= length ) { return value; }
		if ( 0 > width ) { return value.PadRight( length ); }
		if ( !zeroPad ) { return value.PadLeft( length ); }
		var signLength = value.StartsWith( "-", StringComparison.Ordinal ) || value.StartsWith( "+", StringComparison.Ordinal ) ? 1 : 0;
		return string.Concat( value.Substring( 0, signLength ), new string( '0', length - value.Length ), value.Substring( signLength ) );
	}

	private static string ApplyGrouping( string value ) {
		var decimalIndex = value.IndexOf( '.', StringComparison.Ordinal );
		var integer = 0 <= decimalIndex ? value.Substring( 0, decimalIndex ) : value;
		var fraction = 0 <= decimalIndex ? value.Substring( decimalIndex ) : string.Empty;
		var sign = integer.StartsWith( "-", StringComparison.Ordinal ) || integer.StartsWith( "+", StringComparison.Ordinal ) ? integer.Substring( 0, 1 ) : string.Empty;
		if ( 0 < sign.Length ) { integer = integer.Substring( 1 ); }
		var separator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
		if ( string.IsNullOrEmpty( separator ) ) { return value; }
		var builder = new StringBuilder();
		var first = integer.Length % 3;
		if ( 0 == first ) { first = Math.Min( 3, integer.Length ); }
		builder.Append( sign );
		builder.Append( integer.AsSpan( 0, first ) );
		for ( var index = first; index < integer.Length; index += 3 ) {
			builder.Append( separator );
			builder.Append( integer.AsSpan( index, Math.Min( 3, integer.Length - index ) ) );
		}
		builder.Append( fraction );
		return builder.ToString();
	}

	private static int GetSuffixPower( char suffix ) {
		return char.ToUpperInvariant( suffix ) switch {
			'K' => 1, 'M' => 2, 'G' => 3, 'T' => 4, 'P' => 5,
			'E' => 6, 'Z' => 7, 'Y' => 8, 'R' => 9, 'Q' => 10,
			_ => 0
		};
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		const string help = """
Usage: numfmt [OPTION]... [NUMBER]...
Reformat NUMBER(s), or numbers from standard input if none are specified.

      --debug              print warnings about invalid input
  -d, --delimiter=X        use X instead of whitespace for field delimiter
      --field=FIELDS       replace selected cut-style fields (default 1)
      --format=FORMAT      use one printf-style %f directive
      --from=UNIT          input scale: none, auto, si, iec, iec-i
      --from-unit=N        input unit size
      --grouping           use locale-defined digit grouping
      --header[=N]         pass through first N input records (default 1)
      --invalid=MODE       abort, fail, warn, or ignore
      --padding=N          pad converted values
      --round=METHOD       up, down, from-zero, towards-zero, nearest
      --suffix=SUFFIX      accept and append SUFFIX
      --unit-separator=SEP separate the number and output unit
      --to=UNIT            output scale: none, si, iec, iec-i
      --to-unit=N          output unit size
  -z, --zero-terminated    use NUL record terminators
      --help               display this help and exit
      --version            output version information and exit
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
