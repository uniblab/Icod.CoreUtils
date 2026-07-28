using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Sync;

/// <summary>
/// Implements <c>sync [OPTION] [FILE]...</c> using GNU Coreutils 9.11 semantics.
/// </summary>
public static class Command {

	private const string ProgramName = "sync";
	private const string Version = "sync (Icod.CoreUtils) 1.0";

	/// <summary>Runs the command synchronously.</summary>
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

	/// <summary>Runs the command asynchronously with optional text streams.</summary>
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

	/// <summary>Runs the command with an explicit command context.</summary>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) => RunAsync(
		args,
		context,
		SystemFileSystemOperations.Instance
	);

	/// <summary>Runs the command with injectable filesystem operations.</summary>
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
