// Original behavior/reference: GNU Coreutils 9.11 rmdir.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Rmdir;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.Traversal;

/// <summary>
/// Implements GNU <c>rmdir</c> empty-directory removal over the shared single-path mutation provider.
/// </summary>
public static class Command {
	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition(
				"ignore-non-empty",
				longNames: new[] { "ignore-fail-on-non-empty" }
			),
			new OptionDefinition( "parents", 'p', new[] { "parents" } ),
			new OptionDefinition( "verbose", 'v', new[] { "verbose" } ),
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs <c>rmdir</c> synchronously against optional caller-owned text streams.</summary>
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
			"rmdir",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>rmdir</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "rmdir" ),
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance
		);
	}

	/// <summary>Runs <c>rmdir</c> asynchronously with injected filesystem providers.</summary>
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
				await context.StandardError.WriteLineAsync( "rmdir: missing operand" ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var removeParents = parsed.HasOption( "parents" );
			var ignoreNonEmpty = parsed.HasOption( "ignore-non-empty" );
			var verbose = parsed.HasOption( "verbose" );
			var exitStatus = CommandExitCodes.Success;
			foreach ( var operand in parsed.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var current = System.IO.Path.TrimEndingDirectorySeparator( operand );
				if ( current.Length == 0 ) {
					current = operand;
				}
				while ( true ) {
					var result = await RemoveOneAsync(
						current,
						ignoreNonEmpty,
						verbose,
						context,
						mutationProvider,
						metadataProvider
					).ConfigureAwait( false );
					if ( result == RemovalDisposition.Failed ) {
						exitStatus = CommandExitCodes.Failure;
						break;
					}
					if ( result == RemovalDisposition.Ignored || !removeParents ) {
						break;
					}
					var parent = GetParentOperand( current );
					if ( parent is null ) {
						break;
					}
					current = parent;
				}
			}
			return exitStatus;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes the GNU-compatible <c>rmdir</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: rmdir [OPTION]... DIRECTORY...",
			"Remove the DIRECTORY(ies), if they are empty.",
			string.Empty,
			"      --ignore-fail-on-non-empty  ignore failures caused only by a non-empty directory",
			"  -p, --parents                   remove DIRECTORY and its ancestors",
			"  -v, --verbose                   output a diagnostic for every directory processed",
			"      --help                      display this help and exit",
			"      --version                   output version information and exit"
		};
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<RemovalDisposition> RemoveOneAsync(
		string path,
		bool ignoreNonEmpty,
		bool verbose,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider,
		IFileSystemMetadataProvider metadataProvider
	) {
		if ( verbose ) {
			await context.StandardOutput.WriteLineAsync(
				string.Concat( "rmdir: removing directory, ", Quote( path ) )
			).ConfigureAwait( false );
		}
		var metadata = await TryObserveAsync(
			path,
			metadataProvider,
			context.CancellationToken
		).ConfigureAwait( false );
		if ( metadata is null ) {
			await WriteFailureAsync( context, path, "No such file or directory" ).ConfigureAwait( false );
			return RemovalDisposition.Failed;
		}
		if (
			metadata.Kind != FileSystemEntryKind.Directory
				|| metadata.IsPathIndirection
				|| metadata.IsReparsePoint
		) {
			await WriteFailureAsync( context, path, "Not a directory" ).ConfigureAwait( false );
			return RemovalDisposition.Failed;
		}
		var precondition = FileSystemMutationPrecondition.FromObservation(
			metadata.Kind,
			metadata.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		var result = await mutationProvider.RemoveDirectoryAsync(
			path,
			precondition,
			context.CancellationToken
		).ConfigureAwait( false );
		if ( result.Succeeded ) {
			return RemovalDisposition.Removed;
		}
		if ( ignoreNonEmpty && result.ErrorCode == FileSystemMutationErrorCode.DirectoryNotEmpty ) {
			return RemovalDisposition.Ignored;
		}
		await WriteFailureAsync( context, path, DescribeFailure( result ) ).ConfigureAwait( false );
		return RemovalDisposition.Failed;
	}

	private static string? GetParentOperand( string path ) {
		try {
			var trimmed = System.IO.Path.TrimEndingDirectorySeparator( path );
			var parent = System.IO.Path.GetDirectoryName( trimmed );
			if ( string.IsNullOrEmpty( parent ) ) {
				return null;
			}
			var root = System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( trimmed ) );
			if ( root is not null && string.Equals(
				System.IO.Path.TrimEndingDirectorySeparator( System.IO.Path.GetFullPath( parent ) ),
				System.IO.Path.TrimEndingDirectorySeparator( root ),
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
			) ) {
				return null;
			}
			return parent;
		} catch ( Exception exception ) when ( exception is ArgumentException or NotSupportedException or PathTooLongException ) {
			return null;
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
			FileSystemMutationErrorCode.WrongObjectKind => "Not a directory",
			FileSystemMutationErrorCode.DirectoryNotEmpty => "Directory not empty",
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
			string.Concat( "rmdir: failed to remove ", Quote( path ), ": ", message )
		).ConfigureAwait( false );
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync( "Try 'rmdir --help' for more information." )
		);
	}

	private static async ValueTask WriteVersionAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "rmdir (Icod.CoreUtils) 0.1.0" ).ConfigureAwait( false );
	}

	private static string Quote( string value ) {
		return string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
	}

	private enum RemovalDisposition {
		Removed = 0,
		Ignored = 1,
		Failed = 2
	}
}
