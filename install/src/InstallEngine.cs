namespace Icod.CoreUtils.Install;

using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Ownership;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Executes GNU <c>install</c> plans through E3, E4, and E6 shared primitives.</summary>
internal sealed class InstallEngine {
	private const int CopyBufferSize = 128 * 1024;
	private static readonly PosixFileMode ParentDirectoryMode = new( 0x01ed ); // 0755
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly IFileSystemMutationProvider mutationProvider;
	private readonly IIdentityProvider identityProvider;
	private readonly ITransactionalReplacementFileSystem transactionFileSystem;
	private readonly IInstallSecurityContextProvider securityContextProvider;
	private readonly TextWriter output;
	private readonly TextWriter error;

	/// <summary>Initializes an installation engine over injectable shared providers.</summary>
	public InstallEngine(
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IIdentityProvider identityProvider,
		ITransactionalReplacementFileSystem transactionFileSystem,
		IInstallSecurityContextProvider securityContextProvider,
		TextWriter output,
		TextWriter error
	) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.mutationProvider = mutationProvider ?? throw new ArgumentNullException( nameof( mutationProvider ) );
		this.identityProvider = identityProvider ?? throw new ArgumentNullException( nameof( identityProvider ) );
		this.transactionFileSystem = transactionFileSystem ?? throw new ArgumentNullException( nameof( transactionFileSystem ) );
		this.securityContextProvider = securityContextProvider ?? throw new ArgumentNullException( nameof( securityContextProvider ) );
		this.output = output ?? throw new ArgumentNullException( nameof( output ) );
		this.error = error ?? throw new ArgumentNullException( nameof( error ) );
	}

	/// <summary>Executes one parsed invocation.</summary>
	public async ValueTask<int> ExecuteAsync(
		InstallOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( options );
		var modeParse = FileModeParser.Parse( options.ModeText );
		if ( !modeParse.Succeeded ) return await UsageErrorAsync( modeParse.Message ?? "invalid mode" ).ConfigureAwait( false );
		var modeExpression = modeParse.Expression!;
		var requestedFileMode = modeExpression.Apply( new PosixFileMode( 0 ), isDirectory: false, creationMask: FileCreationMask.None );
		var ownership = await ResolveOwnershipAsync( options, cancellationToken ).ConfigureAwait( false );
		if ( ownership.Error is not null ) return await UsageErrorAsync( ownership.Error ).ConfigureAwait( false );
		await ReportIgnoredOptionsAsync( options ).ConfigureAwait( false );
		await ReportUnavailableContextPolicyAsync( options ).ConfigureAwait( false );
		if ( options.DirectoryMode ) {
			if ( options.Operands.Count == 0 ) return await UsageErrorAsync( "missing operand" ).ConfigureAwait( false );
			var status = 0;
			foreach ( var directory in options.Operands ) {
				try {
					await EnsureDirectoryAsync(
						directory,
						modeExpression,
						ownership.UserId,
						ownership.GroupId,
						options,
						cancellationToken
					).ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsControlled( exception ) ) {
					status = 1;
					await WriteErrorAsync( string.Concat( "cannot create directory '", directory, "': ", exception.Message ) ).ConfigureAwait( false );
				}
			}
			return status;
		}
		if ( options.Operands.Count == 0 ) return await UsageErrorAsync( "missing file operand" ).ConfigureAwait( false );
		var plans = await BuildPlansAsync( options, cancellationToken ).ConfigureAwait( false );
		if ( plans.Error is not null ) return await UsageErrorAsync( plans.Error ).ConfigureAwait( false );
		var result = 0;
		foreach ( var plan in plans.Plans ) {
			try {
				if ( options.CreateLeadingDirectories ) {
					var parent = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( plan.Destination ) );
					if ( !string.IsNullOrEmpty( parent ) ) {
						await EnsureDirectoryAsync(
							parent,
							null,
							null,
							null,
							options,
							cancellationToken,
							configureExistingFinal: false
						).ConfigureAwait( false );
					}
				} else {
					await RequireExistingDestinationParentAsync( plan.Destination, cancellationToken ).ConfigureAwait( false );
				}
				var installed = await InstallFileAsync(
					plan,
					requestedFileMode,
					ownership.UserId,
					ownership.GroupId,
					options,
					cancellationToken
				).ConfigureAwait( false );
				if ( options.Verbose ) {
					await output.WriteLineAsync(
						installed
							? string.Concat( "'", plan.Source, "' -> '", plan.Destination, "'" )
							: string.Concat( "omitted '", plan.Destination, "' (unchanged)" )
					).ConfigureAwait( false );
				}
				if ( options.Debug ) {
					await output.WriteLineAsync(
						installed
							? string.Concat( "install: debug: configured a private sibling stage and atomically published '", plan.Destination, "'" )
							: string.Concat( "install: debug: retained '", plan.Destination, "' after content and metadata comparison" )
					).ConfigureAwait( false );
				}
			} catch ( Exception exception ) when ( IsControlled( exception ) ) {
				result = 1;
				await WriteErrorAsync(
					string.Concat( "cannot install '", plan.Source, "' to '", plan.Destination, "': ", exception.Message )
				).ConfigureAwait( false );
			}
		}
		return result;
	}



	private async ValueTask ReportIgnoredOptionsAsync( InstallOptions options ) {
		if ( options.StripProgramWasExplicit && !options.Strip ) {
			await error.WriteLineAsync(
				"install: WARNING: ignoring --strip-program option as -s option was not specified"
			).ConfigureAwait( false );
		}
	}

	private async ValueTask ReportUnavailableContextPolicyAsync( InstallOptions options ) {
		if ( securityContextProvider.IsEnabled ) return;
		if ( options.PreserveContext ) {
			await error.WriteLineAsync(
				"install: WARNING: ignoring --preserve-context; this kernel is not SELinux-enabled"
			).ConfigureAwait( false );
		} else if ( options.ContextRequested && options.ExplicitContext is not null ) {
			await error.WriteLineAsync(
				"install: warning: ignoring --context; it requires an SELinux-enabled kernel"
			).ConfigureAwait( false );
		}
	}

	private async ValueTask<(uint? UserId, uint? GroupId, string? Error)> ResolveOwnershipAsync(
		InstallOptions options,
		CancellationToken cancellationToken
	) {
		uint? userId = null;
		uint? groupId = null;
		var current = await identityProvider.GetCurrentAsync( cancellationToken ).ConfigureAwait( false );
		if ( uint.TryParse( current.EffectiveUser.Id, out var currentUserId ) && currentUserId == 0 ) userId = 0;
		if ( uint.TryParse( current.EffectiveGroup.Id, out var currentGroupId ) ) groupId = currentGroupId;
		if ( options.Owner is not null ) {
			var owner = await OwnershipIdentityResolver.ResolveOwnerSpecAsync(
				options.Owner,
				identityProvider,
				cancellationToken
			).ConfigureAwait( false );
			if ( !owner.Succeeded ) return (null, null, owner.Message ?? "invalid owner");
			userId = owner.Selection!.UserId;
		}
		if ( options.Group is not null ) {
			var group = await OwnershipIdentityResolver.ResolveGroupAsync(
				options.Group,
				identityProvider,
				cancellationToken
			).ConfigureAwait( false );
			if ( !group.Succeeded ) return (null, null, group.Message ?? "invalid group");
			groupId = group.Selection!.GroupId;
		}
		return (userId, groupId, null);
	}

	private async ValueTask<(IReadOnlyList<InstallPlan> Plans, string? Error)> BuildPlansAsync(
		InstallOptions options,
		CancellationToken cancellationToken
	) {
		if ( options.TargetDirectory is not null ) {
			if ( options.Operands.Count == 0 ) return (Array.Empty<InstallPlan>(), "missing file operand");
			var targetExists = await IsDirectoryTargetAsync( options.TargetDirectory, cancellationToken ).ConfigureAwait( false );
			if ( options.CreateLeadingDirectories && !targetExists ) {
				await EnsureDirectoryAsync(
					options.TargetDirectory,
					null,
					null,
					null,
					options,
					cancellationToken,
					configureExistingFinal: false
				).ConfigureAwait( false );
			} else if ( !targetExists ) {
				return (Array.Empty<InstallPlan>(), string.Concat( "target directory '", options.TargetDirectory, "' does not exist" ));
			}
			return (
				options.Operands.Select( source => new InstallPlan(
					source,
					System.IO.Path.Combine( options.TargetDirectory, GetSourceName( source ) )
				) ).ToArray(),
				null
			);
		}
		if ( options.Operands.Count < 2 ) return (Array.Empty<InstallPlan>(), "missing destination file operand after source");
		var destinationArgument = options.Operands[^1];
		var sources = options.Operands.Take( options.Operands.Count - 1 ).ToArray();
		var destinationIsDirectory = !options.TreatDestinationAsFile
			&& await IsDirectoryTargetAsync( destinationArgument, cancellationToken ).ConfigureAwait( false );
		if ( sources.Length > 1 && !destinationIsDirectory ) {
			return (Array.Empty<InstallPlan>(), string.Concat( "target '", destinationArgument, "' is not a directory" ));
		}
		return (
			sources.Select( source => new InstallPlan(
				source,
				destinationIsDirectory
					? System.IO.Path.Combine( destinationArgument, GetSourceName( source ) )
					: destinationArgument
			) ).ToArray(),
			null
		);
	}

	private async ValueTask<bool> InstallFileAsync(
		InstallPlan plan,
		PosixFileMode requestedMode,
		uint? userId,
		uint? groupId,
		InstallOptions options,
		CancellationToken cancellationToken
	) {
		var sourceMetadata = await metadataProvider.GetMetadataAsync(
			plan.Source,
			PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		).ConfigureAwait( false );
		if ( sourceMetadata.Kind == FileSystemEntryKind.Directory ) {
			throw new IOException( "source is a directory" );
		}
		var destinationMetadata = await TryGetMetadataAsync(
			plan.Destination,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		if ( sourceMetadata.EntryIdentity.IsAvailable
			&& destinationMetadata is not null
			&& destinationMetadata.EntryIdentity.IsAvailable
			&& sourceMetadata.EntryIdentity == destinationMetadata.EntryIdentity ) {
			throw new IOException( "source and destination identify the same filesystem entry" );
		}
		if ( destinationMetadata is not null && destinationMetadata.Kind == FileSystemEntryKind.Directory ) {
			throw new IOException( "destination is a directory" );
		}
		if ( destinationMetadata?.IsPathIndirection == true || destinationMetadata?.IsReparsePoint == true ) {
			throw new IOException( "destination pathname names an indirection object; refusing to replace its target" );
		}
		if ( options.Compare && destinationMetadata is not null ) {
			if ( await IsEquivalentAsync(
				plan,
				destinationMetadata,
				requestedMode,
				userId,
				groupId,
				options,
				cancellationToken
			).ConfigureAwait( false ) ) return false;
		}
		var precondition = destinationMetadata is null
			? FileSystemMutationPrecondition.DestinationMustNotExist()
			: FileSystemMutationPrecondition.FromObservation(
				destinationMetadata.Kind,
				destinationMetadata.EntryIdentity,
				PathDereferenceMode.NoFollow
			);
		var backupPolicy = options.Backup
			? new TransactionalReplacementBackupPolicy {
				Mode = options.BackupMode,
				SimpleSuffix = options.BackupSuffix,
				Retention = TransactionalReplacementBackupRetention.RetainAfterSuccess
			}
			: TransactionalReplacementBackupPolicy.None;
		var artifact = new TransactionalReplacementArtifact(
			Guid.NewGuid().ToString( "N" ),
			plan.Destination,
			TransactionalReplacementAction.Replace,
			precondition,
			(destination, token) => CopySourceAsync( plan.Source, destination, token ),
			plan.Destination,
			explicitBackupPath: null,
			retainBackup: options.Backup,
			stagedFileConfigurator: (path, token) => ConfigureStagedFileAsync(
				plan.Source,
				plan.Destination,
				path,
				sourceMetadata,
				destinationMetadata is not null,
				requestedMode,
				userId,
				groupId,
				options,
				token
			)
		);
		var transactionOptions = new TransactionalReplacementOptions {
			AtomicityPolicy = TransactionalReplacementAtomicityPolicy.RequireAtomic,
			CommitPolicy = TransactionalReplacementCommitPolicy.StopAfterFailedUnit,
			BackupPolicy = backupPolicy,
			RequireStagedDurability = true,
			RequireDirectoryDurability = false
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { artifact },
			transactionFileSystem,
			transactionOptions
		);
		await transaction.StageAsync( cancellationToken ).ConfigureAwait( false );
		var result = await transaction.CommitAsync( cancellationToken ).ConfigureAwait( false );
		if ( !result.Succeeded ) {
			var detail = result.Diagnostics.Count == 0
				? result.Outcome.ToString()
				: string.Join( "; ", result.Diagnostics.Select( diagnostic => diagnostic.Message ) );
			throw new IOException( detail );
		}
		return true;
	}

	private async ValueTask ConfigureStagedFileAsync(
		string sourcePath,
		string destinationPath,
		string stagingPath,
		FileSystemMetadata sourceMetadata,
		bool destinationExisted,
		PosixFileMode requestedMode,
		uint? userId,
		uint? groupId,
		InstallOptions options,
		CancellationToken cancellationToken
	) {
		if ( options.Strip ) {
			await InstallStripper.StripAsync( options.StripProgram, stagingPath, cancellationToken ).ConfigureAwait( false );
		}
		var metadata = await metadataProvider.GetMetadataAsync(
			stagingPath,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		var precondition = FileSystemMutationPrecondition.FromObservation(
			metadata.Kind,
			metadata.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		if ( userId.HasValue || groupId.HasValue ) {
			var ownershipResult = await mutationProvider.SetOwnershipAsync(
				stagingPath,
				userId,
				groupId,
				PathDereferenceMode.NoFollow,
				precondition,
				cancellationToken
			).ConfigureAwait( false );
			RequireMutation( ownershipResult, "set ownership" );
			metadata = await metadataProvider.GetMetadataAsync(
				stagingPath,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			precondition = FileSystemMutationPrecondition.FromObservation(
				metadata.Kind,
				metadata.EntryIdentity,
				PathDereferenceMode.NoFollow
			);
		}
		if ( mutationProvider.Capabilities.CanSetModes ) {
			var modeResult = await mutationProvider.SetModeAsync(
				stagingPath,
				requestedMode,
				PathDereferenceMode.NoFollow,
				precondition,
				cancellationToken
			).ConfigureAwait( false );
			RequireMutation( modeResult, "set mode" );
		} else if ( options.ModeWasExplicit ) {
			throw new PlatformNotSupportedException( "POSIX mode mutation is not supported on this platform." );
		}
		if ( options.PreserveTimestamps ) {
			if ( !sourceMetadata.AccessTime.IsAvailable || !sourceMetadata.ModificationTime.IsAvailable ) {
				throw new IOException( "source access and modification timestamps are unavailable" );
			}
			var timestamps = new FileTimestampMutationRequest {
				AccessTime = FileTimestampChange.At( sourceMetadata.AccessTime.GetRequiredValue() ),
				ModificationTime = FileTimestampChange.At( sourceMetadata.ModificationTime.GetRequiredValue() )
			};
			var timestampResult = await metadataProvider.SetTimestampsAsync(
				stagingPath,
				timestamps,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( !timestampResult.Succeeded ) {
				throw new IOException( timestampResult.Message ?? "could not preserve timestamps", timestampResult.Exception );
			}
		}
		await securityContextProvider.ApplyAsync(
			sourcePath,
			destinationPath,
			stagingPath,
			options.PreserveContext,
			options.ContextRequested,
			options.ExplicitContext,
			destinationExisted,
			targetIsDirectory: false,
			cancellationToken: cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask<bool> IsEquivalentAsync(
		InstallPlan plan,
		FileSystemMetadata destinationMetadata,
		PosixFileMode requestedMode,
		uint? userId,
		uint? groupId,
		InstallOptions options,
		CancellationToken cancellationToken
	) {
		if ( destinationMetadata.Kind != FileSystemEntryKind.File ) return false;
		if ( !await FilesEqualAsync( plan.Source, plan.Destination, cancellationToken ).ConfigureAwait( false ) ) return false;
		if ( mutationProvider.Capabilities.CanSetModes ) {
			if ( !destinationMetadata.Mode.IsAvailable
				|| (destinationMetadata.Mode.GetRequiredValue() & 0x0fffU) != checked( (uint)requestedMode.Value ) ) return false;
		} else if ( options.ModeWasExplicit ) return false;
		if ( userId.HasValue && (!destinationMetadata.UserId.IsAvailable || destinationMetadata.UserId.GetRequiredValue() != userId.Value) ) return false;
		if ( groupId.HasValue && (!destinationMetadata.GroupId.IsAvailable || destinationMetadata.GroupId.GetRequiredValue() != groupId.Value) ) return false;
		if ( options.PreserveContext || (options.ContextRequested && options.ExplicitContext is null) ) {
			var destinationContext = await securityContextProvider.GetContextAsync(
				plan.Destination,
				cancellationToken
			).ConfigureAwait( false );
			var expectedContext = options.ExplicitContext;
			if ( options.PreserveContext ) {
				expectedContext = await securityContextProvider.GetContextAsync( plan.Source, cancellationToken ).ConfigureAwait( false );
			} else if ( options.ContextRequested && options.ExplicitContext is null ) {
				expectedContext = await securityContextProvider.GetDefaultContextAsync(
					plan.Destination,
					targetIsDirectory: false,
					cancellationToken: cancellationToken
				).ConfigureAwait( false );
			}
			if ( !string.Equals( destinationContext, expectedContext, StringComparison.Ordinal ) ) return false;
		}
		return true;
	}

	private async ValueTask EnsureDirectoryAsync(
		string path,
		FileModeExpression? finalModeExpression,
		uint? userId,
		uint? groupId,
		InstallOptions options,
		CancellationToken cancellationToken,
		bool configureExistingFinal = true
	) {
		var fullPath = System.IO.Path.GetFullPath( path );
		var missing = new Stack<string>();
		var current = fullPath;
		FileSystemMetadata? existing = null;
		while ( true ) {
			existing = await TryGetMetadataAsync( current, PathDereferenceMode.NoFollow, cancellationToken ).ConfigureAwait( false );
			if ( existing is not null ) break;
			missing.Push( current );
			var parent = System.IO.Path.GetDirectoryName( current );
			if ( string.IsNullOrEmpty( parent ) || string.Equals( parent, current, PathComparison ) ) {
				throw new DirectoryNotFoundException( string.Concat( "no existing parent for '", fullPath, "'" ) );
			}
			current = parent;
		}
		var existingMetadata = existing ?? throw new DirectoryNotFoundException( string.Concat( "no existing parent for '", fullPath, "'" ) );
		var existingDereferenceMode = PathDereferenceMode.NoFollow;
		if ( existingMetadata.IsPathIndirection || existingMetadata.IsReparsePoint ) {
			existingMetadata = await metadataProvider.GetMetadataAsync(
				current,
				PathDereferenceMode.FollowEligiblePathIndirection,
				cancellationToken
			).ConfigureAwait( false );
			RequireDirectoryTarget( current, existingMetadata );
			existingDereferenceMode = PathDereferenceMode.FollowEligiblePathIndirection;
		} else {
			RequirePhysicalDirectory( current, existingMetadata );
		}
		while ( missing.Count > 0 ) {
			var component = missing.Pop();
			var isFinal = missing.Count == 0;
			var mode = isFinal && finalModeExpression is not null
				? finalModeExpression.Apply( new PosixFileMode( 0 ), isDirectory: true, creationMask: FileCreationMask.None )
				: ParentDirectoryMode;
			var result = await mutationProvider.CreateDirectoryAsync(
				component,
				mode,
				FileCreationMask.None,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken
			).ConfigureAwait( false );
			var created = result.Succeeded;
			if ( !result.Succeeded ) {
				if ( result.ErrorCode != FileSystemMutationErrorCode.AlreadyExists ) RequireMutation( result, "create directory" );
				var raced = await metadataProvider.GetMetadataAsync(
					component,
					PathDereferenceMode.NoFollow,
					cancellationToken
				).ConfigureAwait( false );
				RequirePhysicalDirectory( component, raced );
			}
			if ( isFinal && finalModeExpression is not null ) {
				await ConfigureDirectoryAsync(
					component,
					mode,
					userId,
					groupId,
					options,
					destinationExisted: !created,
					dereferenceMode: PathDereferenceMode.NoFollow,
					cancellationToken: cancellationToken
				).ConfigureAwait( false );
			}
			if ( options.Verbose ) await output.WriteLineAsync( string.Concat( "created directory '", component, "'" ) ).ConfigureAwait( false );
		}
		if ( configureExistingFinal
			&& finalModeExpression is not null
			&& missing.Count == 0
			&& string.Equals( current, fullPath, PathComparison ) ) {
			var currentMode = existingMetadata.Mode.IsAvailable
				? new PosixFileMode( checked( (int)(existingMetadata.Mode.GetRequiredValue() & 0x0fffU) ) )
				: new PosixFileMode( 0 );
			var finalMode = finalModeExpression.Apply( currentMode, isDirectory: true, creationMask: FileCreationMask.None );
			await ConfigureDirectoryAsync(
				fullPath,
				finalMode,
				userId,
				groupId,
				options,
				destinationExisted: true,
				dereferenceMode: existingDereferenceMode,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
		}
	}

	private async ValueTask ConfigureDirectoryAsync(
		string path,
		PosixFileMode mode,
		uint? userId,
		uint? groupId,
		InstallOptions options,
		bool destinationExisted,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken
	) {
		var metadata = await metadataProvider.GetMetadataAsync( path, dereferenceMode, cancellationToken ).ConfigureAwait( false );
		if ( dereferenceMode == PathDereferenceMode.NoFollow ) RequirePhysicalDirectory( path, metadata );
		else RequireDirectoryTarget( path, metadata );
		var precondition = FileSystemMutationPrecondition.FromObservation(
			metadata.Kind,
			metadata.EntryIdentity,
			dereferenceMode
		);
		if ( userId.HasValue || groupId.HasValue ) {
			var ownership = await mutationProvider.SetOwnershipAsync(
				path,
				userId,
				groupId,
				dereferenceMode,
				precondition,
				cancellationToken
			).ConfigureAwait( false );
			RequireMutation( ownership, "set directory ownership" );
			metadata = await metadataProvider.GetMetadataAsync( path, dereferenceMode, cancellationToken ).ConfigureAwait( false );
			precondition = FileSystemMutationPrecondition.FromObservation( metadata.Kind, metadata.EntryIdentity, dereferenceMode );
		}
		if ( mutationProvider.Capabilities.CanSetModes ) {
			var modeResult = await mutationProvider.SetModeAsync(
				path,
				mode,
				dereferenceMode,
				precondition,
				cancellationToken
			).ConfigureAwait( false );
			RequireMutation( modeResult, "set directory mode" );
		} else if ( options.ModeWasExplicit ) {
			throw new PlatformNotSupportedException( "POSIX mode mutation is not supported on this platform." );
		}
		if ( options.ContextRequested ) {
			await securityContextProvider.ApplyAsync(
				sourcePath: null,
				destinationPath: path,
				stagingPath: path,
				preserveSourceContext: false,
				destinationDefaultContext: true,
				explicitContext: options.ExplicitContext,
				destinationExisted: destinationExisted,
				targetIsDirectory: true,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
		}
	}

	private async ValueTask RequireExistingDestinationParentAsync( string destination, CancellationToken cancellationToken ) {
		var parent = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( destination ) );
		if ( string.IsNullOrEmpty( parent ) ) return;
		var metadata = await metadataProvider.GetMetadataAsync(
			parent,
			PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		).ConfigureAwait( false );
		RequireDirectoryTarget( parent, metadata );
	}

	private async ValueTask<bool> IsDirectoryTargetAsync( string path, CancellationToken cancellationToken ) {
		var metadata = await TryGetMetadataAsync(
			path,
			PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		).ConfigureAwait( false );
		return metadata is not null && metadata.Kind == FileSystemEntryKind.Directory;
	}

	private async ValueTask<FileSystemMetadata?> TryGetMetadataAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken
	) {
		try {
			return await metadataProvider.GetMetadataAsync( path, dereferenceMode, cancellationToken ).ConfigureAwait( false );
		} catch ( FileNotFoundException ) {
			return null;
		} catch ( DirectoryNotFoundException ) {
			return null;
		}
	}

	private static void RequireDirectoryTarget( string path, FileSystemMetadata metadata ) {
		if ( metadata.Kind != FileSystemEntryKind.Directory ) {
			throw new IOException( string.Concat( "'", path, "' is not a directory" ) );
		}
	}

	private static void RequirePhysicalDirectory( string path, FileSystemMetadata metadata ) {
		if ( metadata.Kind != FileSystemEntryKind.Directory || metadata.IsPathIndirection || metadata.IsReparsePoint ) {
			throw new IOException( string.Concat( "'", path, "' is not a physical directory" ) );
		}
	}

	private static void RequireMutation( FileSystemMutationResult result, string operation ) {
		if ( result.Succeeded ) return;
		if ( !result.Supported ) throw new PlatformNotSupportedException( result.Message ?? string.Concat( operation, " is unsupported" ) );
		throw new IOException( result.Message ?? string.Concat( operation, " failed" ), result.Exception );
	}

	private static async ValueTask CopySourceAsync(
		string sourcePath,
		Stream destination,
		CancellationToken cancellationToken
	) {
		await using var source = new FileStream(
			sourcePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			CopyBufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		await source.CopyToAsync( destination, CopyBufferSize, cancellationToken ).ConfigureAwait( false );
	}

	private static async ValueTask<bool> FilesEqualAsync(
		string leftPath,
		string rightPath,
		CancellationToken cancellationToken
	) {
		var leftInfo = new FileInfo( leftPath );
		var rightInfo = new FileInfo( rightPath );
		if ( leftInfo.Length != rightInfo.Length ) return false;
		await using var left = new FileStream( leftPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan );
		await using var right = new FileStream( rightPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan );
		var leftBuffer = new byte[CopyBufferSize];
		var rightBuffer = new byte[CopyBufferSize];
		while ( true ) {
			var leftRead = await left.ReadAsync( leftBuffer, cancellationToken ).ConfigureAwait( false );
			var rightRead = await right.ReadAsync( rightBuffer, cancellationToken ).ConfigureAwait( false );
			if ( leftRead != rightRead ) return false;
			if ( leftRead == 0 ) return true;
			if ( !leftBuffer.AsSpan( 0, leftRead ).SequenceEqual( rightBuffer.AsSpan( 0, rightRead ) ) ) return false;
		}
	}

	private static string GetSourceName( string source ) {
		var trimmed = System.IO.Path.TrimEndingDirectorySeparator( source );
		var name = System.IO.Path.GetFileName( trimmed );
		if ( string.IsNullOrEmpty( name ) ) throw new ArgumentException( string.Concat( "invalid source pathname '", source, "'" ) );
		return name;
	}

	private async ValueTask<int> UsageErrorAsync( string message ) {
		await WriteErrorAsync( message ).ConfigureAwait( false );
		await error.WriteLineAsync( "Try 'install --help' for more information." ).ConfigureAwait( false );
		return 1;
	}

	private async ValueTask WriteErrorAsync( string message ) {
		await error.WriteLineAsync( string.Concat( "install: ", message ) ).ConfigureAwait( false );
	}

	private static bool IsControlled( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or ArgumentException
			or NotSupportedException
			or System.Security.SecurityException;
	}

	private static StringComparison PathComparison => OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	private sealed record InstallPlan( string Source, string Destination );
}
