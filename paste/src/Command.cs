namespace Icod.CoreUtils.Paste;

using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Delimiters;
using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.Escapes;
using Icod.CommandFramework.IO;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Implements GNU <c>paste</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>paste [OPTION]... [FILE]...</code>
/// <para>Parallel mode combines corresponding records; serial mode joins every record from one operand before advancing.</para>
/// </remarks>
public static class Command {
	private const string DelimitersKey = "delimiters";
	private const string SerialKey = "serial";
	private const string ZeroKey = "zero-terminated";
	private const string HelpKey = "help";
	private const string VersionKey = "version";

	/// <summary>Runs <c>paste</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run( string[] args, TextReader? standardInput = null, TextWriter? standardOutput = null, TextWriter? standardError = null ) {
		return RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>paste</c> asynchronously with optional injected text streams.</summary>
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
			new CommandContext( "paste", standardInput, standardOutput, standardError, inputAdapter, null, null, cancellationToken )
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>paste</c> asynchronously against a command context.</summary>
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
			var options = await CreateOptionsAsync( parsed, context ).ConfigureAwait( false );
			if ( null == options ) {
				return CommandExitCodes.Failure;
			}
			if ( RequiresStandardInput( options.Operands ) && null == context.StandardInputStream ) {
				await context.Diagnostics.ErrorAsync( "a binary standard-input stream was not supplied", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			var success = await new PasteProcessor( options ).ProcessAsync( context, output ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return success ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( IOException exception ) {
			try { await context.Diagnostics.ErrorAsync( string.Concat( "write failed: ", exception.Message ), CancellationToken.None ).ConfigureAwait( false ); } catch { }
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( DelimitersKey, 'd', new[] { "delimiters" }, OptionValueArity.Required ),
			new OptionDefinition( SerialKey, 's', new[] { "serial" } ),
			new OptionDefinition( ZeroKey, 'z', new[] { "zero-terminated" } ),
			new OptionDefinition( HelpKey, null, new[] { "help" } ),
			new OptionDefinition( VersionKey, null, new[] { "version" } )
		},
		new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute }
	);

	private static async Task<PasteOptions?> CreateOptionsAsync( OptionParseResult parsed, CommandContext context ) {
		var delimiterValue = parsed.Options.LastOrDefault( option => option.Definition.Key == DelimitersKey )?.Value ?? "\t";
		var delimiters = PasteDelimiterParser.Parse( delimiterValue );
		if ( !delimiters.IsSuccess ) {
			var diagnostic = delimiters.Diagnostics.First();
			await context.Diagnostics.ErrorAsync( diagnostic.Message, context.CancellationToken ).ConfigureAwait( false );
			await context.StandardError.WriteLineAsync( "Try 'paste --help' for more information.".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return null;
		}
		var expansion = await PathnameOperandExpander.ExpandAsync(
			parsed.Operands,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		return new PasteOptions(
			parsed.HasOption( SerialKey ),
			parsed.HasOption( ZeroKey ) ? (byte)0 : (byte)'\n',
			parsed.HasOption( ZeroKey ) ? new byte[] { 0 } : Encoding.UTF8.GetBytes( Environment.NewLine ),
			delimiters.Value ?? new SeparatorCycle( new[] { new ByteSeparator( new[] { (byte)'\t' } ) } ),
			expansion.Operands
		);
	}

	private static bool RequiresStandardInput( IReadOnlyList<string> operands ) => 0 == operands.Count || operands.Any( value => "-" == value );

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync( "Try 'paste --help' for more information.".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static Task WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "Usage: paste [OPTION]... [FILE]...".AsMemory(), cancellationToken );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await WriteUsageAsync( writer, cancellationToken ).ConfigureAwait( false );
		await writer.WriteAsync(
			string.Concat(
				"Write lines consisting of the sequentially corresponding lines from each FILE,", Environment.NewLine,
				"separated by TABs, to standard output. With no FILE, or when FILE is -, read", Environment.NewLine,
				"standard input.", Environment.NewLine, Environment.NewLine,
				"  -d, --delimiters=LIST   reuse characters from LIST instead of TABs", Environment.NewLine,
				"  -s, --serial            paste one file at a time instead of in parallel", Environment.NewLine,
				"  -z, --zero-terminated   records end with NUL, not newline", Environment.NewLine,
				"      --help              display this help and exit", Environment.NewLine,
				"      --version           output version information and exit", Environment.NewLine,
				Environment.NewLine,
				"LIST recognizes \\0 as an empty delimiter and \\b, \\f, \\n, \\r, \\t, \\v, and \\\\.", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "paste (Icod.CoreUtils) 1.0".AsMemory(), cancellationToken );
	}
}
