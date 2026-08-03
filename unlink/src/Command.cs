// Original behavior/reference: GNU Coreutils 9.11 unlink.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Unlink;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Implements GNU <c>unlink</c> physical name removal over the shared single-path mutation provider.
/// </summary>
public static class Command {
	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs <c>unlink</c> synchronously against optional caller-owned text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		var context = new CommandContext(
			"unlink",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>unlink</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "unlink" ),
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance
		);
	}

	/// <summary>Runs <c>unlink</c> asynchronously with injected filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="mutationProvider">The single-path mutation provider.</param>
	/// <param name="metadataProvider">The authoritative metadata provider.</param>
	/// <returns>The command exit status.</returns>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider,
		IFileSystemMetadataProvider metadataProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		args ??= Array.Empty<string>();
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var parsed = Parser.Parse( args );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors ) {
					await context.StandardError.WriteLineAsync(
						OptionDiagnosticFormatter.Format( context.ProgramName, error )
					).ConfigureAwait( false );
				}
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteUsageAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.Operands.Count == 0 ) {
				await context.StandardError.WriteLineAsync( "unlink: missing operand" ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( parsed.Operands.Count > 1 ) {
				await context.StandardError.WriteLineAsync(
					string.Concat( "unlink: extra operand ", Quote( parsed.Operands[ 1 ] ) )
				).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var path = parsed.Operands[ 0 ];
			var metadata = await TryObserveAsync(
				path,
				metadataProvider,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( metadata is null ) {
				await WriteFailureAsync( context, path, "No such file or directory" ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if (
				metadata.Kind == FileSystemEntryKind.Directory
					&& !metadata.IsPathIndirection
					&& !metadata.IsReparsePoint
			) {
				await WriteFailureAsync( context, path, "Is a directory" ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( metadata.IsVolumeMountPoint ) {
				await WriteFailureAsync(
					context,
					path,
					"The pathname is a mounted volume rather than an unlinkable name"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var precondition = new FileSystemMutationPrecondition(
				FileSystemMutationExistence.MustExist,
				PathDereferenceMode.NoFollow,
				metadata.Kind,
				metadata.EntryIdentity.IsAvailable ? metadata.EntryIdentity : null,
				rejectUncharacterizedIndirection: false
			);
			var result = await mutationProvider.RemoveFileAsync(
				path,
				precondition,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( result.Succeeded ) {
				return CommandExitCodes.Success;
			}
			await WriteFailureAsync( context, path, DescribeFailure( result ) ).ConfigureAwait( false );
			return result.ErrorCode == FileSystemMutationErrorCode.Cancelled
				? CommandExitCodes.Canceled
				: CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes the GNU-compatible <c>unlink</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: unlink FILE",
			"  or:  unlink OPTION",
			"Call the unlink function to remove the specified FILE.",
			string.Empty,
			"      --help     display this help and exit",
			"      --version  output version information and exit"
		};
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<FileSystemMetadata?> TryObserveAsync(
		string path,
		IFileSystemMetadataProvider metadataProvider,
		CancellationToken cancellationToken
	) {
		try {
			return await metadataProvider.GetMetadataAsync(
				path,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( Exception exception ) when (
			exception is FileNotFoundException
				or DirectoryNotFoundException
				or UnauthorizedAccessException
				or IOException
				or NotSupportedException
				or ArgumentException
		) {
			return null;
		}
	}

	private static string DescribeFailure( FileSystemMutationResult result ) {
		return result.ErrorCode switch {
			FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory",
			FileSystemMutationErrorCode.WrongObjectKind => "Is a directory",
			FileSystemMutationErrorCode.AccessDenied => "Permission denied",
			FileSystemMutationErrorCode.PrivilegeRequired => "Operation not permitted",
			_ => result.Message ?? "filesystem operation failed"
		};
	}

	private static async ValueTask WriteFailureAsync(
		CommandContext context,
		string path,
		string message
	) {
		await context.StandardError.WriteLineAsync(
			string.Concat( "unlink: cannot unlink ", Quote( path ), ": ", message )
		).ConfigureAwait( false );
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync( "Try 'unlink --help' for more information." )
		);
	}

	private static async ValueTask WriteVersionAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "unlink (Icod.CoreUtils) 0.1.0" ).ConfigureAwait( false );
	}

	private static string Quote( string value ) {
		return string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
	}
}
