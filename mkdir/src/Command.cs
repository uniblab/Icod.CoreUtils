// Original behavior/reference: GNU Coreutils 9.11 mkdir.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Mkdir;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using FileCreationMask = Icod.CommandFramework.FileSystem.Modes.FileCreationMask;
using PosixFileMode = Icod.CommandFramework.FileSystem.Modes.PosixFileMode;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Implements GNU <c>mkdir</c> directory creation over the shared single-path mutation provider.
/// </summary>
public static class Command {
	private const int DefaultDirectoryMode = 0x01ff;
	private const int UserWriteAndExecute = 0x00c0;

	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition( "mode", 'm', new[] { "mode" }, OptionValueArity.Required ),
			new OptionDefinition( "parents", 'p', new[] { "parents" } ),
			new OptionDefinition( "verbose", 'v', new[] { "verbose" } ),
			new OptionDefinition( "context-default", 'Z' ),
			new OptionDefinition( "context", longNames: new[] { "context" }, valueArity: OptionValueArity.Optional ),
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs <c>mkdir</c> synchronously against optional caller-owned text streams.</summary>
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
			"mkdir",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>mkdir</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "mkdir" ),
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			SystemFileCreationMaskProvider.Instance
		);
	}

	/// <summary>Runs <c>mkdir</c> asynchronously with injected filesystem and umask providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="mutationProvider">The single-path mutation provider.</param>
	/// <param name="metadataProvider">The authoritative metadata provider.</param>
	/// <param name="creationMaskProvider">The current-process creation-mask provider.</param>
	/// <returns>The command exit status.</returns>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileCreationMaskProvider creationMaskProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		ArgumentNullException.ThrowIfNull( creationMaskProvider );
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
				await context.StandardError.WriteLineAsync( "mkdir: missing operand" ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var modeText = parsed.GetLastValue( "mode" );
			FileModeExpression? modeExpression = null;
			if ( modeText is not null ) {
				var modeResult = FileModeParser.Parse( modeText );
				if ( !modeResult.Succeeded ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( "mkdir: invalid mode ", Quote( modeText ), ": ", modeResult.Message )
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				modeExpression = modeResult.Expression;
			}

			if ( parsed.GetLastValue( "context" ) is not null ) {
				await context.StandardError.WriteLineAsync(
					"mkdir: warning: ignoring --context; security-context labeling is unavailable"
				).ConfigureAwait( false );
			}

			var creationMask = creationMaskProvider.GetCurrentMask();
			var requestedMode = new PosixFileMode( DefaultDirectoryMode );
			var finalMode = modeExpression is null
				? requestedMode
				: modeExpression.Apply( requestedMode, true, creationMask );
			var finalMask = modeExpression is null ? creationMask : FileCreationMask.None;
			var parentMask = new FileCreationMask( creationMask.Value & ~UserWriteAndExecute );
			var parents = parsed.HasOption( "parents" );
			var verbose = parsed.HasOption( "verbose" );
			var exitStatus = CommandExitCodes.Success;

			foreach ( var operand in parsed.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var succeeded = parents
					? await CreateParentChainAsync(
						operand,
						finalMode,
						finalMask,
						parentMask,
						verbose,
						context,
						mutationProvider,
						metadataProvider
					).ConfigureAwait( false )
					: await CreateOneAsync(
						operand,
						finalMode,
						finalMask,
						allowExistingDirectory: false,
						verbose,
						context,
						mutationProvider,
						metadataProvider
					).ConfigureAwait( false );
				if ( !succeeded ) {
					exitStatus = CommandExitCodes.Failure;
				}
			}
			return exitStatus;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes the GNU-compatible <c>mkdir</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: mkdir [OPTION]... DIRECTORY...",
			"Create the DIRECTORY(ies), if they do not already exist.",
			string.Empty,
			"  -m, --mode=MODE   set file mode (as in chmod), not a=rwx - umask",
			"  -p, --parents     no error if existing; make parent directories as needed",
			"  -v, --verbose     print a message for each created directory",
			"  -Z                 set SELinux security context to the default type",
			"      --context[=CTX]  like -Z, or set the specified context",
			"      --help         display this help and exit",
			"      --version      output version information and exit"
		};
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<bool> CreateParentChainAsync(
		string operand,
		PosixFileMode finalMode,
		FileCreationMask finalMask,
		FileCreationMask parentMask,
		bool verbose,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider,
		IFileSystemMetadataProvider metadataProvider
	) {
		IReadOnlyList<string> chain;
		try {
			chain = GetDirectoryChain( operand );
		} catch ( Exception exception ) when ( exception is ArgumentException or NotSupportedException or PathTooLongException ) {
			await WriteFailureAsync( context, operand, exception.Message ).ConfigureAwait( false );
			return false;
		}
		for ( var index = 0; index < chain.Count; index++ ) {
			var isFinal = index == chain.Count - 1;
			if ( !await CreateOneAsync(
				chain[ index ],
				isFinal ? finalMode : new PosixFileMode( DefaultDirectoryMode ),
				isFinal ? finalMask : parentMask,
				allowExistingDirectory: true,
				verbose,
				context,
				mutationProvider,
				metadataProvider
			).ConfigureAwait( false ) ) {
				return false;
			}
		}
		return true;
	}

	private static async ValueTask<bool> CreateOneAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		bool allowExistingDirectory,
		bool verbose,
		CommandContext context,
		IFileSystemMutationProvider mutationProvider,
		IFileSystemMetadataProvider metadataProvider
	) {
		var result = await mutationProvider.CreateDirectoryAsync(
			path,
			mode,
			creationMask,
			FileSystemMutationPrecondition.DestinationMustNotExist(),
			context.CancellationToken
		).ConfigureAwait( false );
		if ( result.Succeeded ) {
			if ( verbose ) {
				await context.StandardOutput.WriteLineAsync(
					string.Concat( "mkdir: created directory ", Quote( path ) )
				).ConfigureAwait( false );
			}
			return true;
		}
		if ( allowExistingDirectory && result.ErrorCode == FileSystemMutationErrorCode.AlreadyExists ) {
			var existing = await TryObserveAsync(
				path,
				PathDereferenceMode.FollowEligiblePathIndirection,
				metadataProvider,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( existing is not null && existing.Kind == FileSystemEntryKind.Directory ) {
				return true;
			}
		}
		await WriteFailureAsync( context, path, DescribeFailure( result ) ).ConfigureAwait( false );
		return false;
	}

	private static IReadOnlyList<string> GetDirectoryChain( string operand ) {
		ArgumentException.ThrowIfNullOrEmpty( operand );
		var fullPath = System.IO.Path.GetFullPath( operand );
		var rooted = System.IO.Path.IsPathRooted( operand );
		var root = System.IO.Path.GetPathRoot( fullPath ) ?? string.Empty;
		var relative = rooted
			? fullPath[ root.Length.. ]
			: System.IO.Path.GetRelativePath( Environment.CurrentDirectory, fullPath );
		var segments = relative.Split(
			new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
			StringSplitOptions.RemoveEmptyEntries
		);
		var chain = new List<string>();
		var current = rooted ? root : string.Empty;
		foreach ( var segment in segments ) {
			current = current.Length == 0 ? segment : System.IO.Path.Combine( current, segment );
			chain.Add( current );
		}
		if ( chain.Count == 0 ) {
			chain.Add( rooted ? root : "." );
		}
		return chain;
	}

	private static async ValueTask<FileSystemMetadata?> TryObserveAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		IFileSystemMetadataProvider metadataProvider,
		CancellationToken cancellationToken
	) {
		try {
			return await metadataProvider.GetMetadataAsync(
				path,
				dereferenceMode,
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
			FileSystemMutationErrorCode.AlreadyExists => "File exists",
			FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory",
			FileSystemMutationErrorCode.WrongObjectKind => "Not a directory",
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
			string.Concat( "mkdir: cannot create directory ", Quote( path ), ": ", message )
		).ConfigureAwait( false );
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync( "Try 'mkdir --help' for more information." )
		);
	}

	private static async ValueTask WriteVersionAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "mkdir (Icod.CoreUtils) 0.1.0" ).ConfigureAwait( false );
	}

	private static string Quote( string value ) {
		return string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
	}
}
