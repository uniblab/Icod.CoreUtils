// Original behavior/reference: GNU coreutils 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.TSort;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>Implements GNU-compatible topological sorting of byte-token pairs.</summary>
public static class Command {
	private const string VersionText = "tsort (Icod.CoreUtils) 1.0";
	private static readonly byte[] TokenSeparators = [ (byte)' ', (byte)'\t', (byte)'\n' ];

	/// <summary>Runs <c>tsort</c> synchronously with optional injected text streams.</summary>
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

	/// <summary>Runs <c>tsort</c> asynchronously with optional injected text streams.</summary>
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
				"tsort",
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

	/// <summary>Runs <c>tsort</c> asynchronously against a byte-capable command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		ByteOutputStream? output = null;
		ByteOutputStream? error = null;
		var status = CommandExitCodes.Failure;
		try {
			error = new ByteOutputStream( context.StandardError, context.StandardErrorStream );
			output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			status = await RunCoreAsync( args, context, output, error ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			status = CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
			await TryWriteFailureAsync( error, context, exception.Message ).ConfigureAwait( false );
			status = CommandExitCodes.Failure;
		}

		if ( null != output ) {
			var exception = await CompleteAndDisposeAsync( output ).ConfigureAwait( false );
			if ( null != exception ) {
				status = CommandExitCodes.Failure;
				await TryWriteFailureAsync( error, context, exception.Message ).ConfigureAwait( false );
			}
		}
		if ( null != error ) {
			if ( null != await CompleteAndDisposeAsync( error ).ConfigureAwait( false ) ) {
				status = CommandExitCodes.Failure;
			}
		}
		return status;
	}

	private static async Task<int> RunCoreAsync(
		string[] args,
		CommandContext context,
		ByteOutputStream output,
		ByteOutputStream error
	) {
		var parsed = CreateParser().Parse( args );
		if ( !parsed.IsSuccess ) {
			await error.WriteTextAsync(
				string.Concat(
					OptionDiagnosticFormatter.Format( context.ProgramName, parsed.Errors[ 0 ] ),
					Environment.NewLine,
					"Try '", context.ProgramName, " --help' for more information.", Environment.NewLine
				),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
		if ( parsed.HasOption( "help" ) ) {
			await WriteHelpAsync( output, context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		}
		if ( parsed.HasOption( "version" ) ) {
			await output.WriteTextAsync(
				string.Concat( VersionText, Environment.NewLine ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Success;
		}
		if ( 1 < parsed.Operands.Count ) {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				string.Concat( "extra operand ", QuoteOperand( parsed.Operands[ 1 ] ) ),
				context.CancellationToken
			).ConfigureAwait( false );
			await error.WriteTextAsync(
				string.Concat( "Try '", context.ProgramName, " --help' for more information.", Environment.NewLine ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}

		var sourceName = ( 0 == parsed.Operands.Count )
			? "-"
			: parsed.Operands[ 0 ]
		;
		sourceName = await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandSingularAsync(
			sourceName,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		var displaySourceName = FormatSourceName( sourceName );
		InputSource source;
		try {
			source = InputSource.OpenBinary( InputOperand.Create( sourceName ), context );
		} catch ( FileNotFoundException ) {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				string.Concat( displaySourceName, ": No such file or directory" ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( DirectoryNotFoundException ) {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				string.Concat( displaySourceName, ": No such file or directory" ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( UnauthorizedAccessException ) {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				string.Concat( displaySourceName, ": Permission denied" ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		} catch ( IOException exception ) {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				string.Concat( displaySourceName, ": ", exception.Message ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}

		var graph = new TSortGraph();
		try {
			await using ( source ) {
				using var tokens = new ByteTokenReader( source.BinaryStream!, TokenSeparators );
				while ( true ) {
					context.CancellationToken.ThrowIfCancellationRequested();
					var first = await tokens.ReadTokenAsync( context.CancellationToken ).ConfigureAwait( false );
					if ( null == first ) {
						break;
					}
					var second = await tokens.ReadTokenAsync( context.CancellationToken ).ConfigureAwait( false );
					if ( null == second ) {
						await WriteDiagnosticAsync(
							error,
							context.ProgramName,
							string.Concat( displaySourceName, ": input contains an odd number of tokens" ),
							context.CancellationToken
						).ConfigureAwait( false );
						return CommandExitCodes.Failure;
					}
					graph.AddRelation( first, second );
				}
			}
		} catch ( IOException exception ) {
			await WriteDiagnosticAsync(
				error,
				context.ProgramName,
				string.Concat( displaySourceName, ": read error: ", exception.Message ),
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}

		var encounteredLoop = await graph.WriteAsync(
			output,
			error,
			context.ProgramName,
			displaySourceName,
			context.CancellationToken
		).ConfigureAwait( false );
		return encounteredLoop ? CommandExitCodes.Failure : CommandExitCodes.Success;
	}

	private static async Task<Exception?> CompleteAndDisposeAsync( ByteOutputStream stream ) {
		Exception? failure = null;
		try {
			await stream.CompleteAsync( CancellationToken.None ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
			failure = exception;
		}
		try {
			await stream.DisposeAsync().ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsExpectedCommandException( exception ) ) {
			failure ??= exception;
		}
		return failure;
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "ignore-warnings", 'w' ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static bool IsExpectedCommandException( Exception exception ) => exception is
		IOException
		or UnauthorizedAccessException
		or InvalidOperationException
		or ArgumentException
		or NotSupportedException
		or OverflowException;

	private static async Task TryWriteFailureAsync(
		ByteOutputStream? error,
		CommandContext context,
		string message
	) {
		try {
			if ( null != error ) {
				await WriteDiagnosticAsync(
					error,
					context.ProgramName,
					message,
					CancellationToken.None
				).ConfigureAwait( false );
			} else {
				await context.Diagnostics.ErrorAsync(
					message,
					CancellationToken.None
				).ConfigureAwait( false );
			}
		} catch {
			// A diagnostic failure must not replace the conventional failure status.
		}
	}

	private static string FormatSourceName( string sourceName ) {
		if ( "-" == sourceName ) {
			return sourceName;
		}
		foreach ( var character in sourceName ) {
			if (
				!char.IsLetterOrDigit( character )
				&& '/' != character
				&& '\\' != character
				&& '.' != character
				&& '_' != character
				&& '-' != character
				&& ':' != character
			) {
				return QuoteOperand( sourceName );
			}
		}
		return sourceName;
	}

	private static string QuoteOperand( string operand ) => string.Concat(
		"'",
		operand.Replace( "'", "'\\''", StringComparison.Ordinal ),
		"'"
	);

	private static ValueTask WriteDiagnosticAsync(
		ByteOutputStream error,
		string programName,
		string message,
		CancellationToken cancellationToken
	) => error.WriteTextAsync(
		string.Concat( programName, ": ", message, Environment.NewLine ),
		cancellationToken
	);

	private static ValueTask WriteHelpAsync(
		ByteOutputStream output,
		CancellationToken cancellationToken
	) {
		var help = string.Concat(
			"Usage: tsort [OPTION] [FILE]", Environment.NewLine,
			"Write totally ordered list consistent with the partial ordering in FILE.", Environment.NewLine,
			Environment.NewLine,
			"With no FILE, or when FILE is -, read standard input.", Environment.NewLine,
			Environment.NewLine,
			"      --help                 display this help and exit", Environment.NewLine,
			"      --version              output version information and exit", Environment.NewLine
		);
		return output.WriteTextAsync( help, cancellationToken );
	}
}
