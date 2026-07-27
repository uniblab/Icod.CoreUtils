using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Time;

namespace Icod.CoreUtils.Date;

public static class Command {
	private const string ProgramName = "date";
	private const string Version = "date (Icod.CoreUtils) 1.0";
	private const string DefaultFormat = "%a %b %e %H:%M:%S %Z %Y";

	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? [],
		new CommandContext(
			ProgramName,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		),
		new SystemDateTimeProvider()
	);

	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IDateTimeProvider dateTimeProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( dateTimeProvider );

		var parser = CreateParser(
			new OptionDefinition( "date", 'd', [ "date" ], OptionValueArity.Required ),
			new OptionDefinition( "debug", null, [ "debug" ] ),
			new OptionDefinition( "file", 'f', [ "file" ], OptionValueArity.Required ),
			new OptionDefinition(
				"iso",
				'I',
				[ "iso-8601" ],
				OptionValueArity.Optional,
				optionalValueMayBeSeparate: false
			),
			new OptionDefinition( "resolution", null, [ "resolution" ] ),
			new OptionDefinition( "rfc-email", 'R', [ "rfc-email", "rfc-822" ] ),
			new OptionDefinition( "rfc-3339", null, [ "rfc-3339" ], OptionValueArity.Required ),
			new OptionDefinition( "reference", 'r', [ "reference" ], OptionValueArity.Required ),
			new OptionDefinition( "set", 's', [ "set" ], OptionValueArity.Required ),
			new OptionDefinition( "utc", 'u', [ "utc", "universal" ] ),
			new OptionDefinition( "help", null, [ "help" ] ),
			new OptionDefinition( "version", null, [ "version" ] )
		);

		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}

			if ( result.HasOption( "help" ) ) {
				const string help = """
Usage: date [OPTION]... [+FORMAT]
  or:  date [-u|--utc|--universal] [MMDDhhmm[[CC]YY][.ss]]
Display date and time in the given FORMAT.
  -d, --date=STRING          display time described by STRING, not now
      --debug                annotate the parsed date on standard error
  -f, --file=DATEFILE        like --date; once for each line of DATEFILE
  -I[FMT], --iso-8601[=FMT]  output ISO 8601 date/time; FMT is date, hours,
                             minutes, seconds, or ns
      --resolution           output the available timestamp resolution
  -R, --rfc-email            output RFC 5322 date and time
      --rfc-3339=FMT         output RFC 3339 date/time; FMT is date, seconds, or ns
  -r, --reference=FILE       display the last modification time of FILE
  -s, --set=STRING           set time described by STRING
  -u, --utc, --universal     print or set Coordinated Universal Time
      --help                 display this help and exit
      --version              output version information and exit
FORMAT controls the output.  Common directives include %Y, %m, %d, %H, %M,
%S, %N, %z, %Z, %s, %F, %T, %R, %a, %A, %b, %B, %c, %x, and %X.
""";
				await context.StandardOutput.WriteAsync(
					help.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					Version.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			if ( result.HasOption( "resolution" ) ) {
				await context.StandardOutput.WriteLineAsync(
					"0.0000001".AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var formatOperands = result.Operands.Where( value => value.StartsWith( '+' ) ).ToArray();
			var nonFormatOperands = result.Operands.Where( value => !value.StartsWith( '+' ) ).ToArray();
			if ( formatOperands.Length > 1 ) {
				await context.Diagnostics.ErrorAsync(
					String.Concat( "extra operand '", formatOperands[ 1 ], "'" ),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var sourceCount = CountSources( result ) + ( nonFormatOperands.Length > 0 ? 1 : 0 );
			if ( sourceCount > 1 ) {
				await context.Diagnostics.ErrorAsync(
					"the options to specify dates are mutually exclusive",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			if ( nonFormatOperands.Length > 1 ) {
				await context.Diagnostics.ErrorAsync(
					String.Concat( "extra operand '", nonFormatOperands[ 1 ], "'" ),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			if ( !ValidateNamedFormats( result, out var formatError ) ) {
				await context.Diagnostics.ErrorAsync(
					formatError,
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var baseTime = result.HasOption( "utc" )
				? dateTimeProvider.UtcNow
				: dateTimeProvider.Now;
			var defaultZone = result.HasOption( "utc" ) ? TimeZoneInfo.Utc : TimeZoneInfo.Local;
			var outputFormat = ResolveFormat( result, formatOperands.FirstOrDefault() );

			if ( result.HasOption( "file" ) ) {
				return await ProcessDateFileAsync(
					result.GetLastValue( "file" )!,
					outputFormat,
					baseTime,
					defaultZone,
					result.HasOption( "debug" ),
					context
				).ConfigureAwait( false );
			}

			DateParseResult parsed;
			if ( result.HasOption( "reference" ) ) {
				var path = result.GetLastValue( "reference" )!;
				try {
					var modified = File.GetLastWriteTimeUtc( path );
					parsed = new DateParseResult(
						true,
						new DateTimeOffset( modified, TimeSpan.Zero ),
						defaultZone,
						String.Concat( "using modification time of '", path, "'" )
					);
				} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or ArgumentException ) {
					await context.Diagnostics.ErrorAsync(
						String.Concat( "cannot stat '", path, "': ", exception.Message ),
						context.CancellationToken
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
			} else {
				var input = result.GetLastValue( "date" )
					?? result.GetLastValue( "set" )
					?? nonFormatOperands.FirstOrDefault();
				parsed = input is null
					? new DateParseResult( true, baseTime, defaultZone, "using current date and time" )
					: GnuDateParser.Parse( input, baseTime, defaultZone );
			}

			if ( !parsed.Success ) {
				await context.Diagnostics.ErrorAsync(
					String.Concat( "invalid date: ", parsed.Diagnostic ),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			if ( result.HasOption( "debug" ) ) {
				await context.StandardError.WriteLineAsync(
					String.Concat(
						ProgramName,
						": parsed date: ",
						parsed.Diagnostic,
						"; result: ",
						parsed.Value.ToString( "O", CultureInfo.InvariantCulture )
					).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
			}

			if ( result.HasOption( "set" ) || nonFormatOperands.Length > 0 ) {
				if ( !await dateTimeProvider.TrySetSystemTimeAsync(
					parsed.Value,
					context.CancellationToken
				).ConfigureAwait( false ) ) {
					await context.Diagnostics.ErrorAsync(
						"cannot set date: operation is not supported or permission was denied",
						context.CancellationToken
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
			}

			await WriteDateAsync( parsed, outputFormat, context ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		}
	}

	private static async Task<int> ProcessDateFileAsync(
		string path,
		string format,
		DateTimeOffset baseTime,
		TimeZoneInfo defaultZone,
		bool debug,
		CommandContext context
	) {
		TextReader reader;
		StreamReader? fileReader = null;
		try {
			if ( path == "-" ) {
				reader = context.StandardInput;
			} else {
				fileReader = new StreamReader(
					new FileStream(
						path,
						FileMode.Open,
						FileAccess.Read,
						FileShare.Read,
						4096,
						FileOptions.Asynchronous | FileOptions.SequentialScan
					),
					detectEncodingFromByteOrderMarks: true
				);
				reader = fileReader;
			}

			var failed = false;
			while ( true ) {
				var line = await reader.ReadLineAsync( context.CancellationToken ).ConfigureAwait( false );
				if ( line is null ) {
					break;
				}

				var parsed = GnuDateParser.Parse( line, baseTime, defaultZone );
				if ( !parsed.Success ) {
					await context.Diagnostics.ErrorAsync(
						String.Concat( "invalid date '", line, "': ", parsed.Diagnostic ),
						context.CancellationToken
					).ConfigureAwait( false );
					failed = true;
					continue;
				}

				if ( debug ) {
					await context.StandardError.WriteLineAsync(
						String.Concat(
							ProgramName,
							": parsed date: ",
							parsed.Diagnostic,
							"; result: ",
							parsed.Value.ToString( "O", CultureInfo.InvariantCulture )
						).AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}

				await WriteDateAsync( parsed, format, context ).ConfigureAwait( false );
			}

			return failed ? CommandExitCodes.Failure : CommandExitCodes.Success;
		} catch ( IOException exception ) {
			await context.Diagnostics.ErrorAsync(
				String.Concat( "cannot read '", path, "': ", exception.Message ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( UnauthorizedAccessException exception ) {
			await context.Diagnostics.ErrorAsync(
				String.Concat( "cannot read '", path, "': ", exception.Message ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} finally {
			if ( fileReader is not null ) {
				fileReader.Dispose();
			}
		}
	}

	private static async Task WriteDateAsync(
		DateParseResult parsed,
		string format,
		CommandContext context
	) {
		var output = GnuDateFormatter.Format(
			parsed.Value,
			format,
			parsed.TimeZone,
			CultureInfo.CurrentCulture
		);
		await context.StandardOutput.WriteLineAsync(
			output.AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static string ResolveFormat( OptionParseResult result, string? formatOperand ) {
		if ( formatOperand is not null ) {
			return formatOperand[ 1.. ];
		}
		if ( result.HasOption( "rfc-email" ) ) {
			return "%a, %d %b %Y %H:%M:%S %z";
		}
		if ( result.HasOption( "rfc-3339" ) ) {
			return result.GetLastValue( "rfc-3339" )?.ToLowerInvariant() switch {
				"date" => "%F",
				"seconds" => "%F %T%:z",
				"ns" => "%F %T.%N%:z",
				_ => DefaultFormat,
			};
		}
		if ( result.HasOption( "iso" ) ) {
			return result.GetLastValue( "iso" )?.ToLowerInvariant() switch {
				null or "" or "date" => "%F",
				"hours" => "%FT%H%:z",
				"minutes" => "%FT%H:%M%:z",
				"seconds" => "%FT%T%:z",
				"ns" => "%FT%T,%N%:z",
				_ => DefaultFormat,
			};
		}

		return DefaultFormat;
	}

	private static bool ValidateNamedFormats(
		OptionParseResult result,
		out string error
	) {
		var iso = result.GetLastValue( "iso" );
		if ( result.HasOption( "iso" )
			&& iso is not null
			&& iso.Length > 0
			&& iso.ToLowerInvariant() is not ( "date" or "hours" or "minutes" or "seconds" or "ns" ) ) {
			error = String.Concat( "invalid argument '", iso, "' for --iso-8601" );
			return false;
		}

		var rfc = result.GetLastValue( "rfc-3339" );
		if ( result.HasOption( "rfc-3339" )
			&& rfc?.ToLowerInvariant() is not ( "date" or "seconds" or "ns" ) ) {
			error = String.Concat( "invalid argument '", rfc, "' for --rfc-3339" );
			return false;
		}

		error = String.Empty;
		return true;
	}

	private static int CountSources( OptionParseResult result ) {
		var count = 0;
		foreach ( var key in new[] { "date", "file", "reference", "set" } ) {
			if ( result.HasOption( key ) ) {
				count++;
			}
		}
		return count;
	}

	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute,
		}
	);

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}
}
