namespace Icod.CoreUtils.MkTemp;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Temporary;

/// <summary>Implements <c>mktemp [OPTION]... [TEMPLATE]</c> with secure temporary-file and directory creation.</summary>
public static class Command {
	private const string DefaultTemplate = "tmp.XXXXXXXXXX";
	private const string VersionText = "mktemp (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>mktemp</c> synchronously with optional standard-stream substitution.
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
				"mktemp",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error
			)
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>mktemp</c> asynchronously using caller-supplied standard streams.
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
				"mktemp",
				TextReader.Null,
				standardOutput,
				standardError,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>
	/// Executes <c>mktemp</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		return RunAsync(
			args,
			context,
			SecureTemporaryObjectCreator.System,
			SystemMkTempEnvironment.Instance
		);
	}

	/// <summary>
	/// Executes <c>mktemp</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <param name="creator">The secure temporary-object creator used for exclusive file or directory creation.</param>
	/// <param name="environment">The provider used to resolve environment variables and the default temporary directory.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		SecureTemporaryObjectCreator creator,
		IMkTempEnvironment environment
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( creator );
		ArgumentNullException.ThrowIfNull( environment );

		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var parseResult = CreateParser().Parse( NormalizeOptionalTmpDir( args ) );
			if ( !parseResult.IsSuccess ) {
				await WriteParseErrorsAsync( parseResult, context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parseResult.HasOption( "help" ) ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parseResult.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( 1 < parseResult.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"too many templates",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var options = BuildOptions( parseResult );
			var templateOperand = 0 == parseResult.Operands.Count
				? DefaultTemplate
				: parseResult.Operands[ 0 ];
			var useTemporaryDirectory = options.UseTemporaryDirectory
				|| ( 0 == parseResult.Operands.Count );

			if (
				options.Traditional
				&& ContainsDirectorySeparator( templateOperand )
			) {
				await context.Diagnostics.ErrorAsync(
					string.Concat( "invalid template, '", templateOperand, "', contains directory separator" ),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if (
				useTemporaryDirectory
				&& System.IO.Path.IsPathRooted( templateOperand )
			) {
				await context.Diagnostics.ErrorAsync(
					string.Concat( "invalid template, '", templateOperand, "'; with --tmpdir, it may not be absolute" ),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			if ( !TemporaryNameTemplate.TryParse(
				templateOperand,
				options.Suffix,
				out var parsedTemplate,
				out var templateError
			) ) {
				await context.Diagnostics.ErrorAsync(
					templateError ?? "invalid template",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			if ( useTemporaryDirectory ) {
				var destination = ResolveTemporaryDirectory( options, environment );
				parsedTemplate = parsedTemplate!.WithDirectory( destination );
			}

			var kind = options.DryRun
				? TemporaryObjectKind.NameOnly
				: options.Directory
					? TemporaryObjectKind.Directory
					: TemporaryObjectKind.File;
			var creation = creator.Create(
				parsedTemplate!,
				kind,
				context.CancellationToken
			);
			if ( !creation.IsSuccess || null == creation.Path ) {
				if ( !options.Quiet ) {
					var objectName = options.Directory
						? "directory"
						: "file";
					await context.Diagnostics.ErrorAsync(
						string.Concat(
							"failed to create ",
							objectName,
							" via template '",
							parsedTemplate!.Pattern,
							"': ",
							creation.ErrorMessage ?? "creation failed"
						),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				return CommandExitCodes.Failure;
			}

			try {
				await context.StandardOutput.WriteLineAsync(
					creation.Path.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				await context.StandardOutput.FlushAsync(
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
				CleanupCreatedObject( creator, creation );
				return CommandExitCodes.Canceled;
			} catch ( Exception exception ) when ( IsExpectedOutputException( exception ) ) {
				CleanupCreatedObject( creator, creation );
				if ( !options.Quiet ) {
					await WriteDiagnosticWithoutCancellationAsync(
						context,
						exception.Message
					).ConfigureAwait( false );
				}
				return CommandExitCodes.Failure;
			}
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsExpectedFileSystemException( exception ) ) {
			await WriteDiagnosticWithoutCancellationAsync( context, exception.Message ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "directory", 'd', new[] { "directory" } ),
				new( "quiet", 'q', new[] { "quiet" } ),
				new( "dry-run", 'u', new[] { "dry-run" } ),
				new( "suffix", null, new[] { "suffix" }, OptionValueArity.Required ),
				new( "tmpdir", 'p', new[] { "tmpdir" }, OptionValueArity.Required ),
				new( "traditional", 't' ),
				new( "help", null, new[] { "help" }, allowMultiple: false ),
				new( "version", 'V', new[] { "version" }, allowMultiple: false )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	private static string[] NormalizeOptionalTmpDir( IReadOnlyList<string> args ) {
		var normalized = new string[ args.Count ];
		var optionsEnded = false;
		for ( var index = 0; args.Count > index; index++ ) {
			var token = args[ index ];
			if ( optionsEnded ) {
				normalized[ index ] = token;
				continue;
			}
			if ( "--" == token ) {
				optionsEnded = true;
				normalized[ index ] = token;
				continue;
			}
			if (
				token.StartsWith( "--", StringComparison.Ordinal )
				&& !token.Contains( '=' )
			) {
				var name = token.AsSpan( 2 );
				if (
					!name.IsEmpty
					&& "tmpdir".AsSpan().StartsWith( name, StringComparison.Ordinal )
				) {
					normalized[ index ] = "--tmpdir=";
					continue;
				}
			}
			normalized[ index ] = token;
		}
		return normalized;
	}

	private static ParsedOptions BuildOptions( OptionParseResult parseResult ) {
		var directory = false;
		var quiet = false;
		var dryRun = false;
		var traditional = false;
		var useTemporaryDirectory = false;
		string? suffix = null;
		string? temporaryDirectory = null;
		foreach ( var occurrence in parseResult.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "directory":
					directory = true;
					break;
				case "quiet":
					quiet = true;
					break;
				case "dry-run":
					dryRun = true;
					break;
				case "suffix":
					suffix = occurrence.Value ?? string.Empty;
					break;
				case "tmpdir":
					useTemporaryDirectory = true;
					temporaryDirectory = occurrence.Value;
					break;
				case "traditional":
					traditional = true;
					useTemporaryDirectory = true;
					break;
			}
		}
		return new ParsedOptions(
			directory,
			quiet,
			dryRun,
			traditional,
			useTemporaryDirectory,
			suffix,
			temporaryDirectory
		);
	}

	private static string ResolveTemporaryDirectory(
		ParsedOptions options,
		IMkTempEnvironment environment
	) {
		var environmentDirectory = environment.GetEnvironmentVariable( "TMPDIR" );
		if ( options.Traditional && !string.IsNullOrEmpty( environmentDirectory ) ) {
			return environmentDirectory;
		}
		if ( !string.IsNullOrEmpty( options.TemporaryDirectory ) ) {
			return options.TemporaryDirectory;
		}
		if ( !string.IsNullOrEmpty( environmentDirectory ) ) {
			return environmentDirectory;
		}
		return environment.GetDefaultTemporaryDirectory();
	}

	private static void CleanupCreatedObject(
		SecureTemporaryObjectCreator creator,
		TemporaryObjectCreationResult creation
	) {
		if (
			TemporaryObjectKind.NameOnly == creation.Kind
			|| null == creation.Path
		) {
			return;
		}
		_ = creator.TryDelete(
			creation.Path,
			creation.Kind,
			out _
		);
	}

	private static bool ContainsDirectorySeparator( string value ) {
		return ( 0 <= value.IndexOf( System.IO.Path.DirectorySeparatorChar ) )
			|| ( 0 <= value.IndexOf( System.IO.Path.AltDirectorySeparatorChar ) );
	}

	private static bool IsExpectedOutputException( Exception exception ) {
		return exception is IOException
			or InvalidOperationException;
	}

	private static bool IsExpectedFileSystemException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException
			or OverflowException
			or System.Security.Cryptography.CryptographicException;
	}

	private static async Task WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		foreach ( var parseError in result.Errors ) {
			var message = parseError.Kind switch {
				OptionParseErrorKind.MissingOptionValue => string.Concat( "option requires an argument -- '", parseError.OptionName, "'" ),
				OptionParseErrorKind.UnexpectedOptionValue => string.Concat( "option does not allow an argument -- '", parseError.OptionName, "'" ),
				OptionParseErrorKind.AmbiguousLongOption => string.Concat( "option '", parseError.OptionName, "' is ambiguous" ),
				_ => string.Concat( "unrecognized option '", parseError.Token, "'" )
			};
			await context.Diagnostics.ErrorAsync(
				message,
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static Task WriteHelpAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		var lines = new[] {
			"Usage: mktemp [OPTION]... [TEMPLATE]",
			"Create a temporary file or directory, safely, and print its name.",
			string.Empty,
			"TEMPLATE must contain at least 3 consecutive 'X's in its last component.",
			"If TEMPLATE is not specified, use tmp.XXXXXXXXXX, and --tmpdir is implied.",
			string.Empty,
			"  -d, --directory     create a directory, not a file",
			"  -u, --dry-run       do not create anything; merely print a name (unsafe)",
			"  -q, --quiet         suppress diagnostics about creation failure",
			"      --suffix=SUFF   append SUFF to TEMPLATE; SUFF must not contain a separator",
			"  -p DIR, --tmpdir[=DIR] interpret TEMPLATE relative to DIR; without DIR, use TMPDIR or the platform default",
			"  -t                  interpret TEMPLATE as one component beneath TMPDIR, -p, or the platform default",
			"      --help          display this help and exit",
			"      --version       output version information and exit",
			string.Empty,
			"Windows, Ubuntu, and macOS are tested. FreeBSD support is best effort."
		};
		return output.WriteAsync(
			string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine ).AsMemory(),
			cancellationToken
		);
	}

	private static async Task WriteDiagnosticWithoutCancellationAsync(
		CommandContext context,
		string message
	) {
		try {
			await context.Diagnostics.ErrorAsync(
				message,
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsExpectedOutputException( exception ) ) {
			// The original operation has already failed and diagnostics cannot be written.
		}
	}

	private sealed record ParsedOptions(
		bool Directory,
		bool Quiet,
		bool DryRun,
		bool Traditional,
		bool UseTemporaryDirectory,
		string? Suffix,
		string? TemporaryDirectory
	);
}
