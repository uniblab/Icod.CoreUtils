// Original behavior/reference: GNU Coreutils 9.11 chmod.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chmod;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Implements GNU <c>chmod</c> over the shared metadata, traversal, and mutation contracts.
/// </summary>
public static class Command {
	private const string ModePlaceholder = "__ICOD_CHMOD_MODE_OPERAND__";
	private const int PermissionBits = 0x0fff;
	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition( "changes", 'c', new[] { "changes" } ),
			new OptionDefinition( "quiet", 'f', new[] { "silent", "quiet" } ),
			new OptionDefinition( "verbose", 'v', new[] { "verbose" } ),
			new OptionDefinition( "recursive", 'R', new[] { "recursive" } ),
			new OptionDefinition( "traverse-command-line", 'H' ),
			new OptionDefinition( "traverse-all", 'L' ),
			new OptionDefinition( "traverse-none", 'P' ),
			new OptionDefinition( "no-dereference", 'h', new[] { "no-dereference" } ),
			new OptionDefinition( "dereference", longNames: new[] { "dereference" } ),
			new OptionDefinition( "preserve-root", longNames: new[] { "preserve-root" } ),
			new OptionDefinition( "no-preserve-root", longNames: new[] { "no-preserve-root" } ),
			new OptionDefinition( "reference", longNames: new[] { "reference" }, valueArity: OptionValueArity.Required ),
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs <c>chmod</c> synchronously against optional caller-owned text streams.</summary>
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
			"chmod",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>chmod</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "chmod" ),
			SystemReadOnlyFileSystemProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			SystemFileSystemMutationProvider.Instance,
			SystemFileCreationMaskProvider.Instance
		);
	}

	/// <summary>Runs <c>chmod</c> asynchronously with injected filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="readOnlyProvider">The E1 read-only traversal provider.</param>
	/// <param name="metadataProvider">The E3 authoritative metadata provider.</param>
	/// <param name="mutationProvider">The E4 single-path mutation provider.</param>
	/// <param name="creationMaskProvider">The process creation-mask provider used by omitted-who symbolic modes.</param>
	/// <returns>The command exit status.</returns>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IFileCreationMaskProvider creationMaskProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( readOnlyProvider );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		ArgumentNullException.ThrowIfNull( creationMaskProvider );
		args ??= Array.Empty<string>();
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var prepared = PrepareArguments( args );
			var parsed = Parser.Parse( prepared.Arguments );
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

			var operands = parsed.Operands
				.Select( item => item == ModePlaceholder ? prepared.ModeText! : item )
				.ToArray();
			var referencePath = parsed.GetLastValue( "reference" );
			ModeSource modeSource;
			string[] paths;
			if ( referencePath is null ) {
				if ( operands.Length == 0 ) {
					await context.StandardError.WriteLineAsync( "chmod: missing operand" ).ConfigureAwait( false );
					await WriteTryHelpAsync( context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				if ( operands.Length == 1 ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( "chmod: missing operand after ", Quote( operands[0] ) )
					).ConfigureAwait( false );
					await WriteTryHelpAsync( context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				var modeResult = FileModeParser.Parse( operands[0] );
				if ( !modeResult.Succeeded || modeResult.Expression is null ) {
					var detail = string.IsNullOrEmpty( modeResult.Message ) ? "invalid mode" : modeResult.Message;
					await context.StandardError.WriteLineAsync(
						string.Concat( "chmod: invalid mode: ", Quote( operands[0] ), ": ", detail )
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				modeSource = ModeSource.FromExpression(
					modeResult.Expression,
					creationMaskProvider.GetCurrentMask()
				);
				paths = operands[1..];
			} else {
				if ( operands.Length == 0 ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( "chmod: missing operand after ", Quote( string.Concat( "--reference=", referencePath ) ) )
					).ConfigureAwait( false );
					await WriteTryHelpAsync( context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				var referenceMode = await ReadReferenceModeAsync(
					referencePath,
					metadataProvider,
					context
				).ConfigureAwait( false );
				if ( !referenceMode.Succeeded ) return CommandExitCodes.Failure;
				modeSource = ModeSource.FromReference( referenceMode.Mode );
				paths = operands;
			}

			var reporting = ResolveReportingMode( parsed );
			var quiet = parsed.HasOption( "quiet" );
			var recursive = parsed.HasOption( "recursive" );
			var preserveRoot = recursive && ResolvePreserveRoot( parsed );
			var dereferencePolicy = ResolveDereferencePolicy( parsed );
			var traversalMode = ResolveTraversalMode( parsed );
			if (
				recursive
				&& traversalMode == SymbolicLinkTraversalMode.Never
				&& dereferencePolicy == DereferencePolicy.Dereference
			) {
				await context.StandardError.WriteLineAsync(
					"chmod: -R --dereference requires either -H or -L"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( recursive && traversalMode == SymbolicLinkTraversalMode.Never ) {
				dereferencePolicy = DereferencePolicy.NoDereference;
			} else if (
				recursive
				&& traversalMode == SymbolicLinkTraversalMode.Always
				&& dereferencePolicy == DereferencePolicy.Default
			) {
				dereferencePolicy = DereferencePolicy.Dereference;
			}

			if ( !recursive ) {
				return await ProcessNonRecursiveAsync(
					paths,
					modeSource,
					dereferencePolicy,
					reporting,
					quiet,
					metadataProvider,
					mutationProvider,
					context
				).ConfigureAwait( false );
			}
			return await ProcessRecursiveAsync(
				paths,
				modeSource,
				dereferencePolicy,
				traversalMode,
				preserveRoot,
				reporting,
				quiet,
				readOnlyProvider,
				metadataProvider,
				mutationProvider,
				context
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes GNU-compatible <c>chmod</c> usage text.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: chmod [OPTION]... MODE[,MODE]... FILE...",
			"  or:  chmod [OPTION]... OCTAL-MODE FILE...",
			"  or:  chmod [OPTION]... --reference=RFILE FILE...",
			"Change the mode of each FILE to MODE.",
			string.Empty,
			"  -c, --changes          like verbose but report only when a change is made",
			"  -f, --silent, --quiet  suppress most error messages",
			"  -v, --verbose          output a diagnostic for every file processed",
			"      --dereference      affect the referent of each symbolic link",
			"  -h, --no-dereference   affect symbolic links instead of referenced files",
			"      --no-preserve-root  do not treat '/' specially (the default)",
			"      --preserve-root     fail to operate recursively on '/'",
			"      --reference=RFILE   use RFILE's mode instead of MODE values",
			"  -R, --recursive        change files and directories recursively",
			"  -H                     with -R, traverse command-line directory symlinks",
			"  -L                     with -R, traverse every directory symlink encountered",
			"  -P                     with -R, do not traverse any directory symlinks",
			"      --help              display this help and exit",
			"      --version           output version information and exit"
		};
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<int> ProcessNonRecursiveAsync(
		IEnumerable<string> paths,
		ModeSource modeSource,
		DereferencePolicy dereferencePolicy,
		ReportingMode reporting,
		bool quiet,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		CommandContext context
	) {
		var exitStatus = CommandExitCodes.Success;
		var dereferenceMode = dereferencePolicy == DereferencePolicy.NoDereference
			? PathDereferenceMode.NoFollow
			: PathDereferenceMode.FollowEligiblePathIndirection;
		foreach ( var path in paths ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var outcome = await ChangeModeAsync(
				path,
				path,
				modeSource,
				dereferenceMode,
				precondition: null,
				reporting,
				quiet,
				metadataProvider,
				mutationProvider,
				context
			).ConfigureAwait( false );
			if ( outcome == ModeChangeOutcome.Failed ) exitStatus = CommandExitCodes.Failure;
		}
		return exitStatus;
	}

	private static async ValueTask<int> ProcessRecursiveAsync(
		IReadOnlyList<string> paths,
		ModeSource modeSource,
		DereferencePolicy dereferencePolicy,
		SymbolicLinkTraversalMode traversalMode,
		bool preserveRoot,
		ReportingMode reporting,
		bool quiet,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		CommandContext context
	) {
		var roots = paths.Select( (path, index) => new PathTraversalRoot(
			path,
			index,
			index,
			path,
			path,
			PathTraversalRootKind.Literal
		) ).ToArray();
		var traversal = new RecursiveMutationTraversalEngine( readOnlyProvider );
		var options = new RecursiveMutationOptions {
			PreserveRoot = preserveRoot,
			SymbolicLinkMode = traversalMode,
			ErrorMode = PathTraversalErrorMode.Continue
		};
		var exitStatus = CommandExitCodes.Success;
		var completedDirectories = new HashSet<PathTraversalEntry>();
		await foreach ( var item in traversal.TraverseAsync(
			roots,
			options,
			context.CancellationToken
		).ConfigureAwait( false ) ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			if ( item.Kind == RecursiveMutationEventKind.Error ) {
				exitStatus = CommandExitCodes.Failure;
				if ( !quiet ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( "chmod: cannot access ", Quote( item.Error!.Path ), ": ", item.Error.Message )
					).ConfigureAwait( false );
				}
				continue;
			}
			if ( item.Kind is RecursiveMutationEventKind.Cycle or RecursiveMutationEventKind.FileSystemBoundary ) {
				exitStatus = CommandExitCodes.Failure;
				if ( !quiet ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( "chmod: cannot access ", Quote( item.Entry!.TraversalEntry.DisplayPath ), ": traversal was refused" )
					).ConfigureAwait( false );
				}
				continue;
			}
			if ( item.Kind is not (
				RecursiveMutationEventKind.EnterDirectory
				or RecursiveMutationEventKind.Entry
				or RecursiveMutationEventKind.LeaveDirectory
			) ) {
				continue;
			}
			var entry = item.Entry!;
			var traversalEntry = entry.TraversalEntry;
			if (
				item.Kind == RecursiveMutationEventKind.LeaveDirectory
				&& completedDirectories.Contains( traversalEntry )
			) {
				continue;
			}
			var dereferenceMode = ResolveDereferenceMode( dereferencePolicy, traversalEntry );
			var precondition = entry.Precondition.DereferenceMode == dereferenceMode
				? entry.Precondition
				: null;
			var deferRestrictiveDirectory = item.Kind == RecursiveMutationEventKind.EnterDirectory;
			if (
				item.Kind == RecursiveMutationEventKind.LeaveDirectory
				|| item.Kind == RecursiveMutationEventKind.Entry
				|| deferRestrictiveDirectory
			) {
				var outcome = await ChangeModeAsync(
					traversalEntry.AccessPath,
					traversalEntry.DisplayPath,
					modeSource,
					dereferenceMode,
					precondition,
					reporting,
					quiet,
					metadataProvider,
					mutationProvider,
					context,
					deferRestrictiveDirectory
				).ConfigureAwait( false );
				if ( outcome == ModeChangeOutcome.Failed ) exitStatus = CommandExitCodes.Failure;
				if (
					item.Kind == RecursiveMutationEventKind.EnterDirectory
					&& outcome != ModeChangeOutcome.Deferred
				) {
					completedDirectories.Add( traversalEntry );
				}
			}
		}

		return exitStatus;
	}

	private static async ValueTask<ModeChangeOutcome> ChangeModeAsync(
		string accessPath,
		string displayPath,
		ModeSource modeSource,
		PathDereferenceMode dereferenceMode,
		FileSystemMutationPrecondition? precondition,
		ReportingMode reporting,
		bool quiet,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		CommandContext context,
		bool deferRestrictiveDirectory = false
	) {
		FileSystemMetadata metadata;
		try {
			metadata = await metadataProvider.GetMetadataAsync(
				accessPath,
				dereferenceMode,
				context.CancellationToken
			).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			if ( !quiet ) {
				await context.StandardError.WriteLineAsync(
					string.Concat( "chmod: cannot access ", Quote( displayPath ), ": ", exception.Message )
				).ConfigureAwait( false );
			}
			return ModeChangeOutcome.Failed;
		}
		if (
			dereferenceMode == PathDereferenceMode.NoFollow
			&& metadata.IsPathIndirection
			&& !mutationProvider.Capabilities.CanSetModeWithoutFollowingPathIndirection
		) {
			if ( reporting == ReportingMode.Verbose ) {
				await WriteNotAppliedReportAsync( context.StandardOutput, displayPath ).ConfigureAwait( false );
			}
			return ModeChangeOutcome.Succeeded;
		}
		if ( !metadata.Mode.IsAvailable ) {
			if ( !quiet ) {
				await context.StandardError.WriteLineAsync(
					string.Concat(
						"chmod: changing permissions of ",
						Quote( displayPath ),
						": POSIX mode information is not available on this platform"
					)
				).ConfigureAwait( false );
			}
			return ModeChangeOutcome.Failed;
		}
		var current = new PosixFileMode( checked( (int)(metadata.Mode.GetRequiredValue() & PermissionBits) ) );
		var target = modeSource.Resolve( current, metadata.Kind == FileSystemEntryKind.Directory );
		if (
			deferRestrictiveDirectory
			&& metadata.Kind == FileSystemEntryKind.Directory
			&& (target.Value & current.Value) != current.Value
		) {
			return ModeChangeOutcome.Deferred;
		}
		var changed = current.Value != target.Value;
		if ( !changed ) {
			if ( reporting == ReportingMode.Verbose ) {
				await WriteModeReportAsync( context.StandardOutput, displayPath, current, target, changed: false ).ConfigureAwait( false );
			}
			return ModeChangeOutcome.Succeeded;
		}
		precondition ??= FileSystemMutationPrecondition.FromObservation(
			metadata.Kind,
			metadata.EntryIdentity,
			dereferenceMode
		);
		var result = await mutationProvider.SetModeAsync(
			accessPath,
			target,
			dereferenceMode,
			precondition,
			context.CancellationToken
		).ConfigureAwait( false );
		if (
			!result.Succeeded
			&& result.ErrorCode == FileSystemMutationErrorCode.Unsupported
			&& dereferenceMode == PathDereferenceMode.NoFollow
			&& metadata.IsPathIndirection
		) {
			if ( reporting == ReportingMode.Verbose ) {
				await WriteNotAppliedReportAsync( context.StandardOutput, displayPath ).ConfigureAwait( false );
			}
			return ModeChangeOutcome.Succeeded;
		}
		if ( !result.Succeeded ) {
			if ( !quiet ) {
				await context.StandardError.WriteLineAsync(
					string.Concat( "chmod: changing permissions of ", Quote( displayPath ), ": ", DescribeFailure( result ) )
				).ConfigureAwait( false );
			}
			return ModeChangeOutcome.Failed;
		}
		if ( reporting is ReportingMode.Changes or ReportingMode.Verbose ) {
			await WriteModeReportAsync( context.StandardOutput, displayPath, current, target, changed: true ).ConfigureAwait( false );
		}
		return ModeChangeOutcome.Succeeded;
	}

	private static async ValueTask<ReferenceModeResult> ReadReferenceModeAsync(
		string referencePath,
		IFileSystemMetadataProvider metadataProvider,
		CommandContext context
	) {
		try {
			var metadata = await metadataProvider.GetMetadataAsync(
				referencePath,
				PathDereferenceMode.FollowEligiblePathIndirection,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( !metadata.Mode.IsAvailable ) {
				await context.StandardError.WriteLineAsync(
					string.Concat( "chmod: failed to get attributes of ", Quote( referencePath ), ": POSIX mode information is not available" )
				).ConfigureAwait( false );
				return ReferenceModeResult.Failure();
			}
			return ReferenceModeResult.Success(
				new PosixFileMode( checked( (int)(metadata.Mode.GetRequiredValue() & PermissionBits) ) )
			);
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await context.StandardError.WriteLineAsync(
				string.Concat( "chmod: failed to get attributes of ", Quote( referencePath ), ": ", exception.Message )
			).ConfigureAwait( false );
			return ReferenceModeResult.Failure();
		}
	}

	private static PreparedArguments PrepareArguments( string[] args ) {
		if ( args.Any( item => item == "--reference" || item.StartsWith( "--reference=", StringComparison.Ordinal ) ) ) {
			return new PreparedArguments( args, null );
		}
		var afterDoubleDash = false;
		for ( var index = 0; index < args.Length; index++ ) {
			var item = args[index];
			if ( afterDoubleDash ) return new PreparedArguments( args, null );
			if ( item == "--" ) {
				afterDoubleDash = true;
				continue;
			}
			if ( IsKnownOptionToken( item ) ) continue;
			var mode = FileModeParser.Parse( item );
			if ( !mode.Succeeded ) return new PreparedArguments( args, null );
			if ( item.Length == 0 || item[0] != '-' ) {
				return new PreparedArguments( args, null );
			}
			var normalized = (string[])args.Clone();
			normalized[index] = ModePlaceholder;
			return new PreparedArguments( normalized, item );
		}
		return new PreparedArguments( args, null );
	}

	private static bool IsKnownOptionToken( string value ) {
		if ( value.StartsWith( "--", StringComparison.Ordinal ) ) return true;
		if ( value.Length < 2 || value[0] != '-' ) return false;
		for ( var index = 1; index < value.Length; index++ ) {
			if ( value[index] is not ('c' or 'f' or 'v' or 'R' or 'H' or 'L' or 'P' or 'h') ) return false;
		}
		return true;
	}

	private static ReportingMode ResolveReportingMode( OptionParseResult parsed ) {
		var mode = ReportingMode.None;
		foreach ( var occurrence in parsed.Options ) {
			mode = occurrence.Definition.Key switch {
				"changes" => ReportingMode.Changes,
				"verbose" => ReportingMode.Verbose,
				_ => mode
			};
		}
		return mode;
	}

	private static bool ResolvePreserveRoot( OptionParseResult parsed ) {
		var preserve = false;
		foreach ( var occurrence in parsed.Options ) {
			preserve = occurrence.Definition.Key switch {
				"preserve-root" => true,
				"no-preserve-root" => false,
				_ => preserve
			};
		}
		return preserve;
	}

	private static DereferencePolicy ResolveDereferencePolicy( OptionParseResult parsed ) {
		var policy = DereferencePolicy.Default;
		foreach ( var occurrence in parsed.Options ) {
			policy = occurrence.Definition.Key switch {
				"dereference" => DereferencePolicy.Dereference,
				"no-dereference" => DereferencePolicy.NoDereference,
				_ => policy
			};
		}
		return policy;
	}

	private static SymbolicLinkTraversalMode ResolveTraversalMode( OptionParseResult parsed ) {
		var mode = SymbolicLinkTraversalMode.RootsOnly;
		foreach ( var occurrence in parsed.Options ) {
			mode = occurrence.Definition.Key switch {
				"traverse-command-line" => SymbolicLinkTraversalMode.RootsOnly,
				"traverse-all" => SymbolicLinkTraversalMode.Always,
				"traverse-none" => SymbolicLinkTraversalMode.Never,
				_ => mode
			};
		}
		return mode;
	}

	private static PathDereferenceMode ResolveDereferenceMode(
		DereferencePolicy policy,
		PathTraversalEntry entry
	) {
		return policy switch {
			DereferencePolicy.Dereference => PathDereferenceMode.FollowEligiblePathIndirection,
			DereferencePolicy.NoDereference => PathDereferenceMode.NoFollow,
			_ when entry.IsDescendant => PathDereferenceMode.NoFollow,
			_ => PathDereferenceMode.FollowEligiblePathIndirection
		};
	}

	private static string DescribeFailure( FileSystemMutationResult result ) {
		return result.ErrorCode switch {
			FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory",
			FileSystemMutationErrorCode.AccessDenied => "Permission denied",
			FileSystemMutationErrorCode.PrivilegeRequired => "Operation not permitted",
			FileSystemMutationErrorCode.IdentityChanged => "file changed while it was being processed",
			FileSystemMutationErrorCode.UnsafePathIndirection => "unsafe pathname indirection",
			FileSystemMutationErrorCode.Unsupported => result.Message ?? "Operation not supported",
			_ => result.Message ?? "filesystem operation failed"
		};
	}

	private static bool IsFileSystemException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or System.Security.SecurityException
			or ArgumentException
			or NotSupportedException;
	}

	private static async ValueTask WriteNotAppliedReportAsync( TextWriter output, string path ) {
		await output.WriteLineAsync(
			string.Concat( "neither symbolic link ", Quote( path ), " nor referent has been changed" )
		).ConfigureAwait( false );
	}

	private static async ValueTask WriteModeReportAsync(
		TextWriter output,
		string path,
		PosixFileMode previous,
		PosixFileMode current,
		bool changed
	) {
		var text = changed
			? string.Concat(
				"mode of ", Quote( path ), " changed from ", FormatMode( previous ), " to ", FormatMode( current )
			)
			: string.Concat( "mode of ", Quote( path ), " retained as ", FormatMode( current ) );
		await output.WriteLineAsync( text ).ConfigureAwait( false );
	}

	private static string FormatMode( PosixFileMode mode ) {
		return Convert.ToString( mode.Value, 8 ).PadLeft( 4, '0' );
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync( "Try 'chmod --help' for more information." )
		);
	}

	private static async ValueTask WriteVersionAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync( "chmod (Icod.CoreUtils) 0.1.0" ).ConfigureAwait( false );
	}

	private static string Quote( string value ) {
		return string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
	}

	private enum ReportingMode {
		None = 0,
		Changes = 1,
		Verbose = 2
	}

	private enum DereferencePolicy {
		Default = 0,
		Dereference = 1,
		NoDereference = 2
	}

	private enum ModeChangeOutcome {
		Succeeded = 0,
		Failed = 1,
		Deferred = 2
	}

	private readonly record struct PreparedArguments( string[] Arguments, string? ModeText );

	private readonly record struct ReferenceModeResult( bool Succeeded, PosixFileMode Mode ) {
		/// <summary>Creates a successful reference-mode result.</summary>
		public static ReferenceModeResult Success( PosixFileMode mode ) => new( true, mode );
		/// <summary>Creates a failed reference-mode result.</summary>
		public static ReferenceModeResult Failure() => new( false, default );
	}

	private readonly record struct ModeSource(
		FileModeExpression? Expression,
		PosixFileMode? ReferenceMode,
		FileCreationMask CreationMask
	) {
		/// <summary>Creates a mode source backed by a parsed expression.</summary>
		public static ModeSource FromExpression( FileModeExpression expression, FileCreationMask creationMask ) {
			ArgumentNullException.ThrowIfNull( expression );
			return new ModeSource( expression, null, creationMask );
		}

		/// <summary>Creates a mode source backed by one reference mode.</summary>
		public static ModeSource FromReference( PosixFileMode mode ) => new( null, mode, FileCreationMask.None );

		/// <summary>Resolves the target mode for one observed entry.</summary>
		public PosixFileMode Resolve( PosixFileMode current, bool isDirectory ) {
			return ReferenceMode ?? Expression!.Apply( current, isDirectory, CreationMask );
		}
	}
}
