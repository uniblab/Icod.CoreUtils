namespace Icod.CoreUtils.Fmt;

using System.Globalization;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>Implements GNU <c>fmt</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>fmt [-WIDTH] [OPTION]... [FILE]...</code>
/// <para>Paragraphs are reformatted without normalizing the bytes of retained words.</para>
/// </remarks>
public static class Command {
	private const string CrownKey = "crown-margin";
	private const string GoalKey = "goal";
	private const string HelpKey = "help";
	private const string LegacyWidthKey = "legacy-width";
	private const string PrefixKey = "prefix";
	private const string SplitKey = "split-only";
	private const string TaggedKey = "tagged-paragraph";
	private const string UniformKey = "uniform-spacing";
	private const string VersionKey = "version";
	private const string WidthKey = "width";
	private const int DefaultMaximumWidth = 75;
	private const int MaximumWidth = 2500;

	/// <summary>Runs <c>fmt</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>fmt</c> asynchronously with optional injected text streams.</summary>
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
				"fmt",
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

	/// <summary>Runs <c>fmt</c> asynchronously against a command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( RewriteLegacyWidth( args ) );
			if ( !parsed.IsSuccess ) {
				var error = parsed.Errors[0];
				if (
					error.Kind == OptionParseErrorKind.UnknownShortOption
					&& 1 == error.OptionName.Length
					&& char.IsAsciiDigit( error.OptionName[0] )
				) {
					await WriteMisplacedLegacyWidthAsync( error.OptionName[0], context ).ConfigureAwait( false );
				} else {
					await WriteOptionErrorAsync( error, context ).ConfigureAwait( false );
				}
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

			var options = await CreateOptionsAsync( parsed, context ).ConfigureAwait( false );
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
			var processor = new FmtProcessor( options );
			var success = await processor.ProcessAsync( context, output ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return success ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( OverflowException ) {
			try {
				await context.Diagnostics.ErrorAsync( "input line is too long", CancellationToken.None ).ConfigureAwait( false );
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
				new OptionDefinition( CrownKey, 'c', new[] { "crown-margin" } ),
				new OptionDefinition( PrefixKey, 'p', new[] { "prefix" }, OptionValueArity.Required ),
				new OptionDefinition( SplitKey, 's', new[] { "split-only" } ),
				new OptionDefinition( TaggedKey, 't', new[] { "tagged-paragraph" } ),
				new OptionDefinition( UniformKey, 'u', new[] { "uniform-spacing" } ),
				new OptionDefinition( WidthKey, 'w', new[] { "width" }, OptionValueArity.Required ),
				new OptionDefinition( GoalKey, 'g', new[] { "goal" }, OptionValueArity.Required ),
				new OptionDefinition( LegacyWidthKey, null, new[] { "legacy-width" }, OptionValueArity.Required ),
				new OptionDefinition( HelpKey, null, new[] { "help" } ),
				new OptionDefinition( VersionKey, null, new[] { "version" } )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static async Task<FmtOptions?> CreateOptionsAsync(
		OptionParseResult parsed,
		CommandContext context
	) {
		var maximumWidth = DefaultMaximumWidth;
		var goalWidth = 0;
		var widthSpecified = false;
		var goalSpecified = false;
		var prefix = FmtPrefix.None;
		foreach ( var option in parsed.Options ) {
			switch ( option.Definition.Key ) {
				case WidthKey:
				case LegacyWidthKey:
					if ( !TryParseWidth( option.Value, out maximumWidth ) ) {
						await WriteInvalidWidthAsync( context, option.Value ).ConfigureAwait( false );
						return null;
					}
					widthSpecified = true;
					break;
				case GoalKey:
					if ( !TryParseWidth( option.Value, out goalWidth ) ) {
						await WriteInvalidWidthAsync( context, option.Value ).ConfigureAwait( false );
						return null;
					}
					goalSpecified = true;
					break;
				case PrefixKey:
					prefix = FmtPrefix.Parse( option.Value ?? string.Empty );
					break;
			}
		}
		var goalLimit = widthSpecified ? maximumWidth : DefaultMaximumWidth;
		if ( goalSpecified && goalLimit < goalWidth ) {
			await WriteInvalidWidthAsync( context, goalWidth.ToString( CultureInfo.InvariantCulture ) ).ConfigureAwait( false );
			return null;
		}
		if ( goalSpecified && !widthSpecified ) {
			maximumWidth = checked(goalWidth + 10);
		}
		if ( !goalSpecified ) {
			goalWidth = maximumWidth * 187 / 200;
		}
		return new FmtOptions(
			parsed.HasOption( CrownKey ),
			parsed.HasOption( TaggedKey ),
			parsed.HasOption( SplitKey ),
			parsed.HasOption( UniformKey ),
			maximumWidth,
			goalWidth,
			prefix,
			parsed.Operands
		);
	}

	private static string[] RewriteLegacyWidth( string[] args ) {
		if ( 0 == args.Length || !StartsLegacyWidth( args[0] ) ) {
			return args;
		}
		var rewritten = (string[])args.Clone();
		rewritten[0] = string.Concat( "--legacy-width=", args[0][1..] );
		return rewritten;
	}

	private static bool StartsLegacyWidth( string value ) {
		return 1 < value.Length
			&& '-' == value[0]
			&& char.IsAsciiDigit( value[1] );
	}

	private static bool TryParseWidth( string? value, out int width ) {
		return int.TryParse(
			value,
			NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign,
			CultureInfo.InvariantCulture,
			out width
		)
			&& 0 <= width
			&& width <= MaximumWidth;
	}

	private static bool RequiresStandardInput( IReadOnlyList<string> operands ) {
		return 0 == operands.Count || operands.Any( value => "-" == value );
	}

	private static async Task WriteInvalidWidthAsync( CommandContext context, string? value ) {
		await context.Diagnostics.ErrorAsync(
			string.Concat( "invalid width: '", value ?? string.Empty, "'" ),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteMisplacedLegacyWidthAsync(
		char option,
		CommandContext context
	) {
		await context.StandardError.WriteAsync(
			string.Concat(
				context.ProgramName,
				": invalid option -- ",
				option,
				"; -WIDTH is recognized only when it is the first",
				Environment.NewLine,
				"option; use -w N instead",
				Environment.NewLine,
				"Try 'fmt --help' for more information.",
				Environment.NewLine
			).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync(
			OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync(
			"Try 'fmt --help' for more information.".AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "Usage: fmt [-WIDTH] [OPTION]... [FILE]...".AsMemory(), cancellationToken );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await WriteUsageAsync( writer, cancellationToken ).ConfigureAwait( false );
		await writer.WriteAsync(
			string.Concat(
				"Reformat each paragraph in the FILE(s), writing to standard output.", Environment.NewLine,
				"The option -WIDTH is an abbreviated form of --width=DIGITS.", Environment.NewLine,
				"With no FILE, or when FILE is -, read standard input.", Environment.NewLine,
				Environment.NewLine,
				"  -c, --crown-margin", Environment.NewLine,
				"         preserve indentation of first two lines", Environment.NewLine,
				"  -p, --prefix=STRING", Environment.NewLine,
				"         reformat only lines beginning with STRING,", Environment.NewLine,
				"         reattaching the prefix to reformatted lines", Environment.NewLine,
				"  -s, --split-only", Environment.NewLine,
				"         split long lines, but do not refill", Environment.NewLine,
				"  -t, --tagged-paragraph", Environment.NewLine,
				"         indentation of first line different from second", Environment.NewLine,
				"  -u, --uniform-spacing", Environment.NewLine,
				"         one space between words, two after sentences", Environment.NewLine,
				"  -w, --width=WIDTH", Environment.NewLine,
				"         maximum line width (default of 75 columns)", Environment.NewLine,
				"  -g, --goal=WIDTH", Environment.NewLine,
				"         goal width (default of 93% of width)", Environment.NewLine,
				"      --help                display this help and exit", Environment.NewLine,
				"      --version             output version information and exit", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "fmt (Icod.CoreUtils) GNU Coreutils 9.11 compatibility profile".AsMemory(), cancellationToken );
	}
}
