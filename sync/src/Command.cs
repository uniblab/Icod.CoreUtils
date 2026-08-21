using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem;
using Icod.CoreUtils.Shared.IO;
using Icod.CommandFramework.Platform;

namespace Icod.CoreUtils.Sync;

/// <summary>
/// Implements <c>sync [OPTION] [FILE]...</c> using GNU Coreutils 9.11 semantics.
/// </summary>
public static class Command {

	private const string ProgramName = "sync";
	private const string Version = "sync (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>sync</c> synchronously with optional standard-stream substitution.
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
	) => RunAsync(
		args,
		stdin,
		stdout,
		stderr
	).GetAwaiter().GetResult();

	/// <summary>
	/// Executes <c>sync</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
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
		SystemFileSystemOperations.Instance
	);

	/// <summary>
	/// Executes <c>sync</c> asynchronously using a complete shared command context.
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
	) => RunAsync(
		args,
		context,
		SystemFileSystemOperations.Instance
	);

	/// <summary>
	/// Executes <c>sync</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <param name="fileSystem">The shared filesystem capability provider used to perform flush operations.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemOperations fileSystem
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		ArgumentNullException.ThrowIfNull(
			fileSystem
		);
		args ??= [];
		var cancellationToken = context.CancellationToken;
		try {
			var result = CreateParser().Parse(
				args
			);
			if ( await WriteParseErrorsAsync(
				result,
				context
			).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync(
					context
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					Version.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var dataOnly = result.HasOption( "data" );
			var fileSystemOnly = result.HasOption( "file-system" );
			if ( dataOnly && fileSystemOnly ) {
				await context.Diagnostics.ErrorAsync(
					"cannot specify both --data and --file-system",
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( dataOnly && 0 == result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"--data needs at least one argument",
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			if ( 0 == result.Operands.Count ) {
				return await FlushGlobalAsync(
					context,
					fileSystem
				).ConfigureAwait( false );
			}

			if (
				fileSystemOnly
				&& !fileSystem.Capabilities.SupportsFileSystemFlush
			) {
				// GNU Coreutils falls back to one global sync when syncfs is not
				// available at build time. Preserve that behavior on macOS,
				// FreeBSD, and any future provider exposing only global flushing.
				return await FlushGlobalAsync(
					context,
					fileSystem
				).ConfigureAwait( false );
			}

			var paths = PathnameExpander.Expand(
				result.Operands,
				new PathnameExpansionOptions {
					IncludeDirectories = true,
					IncludeFiles = true,
					PreserveUnmatchedPatterns = true,
				}
			);
			var failed = false;
			foreach ( var path in paths ) {
				cancellationToken.ThrowIfCancellationRequested();
				var operation = fileSystemOnly
					? await fileSystem.FlushFileSystemAsync(
						path,
						cancellationToken
					).ConfigureAwait( false )
					: await fileSystem.FlushFileAsync(
						path,
						dataOnly
							? FileFlushMode.DataOnly
							: FileFlushMode.DataAndMetadata,
						cancellationToken
					).ConfigureAwait( false )
				;
				if ( operation.Succeeded ) {
					continue;
				}
				failed = true;
				await WriteOperationFailureAsync(
					path,
					operation,
					context
				).ConfigureAwait( false );
			}
			return failed
				? CommandExitCodes.Failure
				: CommandExitCodes.Success
			;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsOutputException( exception ) ) {
			try {
				await context.Diagnostics.ErrorAsync(
					exception.Message,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception diagnosticException ) when ( IsOutputException( diagnosticException ) ) {
			}
			return CommandExitCodes.Failure;
		}
	}

	private static async Task<int> FlushGlobalAsync(
		CommandContext context,
		IFileSystemOperations fileSystem
	) {
		var operation = await fileSystem.FlushAllFileSystemsAsync(
			context.CancellationToken
		).ConfigureAwait( false );
		if ( operation.Succeeded ) {
			return CommandExitCodes.Success;
		}
		await context.Diagnostics.ErrorAsync(
			operation.Message ?? "cannot synchronize all filesystems",
			context.CancellationToken
		).ConfigureAwait( false );
		return CommandExitCodes.Failure;
	}

	private static async Task WriteOperationFailureAsync(
		string path,
		PlatformOperationResult operation,
		CommandContext context
	) {
		var message = operation.Message;
		if ( String.IsNullOrWhiteSpace( message ) ) {
			message = System.String.Concat(
				"cannot synchronize '",
				path,
				"'"
			);
		} else if ( !message.Contains( path, StringComparison.Ordinal ) ) {
			message = System.String.Concat(
				"cannot synchronize '",
				path,
				"': ",
				message
			);
		}
		await context.Diagnostics.ErrorAsync(
			message,
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			[
				new OptionDefinition( "data", 'd', [ "data" ] ),
				new OptionDefinition( "file-system", 'f', [ "file-system" ] ),
				new OptionDefinition( "help", null, [ "help" ] ),
				new OptionDefinition( "version", null, [ "version" ] ),
			],
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute,
			}
		);
	}

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

	private static async Task WriteHelpAsync(
		CommandContext context
	) {
		const string help = """
Usage: sync [OPTION] [FILE]...
Synchronize cached writes to persistent storage

If one or more files are specified, sync only them,
or their containing file systems.

  -d, --data             sync only file data, no unneeded metadata
  -f, --file-system      sync the file systems that contain the files
      --help             display this help and exit
      --version          output version information and exit

The --data option requires at least one file operand.  The --data and
--file-system options are mutually exclusive.
""";
		await context.StandardOutput.WriteAsync(
			help.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static bool IsOutputException(
		Exception exception
	) => exception is
		IOException
		or ObjectDisposedException
		or NotSupportedException
		or ArgumentException
	;
}
