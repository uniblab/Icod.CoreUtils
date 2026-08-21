namespace Icod.CoreUtils.NL;

using System.Globalization;
using System.Numerics;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.RegularExpressions;
using Icod.CommandFramework.Text;

/// <summary>Implements GNU <c>nl</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>nl [OPTION]... [FILE]...</code>
/// <para>All operands are processed as one logical document with section-aware numbering.</para>
/// </remarks>
public static class Command {
	private const string BlankJoinKey = "join-blank-lines";
	private const string BodyKey = "body-numbering";
	private const string DelimiterKey = "section-delimiter";
	private const string FooterKey = "footer-numbering";
	private const string FormatKey = "number-format";
	private const string HeaderKey = "header-numbering";
	private const string HelpKey = "help";
	private const string IncrementKey = "line-increment";
	private const string NoRenumberKey = "no-renumber";
	private const string SeparatorKey = "number-separator";
	private const string StartKey = "starting-line-number";
	private const string VersionKey = "version";
	private const string WidthKey = "number-width";

	/// <summary>Runs <c>nl</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null
	) {
		return RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>nl</c> asynchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null,
		CancellationToken cancellationToken = default
	) {
		standardInput ??= Console.In;
		standardOutput ??= Console.Out;
		standardError ??= Console.Error;
		using var inputAdapter = new TextReaderStream( standardInput, leaveOpen: true );
		return await RunAsync(
			args,
			new CommandContext(
				"nl",
				standardInput,
				standardOutput,
				standardError,
				inputAdapter,
				null,
				null,
				cancellationToken
			)
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>nl</c> asynchronously against a command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await WriteOptionErrorAsync( parsed.Errors[0], context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var terminal = parsed.Options.FirstOrDefault(
				option => option.Definition.Key is HelpKey or VersionKey
			);
			if ( terminal?.Definition.Key == HelpKey ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( terminal?.Definition.Key == VersionKey ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var locale = TextLocaleEnvironment.Resolve();
			var options = await CreateOptionsAsync( parsed, locale, context ).ConfigureAwait( false );
			if ( options is null ) {
				return CommandExitCodes.Failure;
			}
			if ( RequiresStandardInput( options.Operands ) && context.StandardInputStream is null ) {
				await context.Diagnostics.ErrorAsync(
					"a binary standard-input stream was not supplied",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			var success = await new NlProcessor( options, locale )
				.ProcessAsync( context, output )
				.ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return success ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( OverflowException ) {
			try {
				await context.Diagnostics.ErrorAsync( "value is too large", CancellationToken.None ).ConfigureAwait( false );
			} catch { }
			return CommandExitCodes.Failure;
		} catch ( IOException exception ) {
			try {
				await context.Diagnostics.ErrorAsync(
					string.Concat( "write failed: ", exception.Message ),
					CancellationToken.None
				).ConfigureAwait( false );
			} catch { }
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			new[] {
				new OptionDefinition( BodyKey, 'b', new[] { "body-numbering" }, OptionValueArity.Required ),
				new OptionDefinition( DelimiterKey, 'd', new[] { "section-delimiter" }, OptionValueArity.Required ),
				new OptionDefinition( FooterKey, 'f', new[] { "footer-numbering" }, OptionValueArity.Required ),
				new OptionDefinition( HeaderKey, 'h', new[] { "header-numbering" }, OptionValueArity.Required ),
				new OptionDefinition( IncrementKey, 'i', new[] { "line-increment" }, OptionValueArity.Required ),
				new OptionDefinition( BlankJoinKey, 'l', new[] { "join-blank-lines" }, OptionValueArity.Required ),
				new OptionDefinition( FormatKey, 'n', new[] { "number-format" }, OptionValueArity.Required ),
				new OptionDefinition( NoRenumberKey, 'p', new[] { "no-renumber" } ),
				new OptionDefinition( SeparatorKey, 's', new[] { "number-separator" }, OptionValueArity.Required ),
				new OptionDefinition( StartKey, 'v', new[] { "starting-line-number" }, OptionValueArity.Required ),
				new OptionDefinition( WidthKey, 'w', new[] { "number-width" }, OptionValueArity.Required ),
				new OptionDefinition( HelpKey, null, new[] { "help" } ),
				new OptionDefinition( VersionKey, null, new[] { "version" } )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static async Task<NlOptions?> CreateOptionsAsync(
		OptionParseResult parsed,
		ITextLocaleProvider locale,
		CommandContext context
	) {
		var body = NlNumberingStyle.Nonempty;
		var footer = NlNumberingStyle.None;
		var header = NlNumberingStyle.None;
		var delimiter = NlSectionDelimiter.Parse( "\\:", locale.DecodingMode );
		var increment = 1L;
		var blankJoin = 1L;
		var format = NlNumberFormat.Right;
		var separator = "\t";
		var start = 1L;
		var width = 6;
		var expressionProvider = new GnuBasicRegularExpressionProvider(
			locale.DecodingMode == TextDecodingMode.Bytes
				? PosixCLocaleRegularExpressionCharacterClassProvider.Instance
				: UnicodeRegularExpressionCharacterClassProvider.CurrentCulture
		);

		foreach ( var option in parsed.Options ) {
			switch ( option.Definition.Key ) {
				case BodyKey:
					body = await ParseStyleAsync( option.Value, "body", expressionProvider, context ).ConfigureAwait( false );
					if ( body is null ) {
						return null;
					}
					break;
				case FooterKey:
					footer = await ParseStyleAsync( option.Value, "footer", expressionProvider, context ).ConfigureAwait( false );
					if ( footer is null ) {
						return null;
					}
					break;
				case HeaderKey:
					header = await ParseStyleAsync( option.Value, "header", expressionProvider, context ).ConfigureAwait( false );
					if ( header is null ) {
						return null;
					}
					break;
				case DelimiterKey:
					delimiter = NlSectionDelimiter.Parse( option.Value ?? string.Empty, locale.DecodingMode );
					break;
				case IncrementKey:
					if ( !TryParseSigned( option.Value, out increment ) ) {
						await WriteInvalidNumberAsync( context, "line increment", option.Value ).ConfigureAwait( false );
						return null;
					}
					break;
				case BlankJoinKey:
					if ( !TryParseBlankJoin( option.Value, out blankJoin ) ) {
						await WriteInvalidNumberAsync( context, "line number of blank lines", option.Value ).ConfigureAwait( false );
						return null;
					}
					break;
				case FormatKey:
					if ( !TryParseFormat( option.Value, out format ) ) {
						await context.Diagnostics.ErrorAsync(
							string.Concat( "invalid line numbering format: '", option.Value ?? string.Empty, "'" ),
							context.CancellationToken
						).ConfigureAwait( false );
						return null;
					}
					break;
				case SeparatorKey:
					separator = option.Value ?? string.Empty;
					break;
				case StartKey:
					if ( !TryParseSigned( option.Value, out start ) ) {
						await WriteInvalidNumberAsync( context, "starting line number", option.Value ).ConfigureAwait( false );
						return null;
					}
					break;
				case WidthKey:
					if ( !TryParsePositiveInt( option.Value, out width ) ) {
						await WriteInvalidNumberAsync( context, "line number field width", option.Value ).ConfigureAwait( false );
						return null;
					}
					break;
			}
		}
		return new NlOptions(
			header,
			body,
			footer,
			delimiter,
			increment,
			blankJoin,
			format,
			!parsed.HasOption( NoRenumberKey ),
			separator,
			start,
			width,
			parsed.Operands
		);
	}

	private static async Task<NlNumberingStyle?> ParseStyleAsync(
		string? value,
		string sectionName,
		GnuBasicRegularExpressionProvider provider,
		CommandContext context
	) {
		value ??= string.Empty;
		if ( value.StartsWith( 'a' ) ) {
			return NlNumberingStyle.All;
		}
		if ( value.StartsWith( 't' ) ) {
			return NlNumberingStyle.Nonempty;
		}
		if ( value.StartsWith( 'n' ) ) {
			return NlNumberingStyle.None;
		}
		if ( value.StartsWith( 'p' ) ) {
			var pattern = value[1..];
			var compiled = await provider.CompileAsync(
				pattern,
				RegularExpressionOptions.GnuExprCompatibility,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( compiled.Expression is not null ) {
				return NlNumberingStyle.CreatePattern( pattern, compiled.Expression );
			}
			await context.Diagnostics.ErrorAsync(
				string.Concat( "invalid regular expression: ", compiled.Diagnostic?.Message ?? "unknown error" ),
				context.CancellationToken
			).ConfigureAwait( false );
			return null;
		}
		await context.Diagnostics.ErrorAsync(
			string.Concat( "invalid ", sectionName, " numbering style: '", value, "'" ),
			context.CancellationToken
		).ConfigureAwait( false );
		return null;
	}

	private static bool TryParseSigned( string? value, out long result ) {
		return long.TryParse(
			value,
			NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign,
			CultureInfo.InvariantCulture,
			out result
		);
	}

	private static bool TryParseBlankJoin( string? value, out long result ) {
		result = 0;
		if (
			!BigInteger.TryParse(
				value,
				NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture,
				out var parsed
			)
			|| parsed < BigInteger.Zero
		) {
			return false;
		}
		if ( BigInteger.Zero == parsed ) {
			result = 1;
		} else if ( long.MaxValue < parsed ) {
			result = long.MaxValue;
		} else {
			result = (long)parsed;
		}
		return true;
	}

	private static bool TryParsePositiveInt( string? value, out int result ) {
		return int.TryParse(
			value,
			NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign,
			CultureInfo.InvariantCulture,
			out result
		) && 0 < result;
	}

	private static bool TryParseFormat( string? value, out NlNumberFormat format ) {
		format = value switch {
			"ln" => NlNumberFormat.Left,
			"rn" => NlNumberFormat.Right,
			"rz" => NlNumberFormat.RightZero,
			_ => default
		};
		return value is "ln" or "rn" or "rz";
	}

	private static bool RequiresStandardInput( IReadOnlyList<string> operands ) {
		return 0 == operands.Count || operands.Any( value => "-" == value );
	}

	private static Task WriteInvalidNumberAsync(
		CommandContext context,
		string description,
		string? value
	) {
		return context.Diagnostics.ErrorAsync(
			string.Concat( "invalid ", description, ": '", value ?? string.Empty, "'" ),
			context.CancellationToken
		).AsTask();
	}

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync(
			OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync(
			"Try 'nl --help' for more information.".AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "Usage: nl [OPTION]... [FILE]...".AsMemory(), cancellationToken );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await WriteUsageAsync( writer, cancellationToken ).ConfigureAwait( false );
		await writer.WriteAsync(
			string.Concat(
				"Write each FILE to standard output, with line numbers added.", Environment.NewLine,
				"With no FILE, or when FILE is -, read standard input.", Environment.NewLine,
				Environment.NewLine,
				"  -b, --body-numbering=STYLE       use STYLE for numbering body lines", Environment.NewLine,
				"  -d, --section-delimiter=CC       use CC for logical-page delimiters", Environment.NewLine,
				"  -f, --footer-numbering=STYLE     use STYLE for numbering footer lines", Environment.NewLine,
				"  -h, --header-numbering=STYLE     use STYLE for numbering header lines", Environment.NewLine,
				"  -i, --line-increment=NUMBER      line number increment at each line", Environment.NewLine,
				"  -l, --join-blank-lines=NUMBER    group NUMBER empty lines as one", Environment.NewLine,
				"  -n, --number-format=FORMAT       insert line numbers according to FORMAT", Environment.NewLine,
				"  -p, --no-renumber                do not reset line numbers for each section", Environment.NewLine,
				"  -s, --number-separator=STRING    add STRING after each line number", Environment.NewLine,
				"  -v, --starting-line-number=NUMBER  first line number in each section", Environment.NewLine,
				"  -w, --number-width=NUMBER        use NUMBER columns for line numbers", Environment.NewLine,
				"      --help                       display this help and exit", Environment.NewLine,
				"      --version                    output version information and exit", Environment.NewLine,
				Environment.NewLine,
				"By default, -v1 -i1 -l1 -sTAB -w6 -nrn are used, and body lines", Environment.NewLine,
				"are numbered while header and footer lines are not.", Environment.NewLine,
				"STYLE is a (all), t (nonempty), n (none), or pBRE (matching GNU BRE).", Environment.NewLine,
				"FORMAT is ln (left), rn (right), or rz (right with leading zeroes).", Environment.NewLine,
				"CC defaults to \\:.  An empty CC disables logical-page delimiters.", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "nl (Icod.CoreUtils) GNU Coreutils 9.11 compatibility profile".AsMemory(), cancellationToken );
	}
}
