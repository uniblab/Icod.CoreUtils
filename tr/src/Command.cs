// Original behavior/reference: GNU coreutils 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Tr;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Escapes;
using Icod.CoreUtils.Shared.IO;

/// <summary>Implements GNU-compatible byte translation, deletion, and squeezing.</summary>
public static class Command {
	private const string VersionText = "tr (Icod.CoreUtils) 1.0";

	/// <summary>Runs <c>tr</c> synchronously with optional injected text streams.</summary>
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
	) => RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();

	/// <summary>Runs <c>tr</c> asynchronously with optional injected text streams.</summary>
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
				"tr",
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

	/// <summary>Runs <c>tr</c> asynchronously against a byte-capable command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await context.StandardError.WriteLineAsync(
					OptionDiagnosticFormatter.Format( context.ProgramName, parsed.Errors[0] ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( !TryCreateOptions( parsed, out var options, out var error ) ) {
				await context.Diagnostics.ErrorAsync( error!, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var first = TrSetParser.Parse( options!.String1 );
			if ( !await ReportParseResultAsync( first, context ).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}
			TrSetParseResult? second = null;
			if ( null != options.String2 ) {
				second = TrSetParser.Parse( options.String2 );
				if ( !await ReportParseResultAsync( second, context ).ConfigureAwait( false ) ) {
					return CommandExitCodes.Failure;
				}
			}
			return await ExecuteAsync(
				options,
				first.Expression!,
				second?.Expression,
				parsed.HasOption( "aix-bytewise" ),
				context
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or InvalidOperationException
			or ArgumentException
			or NotSupportedException
			or OverflowException
		) {
			try {
				await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the conventional command status.
			}
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "aix-bytewise", 'A' ),
			new OptionDefinition( "complement", 'c', new[] { "complement" } ),
			new OptionDefinition( "complement-C", 'C' ),
			new OptionDefinition( "delete", 'd', new[] { "delete" } ),
			new OptionDefinition( "squeeze-repeats", 's', new[] { "squeeze-repeats" } ),
			new OptionDefinition( "truncate-set1", 't', new[] { "truncate-set1" } ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.RequireOrder
		}
	);

	private static bool TryCreateOptions(
		OptionParseResult parsed,
		out TrOptions? options,
		out string? error
	) {
		options = null;
		error = null;
		var delete = parsed.HasOption( "delete" );
		var squeeze = parsed.HasOption( "squeeze-repeats" );
		var minimum = 1 + ( delete == squeeze ? 1 : 0 );
		var maximum = delete && !squeeze ? 1 : 2;
		if ( parsed.Operands.Count < minimum ) {
			error = 0 == parsed.Operands.Count
				? "missing operand"
				: string.Concat(
					"missing operand after '",
					parsed.Operands[^1],
					"'",
					squeeze ? "; two strings must be given when both deleting and squeezing repeats" : "; two strings must be given when translating"
				);
			return false;
		}
		if ( maximum < parsed.Operands.Count ) {
			error = string.Concat( "extra operand '", parsed.Operands[maximum], "'" );
			return false;
		}
		options = new TrOptions {
			Complement = parsed.HasOption( "complement" ) || parsed.HasOption( "complement-C" ),
			Delete = delete,
			SqueezeRepeats = squeeze,
			TruncateSet1 = parsed.HasOption( "truncate-set1" ),
			String1 = parsed.Operands[0],
			String2 = 2 == parsed.Operands.Count ? parsed.Operands[1] : null
		};
		return true;
	}

	private static async Task<bool> ReportParseResultAsync(
		TrSetParseResult result,
		CommandContext context
	) {
		foreach ( var diagnostic in result.Diagnostics ) {
			if ( EscapeDiagnosticSeverity.Warning == diagnostic.Severity ) {
				await context.Diagnostics.WarningAsync( diagnostic.Message, context.CancellationToken ).ConfigureAwait( false );
			} else {
				await context.Diagnostics.ErrorAsync( diagnostic.Message, context.CancellationToken ).ConfigureAwait( false );
			}
		}
		if ( null != result.Error ) {
			await context.Diagnostics.ErrorAsync( result.Error, context.CancellationToken ).ConfigureAwait( false );
		}
		return result.IsSuccess;
	}

	private static async Task<int> ExecuteAsync(
		TrOptions options,
		TrSetExpression string1,
		TrSetExpression? string2,
		bool forceBytewiseLocale,
		CommandContext context
	) {
		var locale = forceBytewiseLocale ? new TrByteLocale( null ) : TrByteLocale.Resolve();
		var plan = TrTransformPlanBuilder.Build( options, string1, string2, locale );
		TextReaderStream? inputAdapter = null;
		var input = context.StandardInputStream;
		if ( null == input ) {
			inputAdapter = new TextReaderStream( context.StandardInput, leaveOpen: true );
			input = inputAdapter;
		}
		try {
			var selectedInput = input ?? throw new InvalidOperationException( "standard input is unavailable" );
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			await TrEngine.TransformAsync( selectedInput, output, plan, context.CancellationToken ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} finally {
			inputAdapter?.Dispose();
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: tr [OPTION]... STRING1 [STRING2]
Translate, squeeze, and/or delete bytes from standard input,
writing to standard output.

  -c, -C, --complement       use the complement of ARRAY1
  -d, --delete               delete bytes in ARRAY1, do not translate
  -s, --squeeze-repeats      replace each repeated byte listed in the last ARRAY
  -t, --truncate-set1        first truncate ARRAY1 to length of ARRAY2
      --help                 display this help and exit
      --version              output version information and exit

ARRAY syntax includes byte escapes, CHAR1-CHAR2 ranges, [CHAR*REPEAT],
[:class:] character classes, and [=CHAR=] equivalence classes.
Squeezing occurs after translation or deletion.  Every input byte, including
NUL, newline, carriage return, and other delimiter bytes, is transformed.
""";
		await context.StandardOutput.WriteAsync( help.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}
}
