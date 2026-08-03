// Original behavior/reference: GNU Coreutils 9.11 rm.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Rm;

using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Implements GNU <c>rm</c> over the shared pathname-expansion, traversal, metadata, and mutation contracts.
/// </summary>
public static class Command {
	private static readonly OptionParser Parser = new(
		new[] {
			new OptionDefinition( "force", 'f', new[] { "force" } ),
			new OptionDefinition( "interactive-always", 'i' ),
			new OptionDefinition( "interactive-once", 'I' ),
			new OptionDefinition( "interactive", longNames: new[] { "interactive" }, valueArity: OptionValueArity.Optional ),
			new OptionDefinition( "recursive", 'r', new[] { "recursive" } ),
			new OptionDefinition( "recursive-upper", 'R' ),
			new OptionDefinition( "dir", 'd', new[] { "dir" } ),
			new OptionDefinition( "one-file-system", longNames: new[] { "one-file-system" } ),
			new OptionDefinition( "preserve-root", longNames: new[] { "preserve-root" }, valueArity: OptionValueArity.Optional ),
			new OptionDefinition( "no-preserve-root", longNames: new[] { "no-preserve-root" } ),
			new OptionDefinition( "verbose", 'v', new[] { "verbose" } ),
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs <c>rm</c> synchronously against optional caller-owned text streams.</summary>
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
			"rm",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>rm</c> asynchronously with the system filesystem providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>The command exit status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		var actualContext = context ?? CommandContext.CreateConsole( "rm" );
		var inputIsTerminal = ReferenceEquals( actualContext.StandardInput, Console.In )
			&& !Console.IsInputRedirected;
		return RunAsync(
			args,
			actualContext,
			SystemReadOnlyFileSystemProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			SystemFileSystemMutationProvider.Instance,
			SystemIdentityProvider.Instance,
			inputIsTerminal
		);
	}

	/// <summary>Runs <c>rm</c> asynchronously with injected filesystem and identity providers.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="readOnlyProvider">The E1 pathname-expansion and traversal provider.</param>
	/// <param name="metadataProvider">The E3 authoritative metadata provider.</param>
	/// <param name="mutationProvider">The E4 race-aware single-path mutation provider.</param>
	/// <param name="identityProvider">The process-identity provider used for write-protection decisions.</param>
	/// <param name="standardInputIsTerminal">Whether standard input is an interactive terminal.</param>
	/// <returns>The command exit status.</returns>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IIdentityProvider identityProvider,
		bool standardInputIsTerminal
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( readOnlyProvider );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		ArgumentNullException.ThrowIfNull( identityProvider );
		args ??= Array.Empty<string>();
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var parsed = Parser.Parse( NormalizeOptionalLongArguments( args ) );
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

			var policyResult = ResolvePolicy( parsed );
			if ( policyResult.Error is not null ) {
				await context.StandardError.WriteLineAsync( policyResult.Error ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var policy = policyResult.Policy!;
			if ( parsed.Operands.Count == 0 ) {
				if ( policy.Force ) return CommandExitCodes.Success;
				await context.StandardError.WriteLineAsync( "rm: missing operand" ).ConfigureAwait( false );
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var invalidOperands = new HashSet<int>();
			for ( var index = 0; index < parsed.Operands.Count; index++ ) {
				if ( !IsDotOrDotDotOperand( parsed.Operands[index] ) ) continue;
				invalidOperands.Add( index );
				await context.StandardError.WriteLineAsync( string.Concat(
					"rm: refusing to remove ", Quote( parsed.Operands[index] ),
					": '.' and '..' may not be removed"
				) ).ConfigureAwait( false );
			}
			var eligibleOperands = parsed.Operands
				.Where( ( _, index ) => !invalidOperands.Contains( index ) )
				.ToArray();
			var roots = new List<PathTraversalRoot>();
			var expansionFailed = 0 < invalidOperands.Count;
			var expander = new PathnameExpander( readOnlyProvider );
			await foreach ( var item in expander.ExpandAsync(
				eligibleOperands,
				new PathnameExpansionOptions {
					UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.PreserveAsLiteral,
					SymbolicLinkMode = SymbolicLinkTraversalMode.Never,
					ErrorMode = PathTraversalErrorMode.Continue
				},
				context.CancellationToken
			).ConfigureAwait( false ) ) {
				switch ( item.Kind ) {
					case PathnameExpansionEventKind.Root:
						roots.Add( item.Root! );
						break;
					case PathnameExpansionEventKind.Error:
						expansionFailed = true;
						await WriteCannotRemoveAsync(
							context,
							item.Error!.Path,
							item.Error.Message
						).ConfigureAwait( false );
						break;
					case PathnameExpansionEventKind.Cycle:
					case PathnameExpansionEventKind.FileSystemBoundary:
						expansionFailed = true;
						await WriteCannotRemoveAsync(
							context,
							item.Path ?? item.OriginalOperand,
							item.Kind == PathnameExpansionEventKind.Cycle
								? "pathname expansion encountered a directory cycle"
								: "pathname expansion crossed a filesystem boundary"
						).ConfigureAwait( false );
						break;
				}
			}

			if (
				policy.Interaction == InteractionMode.Once
				&& (policy.Recursive || 3 < roots.Count)
				&& !await PromptAsync(
					context,
					policy.Recursive
						? "rm: remove all arguments recursively? "
						: string.Concat( "rm: remove ", roots.Count.ToString( CultureInfo.InvariantCulture ), " arguments? " )
				).ConfigureAwait( false )
			) {
				return expansionFailed ? CommandExitCodes.Failure : CommandExitCodes.Success;
			}

			ProcessIdentity? processIdentity = null;
			if ( standardInputIsTerminal && policy.Interaction is (InteractionMode.Default or InteractionMode.Once) ) {
				try {
					processIdentity = await identityProvider.GetCurrentAsync( context.CancellationToken ).ConfigureAwait( false );
				} catch ( Exception exception ) when ( exception is NotSupportedException or InvalidOperationException or IOException ) {
					processIdentity = null;
				}
			}

			var failed = expansionFailed;
			foreach ( var root in roots ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var result = policy.Recursive
					? await RemoveRecursivelyAsync(
						root,
						policy,
						context,
						readOnlyProvider,
						metadataProvider,
						mutationProvider,
						processIdentity,
						standardInputIsTerminal
					).ConfigureAwait( false )
					: await RemoveSingleAsync(
						root,
						policy,
						context,
						readOnlyProvider,
						metadataProvider,
						mutationProvider,
						processIdentity,
						standardInputIsTerminal
					).ConfigureAwait( false );
				failed |= !result;
			}
			return failed ? CommandExitCodes.Failure : CommandExitCodes.Success;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	private static async ValueTask<bool> RemoveSingleAsync(
		PathTraversalRoot root,
		RemovalPolicy policy,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		ProcessIdentity? processIdentity,
		bool standardInputIsTerminal
	) {
		var accessPath = HasTrailingDirectorySeparator( root.OriginalOperand )
			? TrimTrailingDirectorySeparatorsPreservingRoot( root.AccessPath )
			: root.AccessPath;
		ReadOnlyFileSystemEntry observation;
		try {
			observation = await readOnlyProvider.ObserveAsync(
				accessPath,
				PathDereferenceMode.NoFollow,
				context.CancellationToken
			).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsMissingException( exception ) ) {
			if ( policy.Force ) return true;
			await WriteCannotRemoveAsync( context, root.DisplayPath, "No such file or directory" ).ConfigureAwait( false );
			return false;
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await WriteCannotRemoveAsync( context, root.DisplayPath, exception.Message ).ConfigureAwait( false );
			return false;
		}
		if ( HasTrailingDirectorySeparator( root.OriginalOperand ) && observation.Kind != FileSystemEntryKind.Directory ) {
			await WriteCannotRemoveAsync( context, root.DisplayPath, "Not a directory" ).ConfigureAwait( false );
			return false;
		}
		if ( observation.Kind == FileSystemEntryKind.Directory && !policy.RemoveEmptyDirectories ) {
			await WriteCannotRemoveAsync( context, root.DisplayPath, "Is a directory" ).ConfigureAwait( false );
			return false;
		}
		if ( !await ConfirmRemovalAsync(
			root.DisplayPath,
			observation.Kind,
			policy,
			context,
			metadataProvider,
			processIdentity,
			standardInputIsTerminal,
			accessPath
		).ConfigureAwait( false ) ) return true;
		var precondition = FileSystemMutationPrecondition.FromObservation(
			observation.Kind,
			observation.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		var mutation = observation.Kind == FileSystemEntryKind.Directory
			? await mutationProvider.RemoveDirectoryAsync(
				accessPath,
				precondition,
				context.CancellationToken
			).ConfigureAwait( false )
			: await mutationProvider.RemoveFileAsync(
				accessPath,
				precondition,
				context.CancellationToken
			).ConfigureAwait( false );
		return await ReportMutationAsync( root.DisplayPath, observation.Kind, mutation, policy, context ).ConfigureAwait( false );
	}

	private static async ValueTask<bool> RemoveRecursivelyAsync(
		PathTraversalRoot root,
		RemovalPolicy policy,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		ProcessIdentity? processIdentity,
		bool standardInputIsTerminal
	) {
		var rootAccessPath = HasTrailingDirectorySeparator( root.OriginalOperand )
			? TrimTrailingDirectorySeparatorsPreservingRoot( root.AccessPath )
			: root.AccessPath;
		try {
			var rootObservation = await readOnlyProvider.ObserveAsync(
				rootAccessPath,
				PathDereferenceMode.NoFollow,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( HasTrailingDirectorySeparator( root.OriginalOperand ) && rootObservation.Kind != FileSystemEntryKind.Directory ) {
				await WriteCannotRemoveAsync( context, root.DisplayPath, "Not a directory" ).ConfigureAwait( false );
				return false;
			}
		} catch ( Exception exception ) when ( IsMissingException( exception ) ) {
			if ( policy.Force ) return true;
			await WriteCannotRemoveAsync( context, root.DisplayPath, "No such file or directory" ).ConfigureAwait( false );
			return false;
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await WriteCannotRemoveAsync( context, root.DisplayPath, exception.Message ).ConfigureAwait( false );
			return false;
		}

		if ( policy.PreserveAllFileSystemRoots && !await ValidatePreserveAllAsync(
			root,
			context,
			readOnlyProvider
		).ConfigureAwait( false ) ) return false;

		var directoryStates = new Stack<DirectoryRemovalState>();
		var engine = new RecursiveMutationTraversalEngine( readOnlyProvider );
		var succeeded = true;
		await foreach ( var item in engine.TraverseAsync(
			new[] { root },
			new RecursiveMutationOptions {
				PreserveRoot = policy.PreserveRoot,
				SymbolicLinkMode = SymbolicLinkTraversalMode.Never,
				FileSystemBoundaryMode = policy.OneFileSystem
					? FileSystemBoundaryMode.StayOnRootFileSystem
					: FileSystemBoundaryMode.CrossFileSystems,
				ErrorMode = PathTraversalErrorMode.Continue
			},
			context.CancellationToken
		).ConfigureAwait( false ) ) {
			if ( item.Kind == RecursiveMutationEventKind.Error ) {
				var error = item.Error!;
				var exception = error.Exception ?? error.TraversalError?.Exception;
				if ( policy.Force && exception is not null && IsMissingException( exception ) ) {
					if ( error.Scope is PathTraversalErrorScope.Root or PathTraversalErrorScope.Traversal ) break;
					continue;
				}
				succeeded = false;
				MarkCurrentDirectoryRetained( directoryStates );
				await WriteCannotRemoveAsync(
					context,
					error.Path,
					DescribeRecursiveError( error )
				).ConfigureAwait( false );
				if (
					error.Code == RecursiveMutationErrorCode.IdentityUnavailable
					|| error.Scope is PathTraversalErrorScope.Root or PathTraversalErrorScope.Traversal
				) break;
				continue;
			}
			if ( item.Entry is null ) continue;
			var entry = item.Entry.TraversalEntry;
			var displayPath = entry.DisplayPath;
			switch ( item.Kind ) {
				case RecursiveMutationEventKind.EnterDirectory:
					var state = new DirectoryRemovalState(
						directoryStates.TryPeek( out var parentState ) && parentState.SkipSubtree
					);
					directoryStates.Push( state );
					if ( state.SkipSubtree ) break;
					if ( policy.Interaction == InteractionMode.Always ) {
						var descend = await PromptAsync(
							context,
							string.Concat( "rm: descend into directory ", Quote( displayPath ), "? " )
						).ConfigureAwait( false );
						if ( !descend ) state.SkipSubtree = true;
					} else if (
						standardInputIsTerminal
						&& policy.Interaction is (InteractionMode.Default or InteractionMode.Once)
						&& await IsWriteProtectedAsync(
							entry.AccessPath,
							metadataProvider,
							processIdentity,
							context.CancellationToken
						).ConfigureAwait( false )
					) {
						var descend = await PromptAsync(
							context,
							string.Concat( "rm: descend into write-protected directory ", Quote( displayPath ), "? " )
						).ConfigureAwait( false );
						if ( !descend ) state.SkipSubtree = true;
					}
					break;
				case RecursiveMutationEventKind.Entry:
					if ( directoryStates.TryPeek( out var currentState ) && currentState.SkipSubtree ) break;
					if ( !await ConfirmRemovalAsync(
						displayPath,
						entry.Kind,
						policy,
						context,
						metadataProvider,
						processIdentity,
						standardInputIsTerminal,
						entry.AccessPath
					).ConfigureAwait( false ) ) {
						MarkCurrentDirectoryRetained( directoryStates );
						break;
					}
					var fileResult = await mutationProvider.RemoveFileAsync(
						entry.AccessPath,
						item.Entry.Precondition,
						context.CancellationToken
					).ConfigureAwait( false );
					var fileSucceeded = await ReportMutationAsync(
						displayPath,
						entry.Kind,
						fileResult,
						policy,
						context
					).ConfigureAwait( false );
					if ( !fileSucceeded ) {
						succeeded = false;
						MarkCurrentDirectoryRetained( directoryStates );
					}
					break;
				case RecursiveMutationEventKind.LeaveDirectory:
					var directoryState = directoryStates.Pop();
					if ( directoryState.SkipSubtree || directoryState.HasRetainedDescendant ) {
						MarkCurrentDirectoryRetained( directoryStates );
						break;
					}
					if ( !await ConfirmRemovalAsync(
						displayPath,
						FileSystemEntryKind.Directory,
						policy,
						context,
						metadataProvider,
						processIdentity,
						standardInputIsTerminal,
						entry.AccessPath
					).ConfigureAwait( false ) ) {
						MarkCurrentDirectoryRetained( directoryStates );
						break;
					}
					var directoryResult = await mutationProvider.RemoveDirectoryAsync(
						entry.AccessPath,
						item.Entry.Precondition,
						context.CancellationToken
					).ConfigureAwait( false );
					var directorySucceeded = await ReportMutationAsync(
						displayPath,
						FileSystemEntryKind.Directory,
						directoryResult,
						policy,
						context
					).ConfigureAwait( false );
					if ( !directorySucceeded ) {
						succeeded = false;
						MarkCurrentDirectoryRetained( directoryStates );
					}
					break;
				case RecursiveMutationEventKind.Cycle:
					succeeded = false;
					MarkCurrentDirectoryRetained( directoryStates );
					await WriteCannotRemoveAsync(
						context,
						displayPath,
						string.Concat( "directory cycle through ", Quote( item.RelatedPath ?? "unknown" ) )
					).ConfigureAwait( false );
					break;
				case RecursiveMutationEventKind.FileSystemBoundary:
					succeeded = false;
					MarkCurrentDirectoryRetained( directoryStates );
					await WriteCannotRemoveAsync(
						context,
						displayPath,
						"skipping directory on a different filesystem"
					).ConfigureAwait( false );
					break;
			}
		}

		return succeeded;
	}

	private static async ValueTask<bool> ConfirmRemovalAsync(
		string displayPath,
		FileSystemEntryKind kind,
		RemovalPolicy policy,
		CommandContext context,
		IFileSystemMetadataProvider metadataProvider,
		ProcessIdentity? processIdentity,
		bool standardInputIsTerminal,
		string? accessPath = null
	) {
		if ( policy.Interaction == InteractionMode.Always ) {
			return await PromptAsync(
				context,
				string.Concat( "rm: remove ", DescribeKind( kind ), " ", Quote( displayPath ), "? " )
			).ConfigureAwait( false );
		}
		if (
			!standardInputIsTerminal
			|| policy.Interaction is InteractionMode.Never
			|| !await IsWriteProtectedAsync(
				accessPath ?? displayPath,
				metadataProvider,
				processIdentity,
				context.CancellationToken
			).ConfigureAwait( false )
		) return true;
		return await PromptAsync(
			context,
			string.Concat( "rm: remove write-protected ", DescribeKind( kind ), " ", Quote( displayPath ), "? " )
		).ConfigureAwait( false );
	}

	private static async ValueTask<bool> IsWriteProtectedAsync(
		string path,
		IFileSystemMetadataProvider metadataProvider,
		ProcessIdentity? identity,
		CancellationToken cancellationToken
	) {
		try {
			var metadata = await metadataProvider.GetMetadataAsync(
				path,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( metadata.IsPathIndirection ) return false;
			if ( metadata.Attributes.IsAvailable ) {
				var attributes = metadata.Attributes.GetRequiredValue();
				if ( (attributes & FileAttributes.ReadOnly) != 0 ) return true;
			}
			if ( identity is null || !metadata.Mode.IsAvailable ) return false;
			if ( !uint.TryParse( identity.EffectiveUser.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var userId ) ) {
				return false;
			}
			if ( userId == 0 ) return false;
			var mode = metadata.Mode.GetRequiredValue();
			if ( metadata.UserId.IsAvailable && metadata.UserId.GetRequiredValue() == userId ) {
				return (mode & 0x0080u) == 0;
			}
			var groupIds = identity.Groups
				.Select( group => group.Id )
				.Append( identity.EffectiveGroup.Id )
				.ToHashSet( StringComparer.Ordinal );
			if (
				metadata.GroupId.IsAvailable
				&& groupIds.Contains( metadata.GroupId.GetRequiredValue().ToString( CultureInfo.InvariantCulture ) )
			) return (mode & 0x0010u) == 0;
			return (mode & 0x0002u) == 0;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) || exception is NotSupportedException ) {
			return false;
		}
	}

	private static async ValueTask<bool> ValidatePreserveAllAsync(
		PathTraversalRoot root,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider
	) {
		try {
			var source = await readOnlyProvider.ObserveAsync(
				root.AccessPath,
				PathDereferenceMode.NoFollow,
				context.CancellationToken
			).ConfigureAwait( false );
			var fullPath = Path.GetFullPath( root.AccessPath );
			var trimmed = Path.TrimEndingDirectorySeparator( fullPath );
			var parentPath = Path.GetDirectoryName( trimmed );
			if ( string.IsNullOrEmpty( parentPath ) ) return true;
			var parent = await readOnlyProvider.ObserveAsync(
				parentPath,
				PathDereferenceMode.NoFollow,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( !source.FileSystemIdentity.IsAvailable || !parent.FileSystemIdentity.IsAvailable ) {
				await WriteCannotRemoveAsync(
					context,
					root.DisplayPath,
					"filesystem identity is unavailable for --preserve-root=all"
				).ConfigureAwait( false );
				return false;
			}
			if ( source.FileSystemIdentity != parent.FileSystemIdentity ) {
				await WriteCannotRemoveAsync(
					context,
					root.DisplayPath,
					"refusing to remove a filesystem root under --preserve-root=all"
				).ConfigureAwait( false );
				return false;
			}
			return true;
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await WriteCannotRemoveAsync( context, root.DisplayPath, exception.Message ).ConfigureAwait( false );
			return false;
		}
	}

	private static async ValueTask<bool> ReportMutationAsync(
		string displayPath,
		FileSystemEntryKind kind,
		FileSystemMutationResult result,
		RemovalPolicy policy,
		CommandContext context
	) {
		if ( result.Succeeded ) {
			if ( policy.Verbose ) {
				await context.StandardOutput.WriteLineAsync( string.Concat(
					"removed ", kind == FileSystemEntryKind.Directory ? "directory " : string.Empty, Quote( displayPath )
				) ).ConfigureAwait( false );
			}
			return true;
		}
		if ( policy.Force && result.ErrorCode is FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound ) {
			return true;
		}
		await WriteCannotRemoveAsync( context, displayPath, DescribeFailure( result ) ).ConfigureAwait( false );
		return false;
	}

	private static RemovalPolicyResult ResolvePolicy( OptionParseResult parsed ) {
		var force = false;
		var interaction = InteractionMode.Default;
		var preserveRoot = true;
		var preserveAll = false;
		foreach ( var occurrence in parsed.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "force":
					force = true;
					interaction = InteractionMode.Never;
					break;
				case "interactive-always":
					force = false;
					interaction = InteractionMode.Always;
					break;
				case "interactive-once":
					force = false;
					interaction = InteractionMode.Once;
					break;
				case "interactive":
					var value = occurrence.Value ?? "always";
					if ( !TryResolveInteractionMode( value, out interaction ) ) {
						return RemovalPolicyResult.Fail( string.Concat(
							"rm: invalid argument ", Quote( value ), " for '--interactive'"
						) );
					}
					if ( interaction is InteractionMode.Once or InteractionMode.Always ) force = false;
					break;
				case "preserve-root":
					preserveRoot = true;
					var requestsAllRoots = string.Equals( occurrence.Value, "all", StringComparison.Ordinal );
					if ( occurrence.Value is not null && !requestsAllRoots ) {
						return RemovalPolicyResult.Fail( string.Concat(
							"rm: invalid argument ", Quote( occurrence.Value ), " for '--preserve-root'"
						) );
					}
					if ( requestsAllRoots ) preserveAll = true;
					break;
				case "no-preserve-root":
					if ( !string.Equals( occurrence.Spelling, "--no-preserve-root", StringComparison.Ordinal ) ) {
						return RemovalPolicyResult.Fail( "rm: you may not abbreviate the --no-preserve-root option" );
					}
					preserveRoot = false;
					break;
			}
		}
		return RemovalPolicyResult.Success( new RemovalPolicy(
			force,
			interaction,
			parsed.HasOption( "recursive" ) || parsed.HasOption( "recursive-upper" ),
			parsed.HasOption( "dir" ),
			parsed.HasOption( "one-file-system" ),
			preserveRoot,
			preserveAll,
			parsed.HasOption( "verbose" )
		) );
	}

	private static bool TryResolveInteractionMode( string value, out InteractionMode mode ) {
		var candidates = new[] {
			(Key: "never", Mode: InteractionMode.Never),
			(Key: "no", Mode: InteractionMode.Never),
			(Key: "none", Mode: InteractionMode.Never),
			(Key: "once", Mode: InteractionMode.Once),
			(Key: "always", Mode: InteractionMode.Always),
			(Key: "yes", Mode: InteractionMode.Always)
		};
		var matches = candidates
			.Where( candidate => candidate.Key.StartsWith( value, StringComparison.Ordinal ) )
			.Select( candidate => candidate.Mode )
			.Distinct()
			.ToArray();
		if ( matches.Length == 1 ) {
			mode = matches[0];
			return true;
		}
		mode = InteractionMode.Invalid;
		return false;
	}

	private static string[] NormalizeOptionalLongArguments( string[] args ) {
		var normalized = (string[])args.Clone();
		for ( var index = 0; index < normalized.Length; index++ ) {
			normalized[index] = normalized[index] switch {
				"--interactive" => "--interactive=always",
				"--preserve-root" => "--preserve-root",
				_ => normalized[index]
			};
		}
		return normalized;
	}

	private static bool IsDotOrDotDotOperand( string operand ) {
		if ( string.IsNullOrEmpty( operand ) ) return false;
		var value = operand;
		while ( 1 < value.Length && IsDirectorySeparator( value[^1] ) ) value = value[..^1];
		var final = Path.GetFileName( value );
		return final is "." or "..";
	}

	private static bool HasTrailingDirectorySeparator( string path ) =>
		0 < path.Length && IsDirectorySeparator( path[^1] );

	private static string TrimTrailingDirectorySeparatorsPreservingRoot( string path ) {
		var rootLength = Path.GetPathRoot( path )?.Length ?? 0;
		var length = path.Length;
		while ( rootLength < length && IsDirectorySeparator( path[length - 1] ) ) length--;
		return length == path.Length ? path : path[..length];
	}

	private static bool IsDirectorySeparator( char value ) =>
		value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;

	private static void MarkCurrentDirectoryRetained( Stack<DirectoryRemovalState> directoryStates ) {
		if ( directoryStates.TryPeek( out var state ) ) state.HasRetainedDescendant = true;
	}


	private static bool IsMissingException( Exception exception ) =>
		exception is FileNotFoundException or DirectoryNotFoundException;

	private static bool IsFileSystemException( Exception exception ) =>
		exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

	private static string DescribeRecursiveError( RecursiveMutationError error ) => error.Code switch {
		RecursiveMutationErrorCode.PreservedRoot => "it is dangerous to operate recursively on a filesystem root",
		RecursiveMutationErrorCode.IdentityUnavailable => "a stable identity is unavailable for race-aware deletion",
		_ => error.Message
	};

	private static string DescribeFailure( FileSystemMutationResult result ) => result.ErrorCode switch {
		FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory",
		FileSystemMutationErrorCode.WrongObjectKind => "Is a directory",
		FileSystemMutationErrorCode.AccessDenied => "Permission denied",
		FileSystemMutationErrorCode.PrivilegeRequired => "Operation not permitted",
		FileSystemMutationErrorCode.IdentityChanged => "file changed while it was being removed",
		FileSystemMutationErrorCode.UnsafePathIndirection => "unsafe pathname indirection",
		FileSystemMutationErrorCode.DirectoryNotEmpty => "Directory not empty",
		FileSystemMutationErrorCode.Unsupported => result.Message ?? "operation is not supported",
		_ => result.Message ?? "input/output error"
	};

	private static string DescribeKind( FileSystemEntryKind kind ) => kind switch {
		FileSystemEntryKind.Directory => "directory",
		FileSystemEntryKind.SymbolicLink or FileSystemEntryKind.NameSurrogate => "link",
		_ => "file"
	};

	private static async ValueTask<bool> PromptAsync( CommandContext context, string prompt ) {
		await context.StandardError.WriteAsync( prompt ).ConfigureAwait( false );
		await context.StandardError.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
		var response = await context.StandardInput.ReadLineAsync( context.CancellationToken ).ConfigureAwait( false );
		return response is not null && response.TrimStart().StartsWith( "y", StringComparison.OrdinalIgnoreCase );
	}

	private static ValueTask WriteCannotRemoveAsync( CommandContext context, string path, string detail ) {
		return new ValueTask( context.StandardError.WriteLineAsync(
			string.Concat( "rm: cannot remove ", Quote( path ), ": ", detail )
		) );
	}

	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask( context.StandardError.WriteLineAsync( "Try 'rm --help' for more information." ) );
	}

	private static async ValueTask WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		cancellationToken.ThrowIfCancellationRequested();
		await writer.WriteLineAsync( "Usage: rm [OPTION]... [FILE]..." ).ConfigureAwait( false );
		await writer.WriteLineAsync( "Remove (unlink) the FILE(s)." ).ConfigureAwait( false );
		await writer.WriteLineAsync().ConfigureAwait( false );
		await writer.WriteLineAsync( "  -f, --force              ignore nonexistent files and arguments, never prompt" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "  -i                       prompt before every removal" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "  -I                       prompt once before removing more than three files, or recursively" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "      --interactive[=WHEN] prompt according to WHEN: never, once, or always" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "  -d, --dir                remove empty directories" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "  -r, -R, --recursive      remove directories and their contents recursively" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "      --one-file-system    stay on the filesystem of each recursive operand" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "      --preserve-root[=all] do not remove '/' or separately mounted hierarchies" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "      --no-preserve-root   do not treat filesystem roots specially" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "  -v, --verbose            explain what is being done" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "      --help               display this help and exit" ).ConfigureAwait( false );
		await writer.WriteLineAsync( "      --version            output version information and exit" ).ConfigureAwait( false );
	}

	private static async ValueTask WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		cancellationToken.ThrowIfCancellationRequested();
		await writer.WriteLineAsync( "rm (Icod.CoreUtils) 1.0" ).ConfigureAwait( false );
	}

	private enum InteractionMode {
		Default = 0,
		Never = 1,
		Once = 2,
		Always = 3,
		Invalid = 4
	}

	private sealed class DirectoryRemovalState {
		/// <summary>Initializes state for one active directory traversal.</summary>
		public DirectoryRemovalState( bool skipSubtree ) {
			SkipSubtree = skipSubtree;
		}

		/// <summary>Gets or sets whether all mutation beneath this directory is skipped.</summary>
		public bool SkipSubtree { get; set; }
		/// <summary>Gets or sets whether a child remains and prevents removal of this directory.</summary>
		public bool HasRetainedDescendant { get; set; }
	}


	private sealed record RemovalPolicy(
		bool Force,
		InteractionMode Interaction,
		bool Recursive,
		bool RemoveEmptyDirectories,
		bool OneFileSystem,
		bool PreserveRoot,
		bool PreserveAllFileSystemRoots,
		bool Verbose
	);

	private sealed record RemovalPolicyResult( RemovalPolicy? Policy, string? Error ) {
		/// <summary>Creates a successful policy result.</summary>
		public static RemovalPolicyResult Success( RemovalPolicy policy ) => new( policy, null );
		/// <summary>Creates a failed policy result.</summary>
		public static RemovalPolicyResult Fail( string error ) => new( null, error );
	}
}
