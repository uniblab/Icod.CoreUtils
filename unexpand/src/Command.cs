namespace Icod.CoreUtils.Unexpand;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Text;

/// <summary>Implements GNU <c>unexpand</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>unexpand [OPTION]... [FILE]...</code>
/// <para>Eligible blank runs are converted without normalizing untouched input bytes.</para>
/// </remarks>
public static class Command {
	private const string AllKey = "all";
	private const string FirstOnlyKey = "first-only";
	private const string TabsKey = "tabs";
	private const string LegacyTabsKey = "legacy-tabs";
	private const string HelpKey = "help";
	private const string VersionKey = "version";

	/// <summary>Runs <c>unexpand</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run( string[] args, TextReader? standardInput = null, TextWriter? standardOutput = null, TextWriter? standardError = null ) {
		return RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>unexpand</c> asynchronously with optional injected text streams.</summary>
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
			new CommandContext( "unexpand", standardInput, standardOutput, standardError, inputAdapter, null, null, cancellationToken )
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>unexpand</c> asynchronously against a command context.</summary>
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
			var terminal = parsed.Options.FirstOrDefault( option => option.Definition.Key is HelpKey or VersionKey );
			if ( terminal?.Definition.Key == HelpKey ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( terminal?.Definition.Key == VersionKey ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var invalidLegacy = parsed.Options.FirstOrDefault(
				option => option.Definition.Key == LegacyTabsKey
					&& !IsValidLegacyTabSpecification( option.Value )
			);
			if ( invalidLegacy is not null ) {
				await context.Diagnostics.ErrorAsync(
					string.Concat( "tab size contains invalid character(s): '", invalidLegacy.Value, "'" ),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var specifications = parsed.Options
				.Where( option => option.Definition.Key is TabsKey or LegacyTabsKey )
				.Select( option => option.Value ?? string.Empty )
				.ToArray();
			var tabResult = TabStopParser.Parse( specifications );
			if ( !tabResult.IsSuccess ) {
				await context.Diagnostics.ErrorAsync( tabResult.Error!.Message, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var convertAll = parsed.Options.Any( option => option.Definition.Key is AllKey or TabsKey )
				&& !parsed.HasOption( FirstOnlyKey );
			if ( RequiresStandardInput( parsed.Operands ) && context.StandardInputStream is null ) {
				await context.Diagnostics.ErrorAsync( "a binary standard-input stream was not supplied", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var options = new UnexpandOptions( convertAll, tabResult.TabStops!, parsed.Operands );
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			var processor = new UnexpandProcessor(
				options,
				TextLocaleEnvironment.Resolve(),
				UnicodeDisplayWidthProvider.Instance
			);
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
				await context.Diagnostics.ErrorAsync( string.Concat( "write failed: ", exception.Message ), CancellationToken.None ).ConfigureAwait( false );
			} catch { }
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() {
		var settings = new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute };
		settings.TokenRewriteRules.Add(
			new OptionTokenRewriteRule(
				token => IsLegacyTabToken( token )
					? new[] { string.Concat( "--legacy-tabs=", token[1..] ) }
					: null
			)
		);
		return new OptionParser(
			new[] {
				new OptionDefinition( AllKey, 'a', new[] { "all" } ),
				new OptionDefinition( FirstOnlyKey, null, new[] { "first-only" } ),
				new OptionDefinition( TabsKey, 't', new[] { "tabs" }, OptionValueArity.Required ),
				new OptionDefinition( LegacyTabsKey, null, new[] { "legacy-tabs" }, OptionValueArity.Required ),
				new OptionDefinition( HelpKey, null, new[] { "help" } ),
				new OptionDefinition( VersionKey, null, new[] { "version" } )
			},
			settings
		);
	}

	private static bool IsLegacyTabToken( string token ) {
		return 1 < token.Length && '-' == token[0] && char.IsAsciiDigit( token[1] );
	}

	private static bool IsValidLegacyTabSpecification( string? value ) {
		return !string.IsNullOrEmpty( value )
			&& value.All( character => char.IsAsciiDigit( character ) || character == ',' );
	}

	private static bool RequiresStandardInput( IReadOnlyList<string> operands ) {
		return 0 == operands.Count || operands.Any( value => "-" == value );
	}

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync( "Try 'unexpand --help' for more information.".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static Task WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "Usage: unexpand [OPTION]... [FILE]...".AsMemory(), cancellationToken );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await WriteUsageAsync( writer, cancellationToken ).ConfigureAwait( false );
		await writer.WriteAsync(
			string.Concat(
				"Convert blanks in each FILE to tabs, writing to standard output.", Environment.NewLine,
				"With no FILE, or when FILE is -, read standard input.", Environment.NewLine,
				Environment.NewLine,
				"  -a, --all           convert blanks throughout each line", Environment.NewLine,
				"      --first-only    convert only leading blank sequences; overrides -a", Environment.NewLine,
				"  -t, --tabs=LIST     use LIST instead of 8-column stops; enables -a", Environment.NewLine,
				"                      one value repeats; /N is globally aligned;", Environment.NewLine,
				"                      +N repeats relative to the final explicit stop", Environment.NewLine,
				"      --help          display this help and exit", Environment.NewLine,
				"      --version       output version information and exit", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "unexpand (Icod.CoreUtils) GNU Coreutils 9.11 compatibility profile".AsMemory(), cancellationToken );
	}
}
