namespace Icod.CoreUtils.Stat;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Implements GNU-compatible <c>stat</c> metadata and filesystem reporting.</summary>
public static class Command {
	private const string PROGRAM = "stat";
	private const string VERSION = "stat (Icod.CoreUtils) 1.0";

	/// <summary>Executes <c>stat</c> synchronously.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The standard input reader, or <see langword="null"/> for <see cref="Console.In"/>.</param>
	/// <param name="stdout">The standard output writer, or <see langword="null"/> for <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The standard error writer, or <see langword="null"/> for <see cref="Console.Error"/>.</param>
	/// <returns>Zero when every operand is reported; otherwise one.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Executes <c>stat</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The standard input reader, or <see langword="null"/> for <see cref="Console.In"/>.</param>
	/// <param name="stdout">The standard output writer, or <see langword="null"/> for <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The standard error writer, or <see langword="null"/> for <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Zero when every operand is reported; otherwise one.</returns>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? Array.Empty<string>(),
		new CommandContext(
			PROGRAM,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		),
		SystemFileSystemMetadataProvider.Instance
	);

	/// <summary>Executes <c>stat</c> using a complete command context.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <returns>Zero when every operand is reported; otherwise one.</returns>
	public static Task<int> RunAsync( string[] args, CommandContext context ) =>
		RunAsync( args, context, SystemFileSystemMetadataProvider.Instance );

	/// <summary>Executes <c>stat</c> using an injected metadata provider.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <param name="metadataProvider">The authoritative metadata provider.</param>
	/// <returns>Zero when every operand is reported; otherwise one.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMetadataProvider metadataProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		try {
			var result = CreateParser().Parse( args ?? Array.Empty<string>() );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return 1;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return 0;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VERSION.AsMemory(), context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if ( 0 == result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"missing operand", context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}
			var expansion = await PathnameOperandExpander.ExpandAsync(
				result.Operands,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			var operands = expansion.Operands;

			var cached = result.GetLastValue( "cached" );
			if ( null != cached
				&& cached is not "always" and not "never" and not "default" ) {
				await context.Diagnostics.ErrorAsync(
					$"invalid argument '{cached}' for '--cached'",
					context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}
			if ( cached is "always" or "never" ) {
				await context.Diagnostics.ErrorAsync(
					$"attribute-cache mode '{cached}' is unsupported by the current metadata provider",
					context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			var fileSystem = result.HasOption( "file-system" );
			var dereference = result.HasOption( "dereference" );
			string? format = null;
			var interpretEscapes = false;
			var terse = false;
			foreach ( var option in result.Options ) {
				switch ( option.Definition.Key ) {
					case "format":
						format = option.Value;
						interpretEscapes = false;
						terse = false;
						break;
					case "printf":
						format = option.Value;
						interpretEscapes = true;
						terse = false;
						break;
					case "terse":
						format = null;
						interpretEscapes = false;
						terse = true;
						break;
				}
			}

			var exitCode = 0;
			foreach ( var operand in operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				try {
					string text;
					if ( fileSystem ) {
						var information = await metadataProvider.GetFileSystemInformationAsync(
							operand, context.CancellationToken
						).ConfigureAwait( false );
						text = null != format
							? StatFormatEngine.FormatFileSystem( format, operand, information, interpretEscapes )
							: terse
								? StatFormatEngine.FormatFileSystem(
									"%n %i %l %t %s %S %b %f %a %c %d", operand, information, false
								)
								: StatFormatEngine.FormatDefaultFileSystem( operand, information );
					} else {
						var metadata = await metadataProvider.GetMetadataAsync(
							operand, dereference, context.CancellationToken
						).ConfigureAwait( false );
						FileSystemInformation? information = null;
						try {
							information = await metadataProvider.GetFileSystemInformationAsync(
								operand, context.CancellationToken
							).ConfigureAwait( false );
						} catch ( IOException ) {
							// The file report remains useful when containing-filesystem details are unavailable.
						} catch ( UnauthorizedAccessException ) {
							// The file report remains useful when containing-filesystem details are unavailable.
						} catch ( NotSupportedException ) {
							// The file report remains useful when the host cannot expose filesystem details.
						}
						text = null != format
							? StatFormatEngine.FormatFile( format, operand, metadata, information, interpretEscapes )
							: terse
								? StatFormatEngine.FormatFile(
									"%n %s %b %f %u %g %D %i %h %t %T %X %Y %Z %W %o",
									operand,
									metadata,
									information,
									false
								)
								: StatFormatEngine.FormatDefaultFile( operand, metadata );
					}
					await context.StandardOutput.WriteAsync(
						text.AsMemory(), context.CancellationToken
					).ConfigureAwait( false );
					if ( !interpretEscapes ) {
						await context.StandardOutput.WriteLineAsync().ConfigureAwait( false );
					}
				} catch ( FormatException exception ) {
					await context.Diagnostics.ErrorAsync(
						exception.Message, context.CancellationToken
					).ConfigureAwait( false );
					return 1;
				} catch ( Exception exception ) when (
					exception is IOException
					or UnauthorizedAccessException
					or ArgumentException
					or NotSupportedException
				) {
					await context.Diagnostics.ErrorAsync(
						$"cannot stat '{operand}': {exception.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = 1;
				}
			}
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "dereference", 'L', new[] { "dereference" } ),
			new OptionDefinition( "file-system", 'f', new[] { "file-system" } ),
			new OptionDefinition(
				"format", 'c', new[] { "format" }, OptionValueArity.Required
			),
			new OptionDefinition(
				"printf", longNames: new[] { "printf" }, valueArity: OptionValueArity.Required
			),
			new OptionDefinition( "terse", 't', new[] { "terse" } ),
			new OptionDefinition(
				"cached", longNames: new[] { "cached" }, valueArity: OptionValueArity.Required
			),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } ),
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
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

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: stat [OPTION]... FILE...
Display file or file system status.
  -L, --dereference     follow links
  -f, --file-system    display file system status instead of file status
  -c, --format=FORMAT  use FORMAT, appending a newline after each operand
      --printf=FORMAT  like --format, but interpret backslash escapes and do not append a newline
  -t, --terse          print information in terse form
      --cached=MODE    specify attribute-cache behavior: always, never, or default
      --help           display this help and exit
      --version        output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
}
