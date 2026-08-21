namespace Icod.CoreUtils.Shared.FileSystem.Ownership;

using System.Globalization;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CommandFramework.Platform;

/// <summary>Identifies the ownership-changing command whose policy is being executed.</summary>
public enum OwnershipCommandKind {
	/// <summary>Change a user and/or group with <c>chown</c> syntax.</summary>
	Chown = 0,
	/// <summary>Change a group with <c>chgrp</c> syntax.</summary>
	Chgrp = 1
}

/// <summary>Runs the shared GNU <c>chown</c> and <c>chgrp</c> ownership policy.</summary>
public static class OwnershipCommandRunner {
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
			new OptionDefinition( "from", longNames: new[] { "from" }, valueArity: OptionValueArity.Required ),
			new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
			new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	/// <summary>Runs one ownership command with injected host providers.</summary>
	/// <param name="kind">The command policy.</param>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <param name="readOnlyProvider">The E1 traversal provider.</param>
	/// <param name="metadataProvider">The E3 metadata provider.</param>
	/// <param name="mutationProvider">The E4 mutation provider.</param>
	/// <param name="identityProvider">The user and group identity provider.</param>
	/// <returns>The command exit status.</returns>
	public static async ValueTask<int> RunAsync(
		OwnershipCommandKind kind,
		string[] args,
		CommandContext context,
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IIdentityProvider identityProvider
	) {
		if ( !Enum.IsDefined( typeof( OwnershipCommandKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( readOnlyProvider );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		ArgumentNullException.ThrowIfNull( identityProvider );
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
				await WriteUsageAsync( kind, context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await WriteVersionAsync( kind, context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var referencePath = parsed.GetLastValue( "reference" );
			OwnershipSelection target;
			string[] paths;
			if ( referencePath is null ) {
				if ( parsed.Operands.Count == 0 ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( ProgramName( kind ), ": missing operand" )
					).ConfigureAwait( false );
					await WriteTryHelpAsync( context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				if ( parsed.Operands.Count == 1 ) {
					await context.StandardError.WriteLineAsync(
						string.Concat(
							ProgramName( kind ),
							": missing operand after ",
							Quote( parsed.Operands[0] )
						)
					).ConfigureAwait( false );
					await WriteTryHelpAsync( context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				if ( !mutationProvider.Capabilities.CanSetOwnership ) {
					await WriteUnsupportedOwnershipAsync( kind, context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				var targetResult = kind == OwnershipCommandKind.Chown
					? await OwnershipIdentityResolver.ResolveOwnerSpecAsync(
						parsed.Operands[0],
						identityProvider,
						context.CancellationToken
					).ConfigureAwait( false )
					: await OwnershipIdentityResolver.ResolveGroupAsync(
						parsed.Operands[0],
						identityProvider,
						context.CancellationToken
					).ConfigureAwait( false );
				if ( !targetResult.Succeeded ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( ProgramName( kind ), ": ", targetResult.Message )
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				await WriteResolutionWarningAsync( kind, targetResult, context ).ConfigureAwait( false );
				target = targetResult.Selection!;
				paths = parsed.Operands.Skip( 1 ).ToArray();
			} else {
				if ( parsed.Operands.Count == 0 ) {
					await context.StandardError.WriteLineAsync(
						string.Concat(
							ProgramName( kind ),
							": missing operand after ",
							Quote( string.Concat( "--reference=", referencePath ) )
						)
					).ConfigureAwait( false );
					await WriteTryHelpAsync( context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				if ( !mutationProvider.Capabilities.CanSetOwnership ) {
					await WriteUnsupportedOwnershipAsync( kind, context ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				var reference = await ReadReferenceAsync(
					kind,
					referencePath,
					metadataProvider,
					context
				).ConfigureAwait( false );
				if ( reference is null ) return CommandExitCodes.Failure;
				target = reference;
				paths = parsed.Operands.ToArray();
			}

			OwnershipSelection? filter = null;
			var fromText = parsed.GetLastValue( "from" );
			if ( fromText is not null ) {
				var filterResult = await OwnershipIdentityResolver.ResolveOwnerSpecAsync(
					fromText,
					identityProvider,
					context.CancellationToken
				).ConfigureAwait( false );
				if ( !filterResult.Succeeded ) {
					await context.StandardError.WriteLineAsync(
						string.Concat( ProgramName( kind ), ": ", filterResult.Message )
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				await WriteResolutionWarningAsync( kind, filterResult, context ).ConfigureAwait( false );
				filter = filterResult.Selection;
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
					string.Concat( ProgramName( kind ), ": -R --dereference requires either -H or -L" )
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( recursive && traversalMode == SymbolicLinkTraversalMode.Never ) {
				dereferencePolicy = DereferencePolicy.NoDereference;
			} else if ( recursive && dereferencePolicy == DereferencePolicy.Default ) {
				dereferencePolicy = DereferencePolicy.Dereference;
			}

			return recursive
				? await ProcessRecursiveAsync(
					kind,
					paths,
					target,
					filter,
					dereferencePolicy,
					traversalMode,
					preserveRoot,
					reporting,
					quiet,
					readOnlyProvider,
					metadataProvider,
					mutationProvider,
					context
				).ConfigureAwait( false )
				: await ProcessNonRecursiveAsync(
					kind,
					paths,
					target,
					filter,
					dereferencePolicy,
					reporting,
					quiet,
					metadataProvider,
					mutationProvider,
					context
				).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes usage text for one ownership command.</summary>
	/// <param name="kind">The command policy.</param>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when usage is written.</returns>
	public static async ValueTask WriteUsageAsync(
		OwnershipCommandKind kind,
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = kind == OwnershipCommandKind.Chown
			? new[] {
				"Usage: chown [OPTION]... [OWNER][:[GROUP]] FILE...",
				"  or:  chown [OPTION]... --reference=RFILE FILE...",
				"Change the owner and/or group of each FILE."
			}
			: new[] {
				"Usage: chgrp [OPTION]... GROUP FILE...",
				"  or:  chgrp [OPTION]... --reference=RFILE FILE...",
				"Change the group of each FILE."
			};
		foreach ( var line in lines.Concat( new[] {
			string.Empty,
			"  -c, --changes          like verbose but report only when a change is made",
			"  -f, --silent, --quiet  suppress most error messages",
			"  -v, --verbose          output a diagnostic for every file processed",
			"      --dereference      affect the referent of each symbolic link",
			"  -h, --no-dereference   affect symbolic links instead of referenced files",
			"      --from=CURRENT      change only when current owner/group matches",
			"      --no-preserve-root  do not treat '/' specially (the default)",
			"      --preserve-root     fail to operate recursively on '/'",
			"      --reference=RFILE   use RFILE's owner/group instead of an operand",
			"  -R, --recursive        operate on files and directories recursively",
			"  -H                     with -R, traverse command-line directory symlinks",
			"  -L                     with -R, traverse every directory symlink encountered",
			"  -P                     with -R, do not traverse directory symlinks (default)",
			"      --help              display this help and exit",
			"      --version           output version information and exit"
		} ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( line ).ConfigureAwait( false );
		}
	}

	private static async ValueTask<int> ProcessNonRecursiveAsync(
		OwnershipCommandKind kind,
		IEnumerable<string> paths,
		OwnershipSelection target,
		OwnershipSelection? filter,
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
			var succeeded = await ChangeOwnershipAsync(
				kind,
				path,
				path,
				target,
				filter,
				dereferenceMode,
				precondition: null,
				reporting,
				quiet,
				metadataProvider,
				mutationProvider,
				context
			).ConfigureAwait( false );
			if ( !succeeded ) exitStatus = CommandExitCodes.Failure;
		}
		return exitStatus;
	}

	private static async ValueTask<int> ProcessRecursiveAsync(
		OwnershipCommandKind kind,
		IReadOnlyList<string> paths,
		OwnershipSelection target,
		OwnershipSelection? filter,
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
						string.Concat( ProgramName( kind ), ": cannot access ", Quote( item.Error!.Path ), ": ", item.Error.Message )
					).ConfigureAwait( false );
				}
				continue;
			}
			if ( item.Kind is RecursiveMutationEventKind.Cycle or RecursiveMutationEventKind.FileSystemBoundary ) {
				exitStatus = CommandExitCodes.Failure;
				if ( !quiet ) {
					await context.StandardError.WriteLineAsync(
						string.Concat(
							ProgramName( kind ),
							": cannot access ",
							Quote( item.Entry!.TraversalEntry.DisplayPath ),
							": traversal was refused"
						)
					).ConfigureAwait( false );
				}
				continue;
			}
			if ( item.Kind is not (RecursiveMutationEventKind.LeaveDirectory or RecursiveMutationEventKind.Entry) ) {
				continue;
			}
			var entry = item.Entry!;
			var traversalEntry = entry.TraversalEntry;
			var dereferenceMode = ResolveDereferenceMode( dereferencePolicy );
			var precondition = entry.Precondition.DereferenceMode == dereferenceMode
				? entry.Precondition
				: null;
			var succeeded = await ChangeOwnershipAsync(
				kind,
				traversalEntry.AccessPath,
				traversalEntry.DisplayPath,
				target,
				filter,
				dereferenceMode,
				precondition,
				reporting,
				quiet,
				metadataProvider,
				mutationProvider,
				context
			).ConfigureAwait( false );
			if ( !succeeded ) exitStatus = CommandExitCodes.Failure;
		}
		return exitStatus;
	}

	private static async ValueTask<bool> ChangeOwnershipAsync(
		OwnershipCommandKind kind,
		string accessPath,
		string displayPath,
		OwnershipSelection target,
		OwnershipSelection? filter,
		PathDereferenceMode dereferenceMode,
		FileSystemMutationPrecondition? precondition,
		ReportingMode reporting,
		bool quiet,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		CommandContext context
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
					string.Concat( ProgramName( kind ), ": cannot access ", Quote( displayPath ), ": ", exception.Message )
				).ConfigureAwait( false );
			}
			return false;
		}
		if (
			dereferenceMode == PathDereferenceMode.NoFollow
				&& metadata.IsPathIndirection
				&& !mutationProvider.Capabilities.CanSetOwnershipWithoutFollowingPathIndirection
		) {
			if ( reporting == ReportingMode.Verbose ) {
				await WriteNotAppliedReportAsync( context.StandardOutput, displayPath ).ConfigureAwait( false );
			}
			return true;
		}
		if ( !metadata.UserId.IsAvailable || !metadata.GroupId.IsAvailable ) {
			if ( !quiet ) {
				await context.StandardError.WriteLineAsync(
					string.Concat(
						ProgramName( kind ),
						": changing ",
						kind == OwnershipCommandKind.Chown ? "ownership" : "group",
						" of ",
						Quote( displayPath ),
						": POSIX ownership information is not available on this platform"
					)
				).ConfigureAwait( false );
			}
			return false;
		}
		var currentUserId = metadata.UserId.GetRequiredValue();
		var currentGroupId = metadata.GroupId.GetRequiredValue();
		var currentUser = metadata.OwnerName.IsAvailable
			? metadata.OwnerName.GetRequiredValue()
			: currentUserId.ToString( CultureInfo.InvariantCulture );
		var currentGroup = metadata.GroupName.IsAvailable
			? metadata.GroupName.GetRequiredValue()
			: currentGroupId.ToString( CultureInfo.InvariantCulture );
		if (
			filter is not null
				&& (
					(filter.UserId.HasValue && filter.UserId.Value != currentUserId)
						|| (filter.GroupId.HasValue && filter.GroupId.Value != currentGroupId)
				)
		) {
			if ( reporting == ReportingMode.Verbose ) {
				await WriteOwnershipReportAsync(
					kind,
					context.StandardOutput,
					displayPath,
					currentUser,
					currentGroup,
					currentUser,
					currentGroup,
					changed: false
				).ConfigureAwait( false );
			}
			return true;
		}
		var targetUserId = target.UserId ?? currentUserId;
		var targetGroupId = target.GroupId ?? currentGroupId;
		var targetUser = target.UserDisplay ?? currentUser;
		var targetGroup = target.GroupDisplay ?? currentGroup;
		var changed = targetUserId != currentUserId || targetGroupId != currentGroupId;
		if ( !changed ) {
			if ( reporting == ReportingMode.Verbose ) {
				await WriteOwnershipReportAsync(
					kind,
					context.StandardOutput,
					displayPath,
					currentUser,
					currentGroup,
					targetUser,
					targetGroup,
					changed: false
				).ConfigureAwait( false );
			}
			return true;
		}
		precondition = precondition is null
			? FileSystemMutationPrecondition.FromOwnershipObservation(
				metadata.Kind,
				metadata.EntryIdentity,
				dereferenceMode,
				filter?.UserId,
				filter?.GroupId
			)
			: precondition.WithExpectedOwnership( filter?.UserId, filter?.GroupId );
		var result = await mutationProvider.SetOwnershipAsync(
			accessPath,
			target.UserId,
			target.GroupId,
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
			return true;
		}
		if ( !result.Succeeded ) {
			if ( !quiet ) {
				await context.StandardError.WriteLineAsync(
					string.Concat(
						ProgramName( kind ),
						": changing ",
						kind == OwnershipCommandKind.Chown ? "ownership" : "group",
						" of ",
						Quote( displayPath ),
						": ",
						DescribeFailure( result )
					)
				).ConfigureAwait( false );
			}
			if ( reporting == ReportingMode.Verbose ) {
				await WriteOwnershipFailureReportAsync(
					kind,
					context.StandardOutput,
					displayPath,
					currentUser,
					currentGroup,
					targetUser,
					targetGroup
				).ConfigureAwait( false );
			}
			return false;
		}
		if ( reporting is ReportingMode.Changes or ReportingMode.Verbose ) {
			await WriteOwnershipReportAsync(
				kind,
				context.StandardOutput,
				displayPath,
				currentUser,
				currentGroup,
				targetUser,
				targetGroup,
				changed: true
			).ConfigureAwait( false );
		}
		return true;
	}

	private static async ValueTask<OwnershipSelection?> ReadReferenceAsync(
		OwnershipCommandKind kind,
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
			if ( !metadata.UserId.IsAvailable || !metadata.GroupId.IsAvailable ) {
				await context.StandardError.WriteLineAsync(
					string.Concat(
						ProgramName( kind ),
						": failed to get attributes of ",
						Quote( referencePath ),
						": POSIX ownership information is not available"
					)
				).ConfigureAwait( false );
				return null;
			}
			var userId = metadata.UserId.GetRequiredValue();
			var groupId = metadata.GroupId.GetRequiredValue();
			var userName = metadata.OwnerName.IsAvailable
				? metadata.OwnerName.GetRequiredValue()
				: userId.ToString( CultureInfo.InvariantCulture );
			var groupName = metadata.GroupName.IsAvailable
				? metadata.GroupName.GetRequiredValue()
				: groupId.ToString( CultureInfo.InvariantCulture );
			return kind == OwnershipCommandKind.Chown
				? new OwnershipSelection( userId, groupId, userName, groupName )
				: new OwnershipSelection( null, groupId, null, groupName );
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await context.StandardError.WriteLineAsync(
				string.Concat(
					ProgramName( kind ),
					": failed to get attributes of ",
					Quote( referencePath ),
					": ",
					exception.Message
				)
			).ConfigureAwait( false );
			return null;
		}
	}

	private static async ValueTask WriteResolutionWarningAsync(
		OwnershipCommandKind kind,
		OwnershipResolutionResult result,
		CommandContext context
	) {
		if ( result.Warning is null ) return;
		await context.StandardError.WriteLineAsync(
			string.Concat( ProgramName( kind ), ": ", result.Warning )
		).ConfigureAwait( false );
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
		var mode = SymbolicLinkTraversalMode.Never;
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

	private static PathDereferenceMode ResolveDereferenceMode( DereferencePolicy policy ) {
		return policy switch {
			DereferencePolicy.Dereference => PathDereferenceMode.FollowEligiblePathIndirection,
			DereferencePolicy.NoDereference => PathDereferenceMode.NoFollow,
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

	private static async ValueTask WriteOwnershipFailureReportAsync(
		OwnershipCommandKind kind,
		TextWriter output,
		string path,
		string previousUser,
		string previousGroup,
		string requestedUser,
		string requestedGroup
	) {
		var text = kind == OwnershipCommandKind.Chown
			? string.Concat(
				"failed to change ownership of ",
				Quote( path ),
				" from ",
				previousUser,
				":",
				previousGroup,
				" to ",
				requestedUser,
				":",
				requestedGroup
			)
			: string.Concat(
				"failed to change group of ",
				Quote( path ),
				" from ",
				previousGroup,
				" to ",
				requestedGroup
			);
		await output.WriteLineAsync( text ).ConfigureAwait( false );
	}

	private static async ValueTask WriteOwnershipReportAsync(
		OwnershipCommandKind kind,
		TextWriter output,
		string path,
		string previousUser,
		string previousGroup,
		string currentUser,
		string currentGroup,
		bool changed
	) {
		string text;
		if ( kind == OwnershipCommandKind.Chown ) {
			var previous = string.Concat( previousUser, ":", previousGroup );
			var current = string.Concat( currentUser, ":", currentGroup );
			text = changed
				? string.Concat( "changed ownership of ", Quote( path ), " from ", previous, " to ", current )
				: string.Concat( "ownership of ", Quote( path ), " retained as ", current );
		} else {
			text = changed
				? string.Concat( "changed group of ", Quote( path ), " from ", previousGroup, " to ", currentGroup )
				: string.Concat( "group of ", Quote( path ), " retained as ", currentGroup );
		}
		await output.WriteLineAsync( text ).ConfigureAwait( false );
	}

	private static ValueTask WriteTryHelpAsync( CommandContext context ) {
		return new ValueTask(
			context.StandardError.WriteLineAsync(
				string.Concat( "Try '", context.ProgramName, " --help' for more information." )
			)
		);
	}

	private static async ValueTask WriteUnsupportedOwnershipAsync(
		OwnershipCommandKind kind,
		CommandContext context
	) {
		await context.StandardError.WriteLineAsync(
			string.Concat(
				ProgramName( kind ),
				": POSIX ownership mutation is not supported on this platform"
			)
		).ConfigureAwait( false );
	}

	private static async ValueTask WriteVersionAsync(
		OwnershipCommandKind kind,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await output.WriteLineAsync(
			string.Concat( ProgramName( kind ), " (Icod.CoreUtils) 0.1.0" )
		).ConfigureAwait( false );
	}

	private static string ProgramName( OwnershipCommandKind kind ) {
		return kind == OwnershipCommandKind.Chown ? "chown" : "chgrp";
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
}
