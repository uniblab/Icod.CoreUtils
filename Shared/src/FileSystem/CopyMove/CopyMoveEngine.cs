using Path = global::System.IO.Path;
using System.Runtime.InteropServices;
using PathIndirectionInfo = Icod.Path.PathIndirectionInfo;
using PathIndirectionKind = Icod.Path.PathIndirectionKind;
using Icod.CoreUtils.Shared.FileSystem;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.CopyMove;

/// <summary>
/// Implements shared source/destination classification, E5 recursive planning, E6 replacement,
/// same-filesystem rename, cross-filesystem fallback, and partial-failure cleanup for <c>cp</c> and <c>mv</c>.
/// </summary>
public sealed class CopyMoveEngine {
	private const int CopyBufferSize = 128 * 1024;
	private readonly IReadOnlyFileSystemProvider readOnlyProvider;
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly ITransactionalReplacementFileSystem replacementFileSystem;
	private readonly IFileSystemMutationProvider mutationProvider;
	private readonly RecursiveMutationTraversalEngine traversal;
	private readonly SparseFileCopier sparseCopier;

	/// <summary>Initializes the engine with the system E1, E3, E5, and E6 providers.</summary>
	public CopyMoveEngine()
		: this(
			SystemReadOnlyFileSystemProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			SystemTransactionalReplacementFileSystem.Instance,
			SystemFileSystemOperations.Instance
		) {
	}

	/// <summary>Initializes the engine over injectable filesystem boundaries.</summary>
	/// <param name="readOnlyProvider">The E1 observation provider.</param>
	/// <param name="metadataProvider">The E3 metadata provider.</param>
	/// <param name="replacementFileSystem">The E6 transactional filesystem boundary.</param>
	/// <param name="fileSystemOperations">The sparse-file operation provider.</param>
	public CopyMoveEngine(
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		ITransactionalReplacementFileSystem replacementFileSystem,
		IFileSystemOperations fileSystemOperations
	) : this(
		readOnlyProvider,
		metadataProvider,
		replacementFileSystem,
		fileSystemOperations,
		new SystemFileSystemMutationProvider( metadataProvider )
	) {
	}

	/// <summary>Initializes the engine over fully injectable filesystem boundaries.</summary>
	/// <param name="readOnlyProvider">The E1 observation provider.</param>
	/// <param name="metadataProvider">The E3 metadata provider.</param>
	/// <param name="replacementFileSystem">The E6 transactional filesystem boundary.</param>
	/// <param name="fileSystemOperations">The sparse-file operation provider.</param>
	/// <param name="mutationProvider">The E4 single-path mutation provider.</param>
	public CopyMoveEngine(
		IReadOnlyFileSystemProvider readOnlyProvider,
		IFileSystemMetadataProvider metadataProvider,
		ITransactionalReplacementFileSystem replacementFileSystem,
		IFileSystemOperations fileSystemOperations,
		IFileSystemMutationProvider mutationProvider
	) {
		this.readOnlyProvider = readOnlyProvider ?? throw new ArgumentNullException( nameof( readOnlyProvider ) );
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.replacementFileSystem = replacementFileSystem ?? throw new ArgumentNullException( nameof( replacementFileSystem ) );
		this.mutationProvider = mutationProvider ?? throw new ArgumentNullException( nameof( mutationProvider ) );
		ArgumentNullException.ThrowIfNull( fileSystemOperations );
		traversal = new RecursiveMutationTraversalEngine( readOnlyProvider );
		sparseCopier = new SparseFileCopier( fileSystemOperations );
	}

	/// <summary>Copies or moves source operands to one destination operand.</summary>
	/// <param name="sourcePaths">The source operands.</param>
	/// <param name="destinationPath">The destination operand.</param>
	/// <param name="options">The operation policy.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The per-source result.</returns>
	public async ValueTask<CopyMoveResult> ExecuteAsync(
		IReadOnlyList<string> sourcePaths,
		string destinationPath,
		CopyMoveOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( sourcePaths );
		if ( sourcePaths.Count == 0 ) throw new ArgumentException( "At least one source pathname is required.", nameof( sourcePaths ) );
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		options ??= new CopyMoveOptions();
		options.Validate();

		var destinationIsDirectory = ResolveDestinationDirectoryMode( sourcePaths.Count, destinationPath, options.DestinationMode );
		var items = new List<CopyMoveItemResult>( sourcePaths.Count );
		for ( var index = 0; index < sourcePaths.Count; index++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var source = sourcePaths[index];
			if ( string.IsNullOrWhiteSpace( source ) ) {
				items.Add( new CopyMoveItemResult( source ?? string.Empty, destinationPath, CopyMoveItemOutcome.Failed, "The source pathname is empty." ) );
				continue;
			}
			var target = destinationIsDirectory
				? System.IO.Path.Combine( destinationPath, GetSourceName( source ) )
				: destinationPath;
			try {
				var outcome = await ExecuteOneAsync( source, target, options, cancellationToken ).ConfigureAwait( false );
				items.Add( outcome );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				throw;
			} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
				items.Add( new CopyMoveItemResult( source, target, CopyMoveItemOutcome.Failed, exception.Message ) );
			}
		}
		return new CopyMoveResult( items );
	}

	private async ValueTask<CopyMoveItemResult> ExecuteOneAsync(
		string sourcePath,
		string destinationPath,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		var source = await readOnlyProvider.ObserveAsync(
			sourcePath,
			options.SymbolicLinkMode == SymbolicLinkTraversalMode.Never
				? PathDereferenceMode.NoFollow
				: PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		).ConfigureAwait( false );
		var initialDestination = await replacementFileSystem.ObserveAsync(
			destinationPath,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		if ( IsSameEntry( source.EntryIdentity, initialDestination ) ) {
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Failed, "The source and destination identify the same filesystem entry." );
		}

		if ( source.Kind == FileSystemEntryKind.Directory
			&& !(source.IsReparsePoint && !source.WasDereferenced)
			&& !options.Recursive
			&& options.Operation == CopyMoveOperationKind.Copy ) {
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Failed, "Recursive mode was not specified for a directory operand." );
		}

		OverwriteDecision? approvedOverwrite = null;
		if ( options.Operation == CopyMoveOperationKind.Move
			&& options.BackupMode == TransactionalReplacementBackupMode.None
			&& !options.RemoveDestination ) {
			approvedOverwrite = await EvaluateOverwriteAsync( sourcePath, destinationPath, options, cancellationToken, initialDestination ).ConfigureAwait( false );
			if ( !approvedOverwrite.Value.Proceed ) {
				return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Skipped, approvedOverwrite.Value.Message );
			}
			try {
				DirectRename(
					sourcePath,
					destinationPath,
					IsPhysicalDirectoryObject( source ),
					approvedOverwrite.Value.DestinationExists
				);
				return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Completed );
			} catch ( IOException ) when ( !options.NoCopyFallback ) {
				// Continue with an E5/E6 copy and remove the source only after complete success.
			}
		}

		if ( options.CopyAsHardLink || options.CopyAsSymbolicLink ) {
			var overwrite = approvedOverwrite
				?? await EvaluateOverwriteAsync( sourcePath, destinationPath, options, cancellationToken, initialDestination ).ConfigureAwait( false );
			if ( !overwrite.Proceed ) {
				return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Skipped, overwrite.Message );
			}
			await CreateRequestedLinkAsync(
				sourcePath,
				destinationPath,
				source,
				options,
				overwrite,
				cancellationToken
			).ConfigureAwait( false );
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Completed );
		}

		if ( source.Kind != FileSystemEntryKind.Directory && approvedOverwrite is null ) {
			approvedOverwrite = await EvaluateOverwriteAsync(
				sourcePath,
				destinationPath,
				options,
				cancellationToken,
				initialDestination
			).ConfigureAwait( false );
			if ( !approvedOverwrite.Value.Proceed ) {
				return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Skipped, approvedOverwrite.Value.Message );
			}
		}

		if ( options.Operation == CopyMoveOperationKind.Move
			&& source.Kind == FileSystemEntryKind.Directory
			&& !(source.IsReparsePoint && !source.WasDereferenced)
			&& initialDestination.Exists
			&& Directory.Exists( destinationPath )
			&& Directory.EnumerateFileSystemEntries( destinationPath ).Any() ) {
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Failed, "The destination directory is not empty." );
		}

		var copyAsDirectory = source.Kind == FileSystemEntryKind.Directory
			&& !(source.IsReparsePoint && !source.WasDereferenced);
		var copied = copyAsDirectory
			? await CopyDirectoryAsync( sourcePath, destinationPath, options, cancellationToken ).ConfigureAwait( false )
			: await CopySingleEntryAsync(
				sourcePath,
				destinationPath,
				source.Kind,
				source.WasDereferenced,
				source.Indirection,
				null,
				approvedOverwrite,
				options,
				cancellationToken
			).ConfigureAwait( false );
		if ( !copied.Succeeded ) {
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Failed, copied.Message );
		}

		if ( options.Operation == CopyMoveOperationKind.Move ) {
			var removal = await RemoveSourceAfterCopyAsync(
				sourcePath,
				source,
				cancellationToken
			).ConfigureAwait( false );
			if ( !removal.Succeeded ) {
				return new CopyMoveItemResult(
					sourcePath,
					destinationPath,
					CopyMoveItemOutcome.Failed,
					removal.Message
				);
			}
		}
		return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Completed );
	}

	private async ValueTask<(bool Succeeded, string? Message)> CopyDirectoryAsync(
		string sourcePath,
		string destinationPath,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		var root = new PathTraversalRoot( sourcePath, 0, 0, sourcePath, sourcePath, PathTraversalRootKind.Literal );
		var traversalOptions = new RecursiveMutationOptions {
			PreserveRoot = true,
			DestinationPath = destinationPath,
			RequireStableEntryIdentity = true,
			SymbolicLinkMode = options.SymbolicLinkMode,
			FileSystemBoundaryMode = options.FileSystemBoundaryMode,
			ErrorMode = PathTraversalErrorMode.Stop,
			MetadataFields = options.MetadataFields,
			RequiredMetadataFields = options.RequiredMetadataFields,
			SparseFilePolicy = options.SparseFilePolicy,
			Selector = PhysicalReparsePointTraversalSelector.Instance
		};
		var createdDirectories = new List<string>();
		var completed = false;
		try {
			await foreach ( var item in traversal.TraverseAsync( new[] { root }, traversalOptions, cancellationToken ).ConfigureAwait( false ) ) {
				cancellationToken.ThrowIfCancellationRequested();
				switch ( item.Kind ) {
					case RecursiveMutationEventKind.Root:
						break;
					case RecursiveMutationEventKind.EnterDirectory: {
						var entry = item.Entry!;
						if ( entry.TraversalEntry.IsReparsePoint && !entry.TraversalEntry.WasDereferenced ) {
							var reparseResult = await CopySingleEntryAsync(
								entry.TraversalEntry.AccessPath,
								entry.DestinationPath!,
								entry.TraversalEntry.Kind,
								false,
								entry.TraversalEntry.Indirection,
								entry,
								null,
								options,
								cancellationToken
							).ConfigureAwait( false );
							if ( !reparseResult.Succeeded ) return reparseResult;
							break;
						}
						var target = entry.DestinationPath!;
						if ( PathExistsNoFollow( target ) ) {
							if ( !Directory.Exists( target ) ) return (false, string.Concat( "The destination is not a directory: ", target ));
						} else {
							Directory.CreateDirectory( target );
							createdDirectories.Add( target );
						}
						break;
					}
					case RecursiveMutationEventKind.Entry: {
						var entry = item.Entry!;
						var result = await CopySingleEntryAsync(
							entry.TraversalEntry.AccessPath,
							entry.DestinationPath!,
							entry.TraversalEntry.Kind,
							entry.TraversalEntry.WasDereferenced,
							entry.TraversalEntry.Indirection,
							entry,
							null,
							options,
							cancellationToken
						).ConfigureAwait( false );
						if ( !result.Succeeded ) return result;
						break;
					}
					case RecursiveMutationEventKind.LeaveDirectory:
						if ( !(item.Entry!.TraversalEntry.IsReparsePoint && !item.Entry.TraversalEntry.WasDereferenced) ) {
							ApplyDirectoryMetadataBestEffort( item.Entry!, options );
						}
						break;
					case RecursiveMutationEventKind.FileSystemBoundary:
						return (false, string.Concat( "The operation would cross a filesystem boundary at ", item.Entry!.TraversalEntry.DisplayPath, "." ));
					case RecursiveMutationEventKind.Cycle:
						return (false, string.Concat( "A directory cycle was detected at ", item.Entry!.TraversalEntry.DisplayPath, "." ));
					case RecursiveMutationEventKind.Error:
						return (false, item.Error!.Message);
					default:
						throw new InvalidOperationException( "Unsupported E5 traversal event." );
				}
			}
			completed = true;
			return (true, null);
		} finally {
			if ( !completed ) {
				for ( var index = createdDirectories.Count - 1; index >= 0; index-- ) {
					try {
						if ( Directory.Exists( createdDirectories[index] ) && !Directory.EnumerateFileSystemEntries( createdDirectories[index] ).Any() ) {
							Directory.Delete( createdDirectories[index] );
						}
					} catch ( IOException ) {
					} catch ( UnauthorizedAccessException ) {
					}
				}
			}
		}
	}

	private async ValueTask<(bool Succeeded, string? Message)> CopySingleEntryAsync(
		string sourcePath,
		string destinationPath,
		FileSystemEntryKind sourceKind,
		bool sourceWasDereferenced,
		PathIndirectionInfo sourceIndirection,
		RecursiveMutationEntry? recursiveEntry,
		OverwriteDecision? approvedOverwrite,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		if ( !sourceWasDereferenced && sourceIndirection.Kind != PathIndirectionKind.None ) {
			return await CopyPhysicalIndirectionAsync(
				sourcePath,
				destinationPath,
				sourceIndirection,
				approvedOverwrite,
				options,
				cancellationToken
			).ConfigureAwait( false );
		}
		if ( sourceKind == FileSystemEntryKind.File ) {
			if ( options.PreserveHardLinks && recursiveEntry?.IsRepeatedHardLink == true && recursiveEntry.FirstHardLinkDestinationPath is not null ) {
				return await CreatePreservedHardLinkAsync(
					recursiveEntry.FirstHardLinkDestinationPath,
					destinationPath,
					options,
					cancellationToken
				).ConfigureAwait( false );
			}
			return await CopyRegularFileAsync( sourcePath, destinationPath, sourceWasDereferenced, recursiveEntry, approvedOverwrite, options, cancellationToken ).ConfigureAwait( false );
		}
		return (false, string.Concat( "Unsupported file type: ", sourcePath ));
	}

	private async ValueTask<(bool Succeeded, string? Message)> CopyRegularFileAsync(
		string sourcePath,
		string destinationPath,
		bool sourceWasDereferenced,
		RecursiveMutationEntry? recursiveEntry,
		OverwriteDecision? approvedOverwrite,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		var overwrite = approvedOverwrite
			?? await EvaluateOverwriteAsync( sourcePath, destinationPath, options, cancellationToken ).ConfigureAwait( false );
		if ( !overwrite.Proceed ) return (true, overwrite.Message);
		if ( overwrite.DestinationExists && options.RemoveDestination && options.BackupMode == TransactionalReplacementBackupMode.None ) {
			var removal = await RemoveExistingDestinationAsync(
				destinationPath,
				overwrite.Observation,
				cancellationToken
			).ConfigureAwait( false );
			if ( !removal.Succeeded ) return removal;
			var removedObservation = await replacementFileSystem.ObserveAsync(
				destinationPath,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			overwrite = new OverwriteDecision( overwrite.Proceed, removedObservation, overwrite.Message );
		}

		var sourceMetadata = options.MetadataFields == RecursiveMetadataFields.None
			? null
			: await metadataProvider.GetMetadataAsync(
				sourcePath,
				sourceWasDereferenced
					? PathDereferenceMode.FollowEligiblePathIndirection
					: PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		var metadataPlan = sourceMetadata is null
			? null
			: RecursiveMetadataPreservationPlan.Create( sourceMetadata, options.MetadataFields, options.RequiredMetadataFields );
		if ( metadataPlan is not null && !metadataPlan.CanProceed ) return (false, "Required source metadata is unavailable." );

		var destinationObservation = overwrite.Observation;
		if ( destinationObservation.Exists && destinationObservation.Metadata!.Kind != FileSystemEntryKind.File ) {
			return (false, string.Concat( "The destination is not an ordinary file: ", destinationPath ));
		}
		var precondition = destinationObservation.Exists
			? FileSystemMutationPrecondition.FromObservation(
				destinationObservation.Metadata!.Kind,
				destinationObservation.Metadata.EntryIdentity,
				PathDereferenceMode.NoFollow
			)
			: FileSystemMutationPrecondition.DestinationMustNotExist();

		var artifact = new TransactionalReplacementArtifact(
			Guid.NewGuid().ToString( "N" ),
			destinationPath,
			TransactionalReplacementAction.Replace,
			precondition,
			async ( destination, token ) => {
				await using var source = new FileStream(
					sourcePath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read,
					CopyBufferSize,
					FileOptions.Asynchronous | FileOptions.SequentialScan
				);
				if ( destination is not FileStream destinationFile ) {
					await source.CopyToAsync( destination, CopyBufferSize, token ).ConfigureAwait( false );
					return;
				}
				var copied = await CopyContentAsync( source, destinationFile, options, token ).ConfigureAwait( false );
				if ( !copied.Succeeded ) throw new IOException( copied.Message ?? "The file contents could not be copied." );
			},
			sourcePath,
			sourceMetadata,
			metadataPlan,
			recursiveEntry
		);
		var transactionOptions = new TransactionalReplacementOptions {
			AtomicityPolicy = TransactionalReplacementAtomicityPolicy.PreferAtomic,
			CommitPolicy = TransactionalReplacementCommitPolicy.StopAfterFailedUnit,
			BackupPolicy = options.BackupMode == TransactionalReplacementBackupMode.None
				? TransactionalReplacementBackupPolicy.None
				: new TransactionalReplacementBackupPolicy {
					Mode = options.BackupMode,
					SimpleSuffix = options.BackupSuffix,
					Retention = TransactionalReplacementBackupRetention.RetainAfterSuccess
				},
			RequireStagedDurability = true
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { artifact },
			replacementFileSystem,
			transactionOptions
		);
		var result = await transaction.CommitAsync( cancellationToken ).ConfigureAwait( false );
		if ( result.Succeeded ) return (true, null);
		return (false, result.Diagnostics.Count == 0
			? string.Concat( "Transactional replacement failed with outcome ", result.Outcome, "." )
			: string.Join( "; ", result.Diagnostics.Select( diagnostic => diagnostic.Message ) ));
	}

	private async ValueTask<(bool Succeeded, string? Message)> CopyContentAsync(
		FileStream source,
		FileStream destination,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		if ( options.ReflinkPolicy == CopyMoveReflinkPolicy.Always ) {
			return TryCloneFile( source, destination )
				? (true, null)
				: (false, "A reflink was required but the host did not create one.");
		}
		if ( options.SparseFilePolicy == RecursiveSparseFilePolicy.Require ) {
			var requiredSparse = await sparseCopier.CopyAsync(
				source,
				destination,
				RecursiveSparseFilePolicy.Require,
				CopyBufferSize,
				cancellationToken
			).ConfigureAwait( false );
			return (requiredSparse.Succeeded, requiredSparse.Message);
		}
		if ( options.SparseFilePolicy == RecursiveSparseFilePolicy.WhenSupported ) {
			if ( options.ReflinkPolicy == CopyMoveReflinkPolicy.Auto && TryCloneFile( source, destination ) ) {
				return (true, null);
			}
			var sparse = await sparseCopier.CopyAsync(
				source,
				destination,
				RecursiveSparseFilePolicy.WhenSupported,
				CopyBufferSize,
				cancellationToken
			).ConfigureAwait( false );
			return (sparse.Succeeded, sparse.Message);
		}
		var dense = await sparseCopier.CopyAsync(
			source,
			destination,
			RecursiveSparseFilePolicy.Never,
			CopyBufferSize,
			cancellationToken
		).ConfigureAwait( false );
		return (dense.Succeeded, dense.Message);
	}

	private async ValueTask<(bool Succeeded, string? Message)> CopyPhysicalIndirectionAsync(
		string sourcePath,
		string destinationPath,
		PathIndirectionInfo indirection,
		OverwriteDecision? approvedOverwrite,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		var target = indirection.Target;
		if ( (indirection.Kind is PathIndirectionKind.PosixSymbolicLink or PathIndirectionKind.WindowsSymbolicLink)
			&& string.IsNullOrEmpty( target ) ) {
			target = new FileInfo( sourcePath ).LinkTarget ?? new DirectoryInfo( sourcePath ).LinkTarget;
		}
		switch ( indirection.Kind ) {
			case PathIndirectionKind.PosixSymbolicLink:
			case PathIndirectionKind.WindowsSymbolicLink:
				if ( string.IsNullOrEmpty( target ) ) {
					return (false, string.Concat( "The symbolic-link target is unavailable: ", sourcePath ));
				}
				break;
			case PathIndirectionKind.WindowsJunction:
				if ( string.IsNullOrEmpty( target ) ) {
					return (false, string.Concat( "The junction target is unavailable: ", sourcePath ));
				}
				break;
			case PathIndirectionKind.WindowsVolumeMountPoint:
				return (false, string.Concat(
					"Refusing to recreate a mounted volume at ", sourcePath,
					". Use a dereferencing option to copy the mounted contents instead."
				));
			case PathIndirectionKind.WindowsOtherNameSurrogate:
				return (false, DescribeUnsupportedReparsePoint( sourcePath, "unknown name-surrogate", indirection ));
			case PathIndirectionKind.WindowsCloudPlaceholder:
				return (false, DescribeUnsupportedReparsePoint( sourcePath, "Cloud Files placeholder", indirection ));
			case PathIndirectionKind.WindowsOpaqueReparsePoint:
				return (false, DescribeUnsupportedReparsePoint( sourcePath, "opaque reparse point", indirection ));
			case PathIndirectionKind.Unknown:
				return (false, DescribeUnsupportedReparsePoint( sourcePath, "uncharacterized reparse point", indirection ));
			default:
				return (false, string.Concat( "Unsupported pathname indirection: ", sourcePath ));
		}

		var overwrite = approvedOverwrite
			?? await EvaluateOverwriteAsync( sourcePath, destinationPath, options, cancellationToken ).ConfigureAwait( false );
		if ( !overwrite.Proceed ) return (true, overwrite.Message);
		if ( overwrite.DestinationExists && options.BackupMode != TransactionalReplacementBackupMode.None ) {
			return (false, "Backup replacement of links and reparse points is not supported; the existing destination was retained.");
		}
		if ( overwrite.DestinationExists ) {
			var removal = await RemoveExistingDestinationAsync(
				destinationPath,
				overwrite.Observation,
				cancellationToken
			).ConfigureAwait( false );
			if ( !removal.Succeeded ) return removal;
		}

		FileSystemMutationResult mutation;
		if ( indirection.Kind is PathIndirectionKind.PosixSymbolicLink or PathIndirectionKind.WindowsSymbolicLink ) {
			mutation = await mutationProvider.CreateSymbolicLinkAsync(
				destinationPath,
				target!,
				indirection.IsDirectory,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken
			).ConfigureAwait( false );
		} else {
			mutation = await mutationProvider.CreateJunctionAsync(
				destinationPath,
				target!,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
		}
		if ( mutation.Succeeded ) return (true, null);
		cancellationToken.ThrowIfCancellationRequested();
		return (false, mutation.Message ?? "The pathname-indirection object could not be created.");
	}

	private async ValueTask<(bool Succeeded, string? Message)> CreatePreservedHardLinkAsync(
		string firstDestinationPath,
		string destinationPath,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		var overwrite = await EvaluateOverwriteAsync( firstDestinationPath, destinationPath, options, cancellationToken ).ConfigureAwait( false );
		if ( !overwrite.Proceed ) return (true, overwrite.Message);
		try {
			if ( overwrite.DestinationExists ) {
				var removal = await RemoveExistingDestinationAsync(
					destinationPath,
					overwrite.Observation,
					cancellationToken
				).ConfigureAwait( false );
				if ( !removal.Succeeded ) return removal;
			}
			var result = await mutationProvider.CreateHardLinkAsync(
				destinationPath,
				firstDestinationPath,
				PathDereferenceMode.NoFollow,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
			if ( result.Succeeded ) return (true, null);
			cancellationToken.ThrowIfCancellationRequested();
			return (false, result.Message ?? "The preserved hard link could not be created.");
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return (false, exception.Message);
		}
	}

	private async ValueTask CreateRequestedLinkAsync(
		string sourcePath,
		string destinationPath,
		ReadOnlyFileSystemEntry source,
		CopyMoveOptions options,
		OverwriteDecision overwrite,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( overwrite.DestinationExists ) {
			var removal = await RemoveExistingDestinationAsync(
				destinationPath,
				overwrite.Observation,
				cancellationToken
			).ConfigureAwait( false );
			if ( !removal.Succeeded ) throw new IOException( removal.Message ?? "The existing destination could not be removed." );
		}
		FileSystemMutationResult result;
		if ( options.CopyAsHardLink ) {
			result = await mutationProvider.CreateHardLinkAsync(
				destinationPath,
				sourcePath,
				source.Kind is FileSystemEntryKind.SymbolicLink or FileSystemEntryKind.NameSurrogate
					? PathDereferenceMode.NoFollow
					: PathDereferenceMode.FollowEligiblePathIndirection,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
		} else {
			result = await mutationProvider.CreateSymbolicLinkAsync(
				destinationPath,
				sourcePath,
				IsPhysicalDirectoryObject( source ),
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken
			).ConfigureAwait( false );
		}
		if ( result.Succeeded ) return;
		cancellationToken.ThrowIfCancellationRequested();
		if ( !result.Supported ) throw new NotSupportedException( result.Message ?? "The requested link operation is unsupported." );
		throw new IOException( result.Message ?? "The requested link could not be created." );
	}

	private async ValueTask<OverwriteDecision> EvaluateOverwriteAsync(
		string sourcePath,
		string destinationPath,
		CopyMoveOptions options,
		CancellationToken cancellationToken,
		TransactionalReplacementObservation? knownObservation = null
	) {
		var observation = knownObservation
			?? await replacementFileSystem.ObserveAsync(
				destinationPath,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		if ( !observation.Exists ) return new OverwriteDecision( true, observation, null );
		if ( options.OverwriteMode == CopyMoveOverwriteMode.NoClobber ) return new OverwriteDecision( false, observation, "The existing destination was retained." );
		if ( options.OverwriteMode == CopyMoveOverwriteMode.Interactive ) {
			var accepted = await options.Prompt!( sourcePath, destinationPath, cancellationToken ).ConfigureAwait( false );
			return new OverwriteDecision( accepted, observation, accepted ? null : "Replacement was declined." );
		}
		if ( options.OverwriteMode == CopyMoveOverwriteMode.Update && observation.ModificationTime.HasValue ) {
			var sourceMetadata = await metadataProvider.GetMetadataAsync( sourcePath, true, cancellationToken ).ConfigureAwait( false );
			if ( sourceMetadata.ModificationTime.IsAvailable
				&& sourceMetadata.ModificationTime.GetRequiredValue() <= observation.ModificationTime.Value ) {
				return new OverwriteDecision( false, observation, "The destination is not older than the source." );
			}
		}
		return new OverwriteDecision( true, observation, null );
	}

	private readonly record struct OverwriteDecision(
		bool Proceed,
		TransactionalReplacementObservation Observation,
		string? Message
	) {
		public bool DestinationExists => Observation.Exists;
	}

	private static bool IsSameEntry(
		FileSystemEntryIdentity sourceIdentity,
		TransactionalReplacementObservation destination
	) => sourceIdentity.IsAvailable
		&& destination.Exists
		&& destination.Metadata!.EntryIdentity.IsAvailable
		&& sourceIdentity == destination.Metadata.EntryIdentity;

	private static bool ResolveDestinationDirectoryMode(
		int sourceCount,
		string destinationPath,
		CopyMoveDestinationMode mode
	) {
		if ( mode == CopyMoveDestinationMode.TargetDirectory ) {
			if ( !Directory.Exists( destinationPath ) ) throw new DirectoryNotFoundException( string.Concat( "Target directory does not exist: ", destinationPath ) );
			return true;
		}
		if ( mode == CopyMoveDestinationMode.NoTargetDirectory ) {
			if ( sourceCount != 1 ) throw new ArgumentException( "--no-target-directory requires exactly one source operand." );
			return false;
		}
		if ( sourceCount > 1 ) {
			if ( !Directory.Exists( destinationPath ) ) throw new DirectoryNotFoundException( "The destination must be an existing directory when multiple sources are supplied." );
			return true;
		}
		return Directory.Exists( destinationPath );
	}

	private static string GetSourceName( string sourcePath ) {
		var trimmed = System.IO.Path.TrimEndingDirectorySeparator( sourcePath );
		var name = System.IO.Path.GetFileName( trimmed );
		if ( string.IsNullOrEmpty( name ) ) throw new ArgumentException( string.Concat( "The source has no usable basename: ", sourcePath ) );
		return name;
	}

	private static void DirectRename(
		string sourcePath,
		string destinationPath,
		bool sourceIsDirectoryObject,
		bool destinationExists
	) {
		if ( sourceIsDirectoryObject ) {
			if ( destinationExists ) throw new IOException( "A directory destination already exists." );
			Directory.Move( sourcePath, destinationPath );
			return;
		}
		File.Move( sourcePath, destinationPath, destinationExists );
	}

	private async ValueTask<(bool Succeeded, string? Message)> RemoveSourceAfterCopyAsync(
		string sourcePath,
		ReadOnlyFileSystemEntry source,
		CancellationToken cancellationToken
	) {
		if ( source.Kind == FileSystemEntryKind.Directory && !(source.IsReparsePoint && !source.WasDereferenced) ) {
			var root = new PathTraversalRoot( sourcePath, 0, 0, sourcePath, sourcePath, PathTraversalRootKind.Literal );
			await foreach ( var item in traversal.TraverseAsync(
				new[] { root },
				new RecursiveMutationOptions {
					PreserveRoot = true,
					RequireStableEntryIdentity = true,
					SymbolicLinkMode = SymbolicLinkTraversalMode.Never,
					FileSystemBoundaryMode = FileSystemBoundaryMode.CrossFileSystems,
					ErrorMode = PathTraversalErrorMode.Stop,
					Selector = PhysicalReparsePointTraversalSelector.Instance
				},
				cancellationToken
			).ConfigureAwait( false ) ) {
				cancellationToken.ThrowIfCancellationRequested();
				if ( item.Kind is RecursiveMutationEventKind.Root or RecursiveMutationEventKind.EnterDirectory ) continue;
				if ( item.Kind == RecursiveMutationEventKind.Error ) return (false, item.Error!.Message);
				if ( item.Kind == RecursiveMutationEventKind.Cycle ) {
					return (false, string.Concat( "A directory cycle was detected while removing ", item.Entry!.TraversalEntry.DisplayPath, "." ));
				}
				if ( item.Kind == RecursiveMutationEventKind.FileSystemBoundary ) {
					return (false, string.Concat( "A filesystem boundary prevented source cleanup at ", item.Entry!.TraversalEntry.DisplayPath, "." ));
				}
				var entry = item.Entry!.TraversalEntry;
				var removal = await RemoveObservedEntryAsync(
					entry.AccessPath,
					entry.Kind,
					entry.EntryIdentity,
					entry.IsReparsePoint,
					entry.IsPathIndirection,
					cancellationToken
				).ConfigureAwait( false );
				if ( !removal.Succeeded ) return removal;
			}
			return (true, null);
		}
		var observation = await readOnlyProvider.ObserveAsync(
			sourcePath,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		return await RemoveObservedEntryAsync(
			sourcePath,
			observation.Kind,
			observation.EntryIdentity,
			observation.IsReparsePoint,
			observation.IsPathIndirection,
			cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask<(bool Succeeded, string? Message)> RemoveExistingDestinationAsync(
		string path,
		TransactionalReplacementObservation observation,
		CancellationToken cancellationToken
	) {
		if ( !observation.Exists || observation.Metadata is null ) return (true, null);
		var metadata = observation.Metadata;
		return await RemoveObservedEntryAsync(
			path,
			metadata.Kind,
			metadata.EntryIdentity,
			metadata.IsReparsePoint,
			metadata.IsPathIndirection,
			cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask<(bool Succeeded, string? Message)> RemoveObservedEntryAsync(
		string path,
		FileSystemEntryKind kind,
		FileSystemEntryIdentity identity,
		bool isReparsePoint,
		bool isPathIndirection,
		CancellationToken cancellationToken
	) {
		var precondition = CreatePhysicalRemovalPrecondition( kind, identity, isReparsePoint );
		var result = kind == FileSystemEntryKind.Directory && !isReparsePoint && !isPathIndirection
			? await mutationProvider.RemoveDirectoryAsync( path, precondition, cancellationToken ).ConfigureAwait( false )
			: await mutationProvider.RemoveFileAsync( path, precondition, cancellationToken ).ConfigureAwait( false );
		if ( result.Succeeded ) return (true, null);
		cancellationToken.ThrowIfCancellationRequested();
		return (false, result.Message ?? string.Concat( "The pathname could not be removed: ", path ));
	}

	private static FileSystemMutationPrecondition CreatePhysicalRemovalPrecondition(
		FileSystemEntryKind kind,
		FileSystemEntryIdentity identity,
		bool isReparsePoint
	) => new(
		FileSystemMutationExistence.MustExist,
		PathDereferenceMode.NoFollow,
		kind,
		identity.IsAvailable ? identity : null,
		rejectUncharacterizedIndirection: !isReparsePoint
	);

	private static bool IsPhysicalDirectoryObject( ReadOnlyFileSystemEntry source ) =>
		source.Kind == FileSystemEntryKind.Directory
		|| (!source.WasDereferenced && source.Indirection.IsDirectory);

	private static string DescribeUnsupportedReparsePoint(
		string sourcePath,
		string kind,
		PathIndirectionInfo indirection
	) {
		var tag = indirection.ReparseTag.HasValue
			? string.Concat( "0x", indirection.ReparseTag.Value.ToString( "X8" ) )
			: "unknown";
		return string.Concat(
			"Cannot safely recreate ", kind, " ", sourcePath,
			" (reparse tag ", tag, "). The source was retained."
		);
	}

	private static bool PathExistsNoFollow( string path ) {
		try {
			_ = File.GetAttributes( path );
			return true;
		} catch ( FileNotFoundException ) {
			return false;
		} catch ( DirectoryNotFoundException ) {
			return false;
		}
	}

	private static void ApplyDirectoryMetadataBestEffort( RecursiveMutationEntry entry, CopyMoveOptions options ) {
		var source = entry.TraversalEntry.AccessPath;
		var destination = entry.DestinationPath!;
		try {
			if ( (options.MetadataFields & RecursiveMetadataFields.Mode) != 0 && !OperatingSystem.IsWindows() ) {
				File.SetUnixFileMode( destination, File.GetUnixFileMode( source ) );
			}
			if ( (options.MetadataFields & RecursiveMetadataFields.AccessTime) != 0 ) {
				Directory.SetLastAccessTimeUtc( destination, Directory.GetLastAccessTimeUtc( source ) );
			}
			if ( (options.MetadataFields & RecursiveMetadataFields.ModificationTime) != 0 ) {
				Directory.SetLastWriteTimeUtc( destination, Directory.GetLastWriteTimeUtc( source ) );
			}
			if ( (options.MetadataFields & RecursiveMetadataFields.Attributes) != 0 ) {
				File.SetAttributes( destination, File.GetAttributes( source ) & ~FileAttributes.ReparsePoint );
			}
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( (options.RequiredMetadataFields & options.MetadataFields) != RecursiveMetadataFields.None ) throw;
		}
	}

	private static bool TryCloneFile( FileStream source, FileStream destination ) {
		if ( !OperatingSystem.IsLinux() ) return false;
		try {
			source.Position = 0;
			destination.Position = 0;
			destination.SetLength( 0 );
			var result = ioctl(
				destination.SafeFileHandle.DangerousGetHandle().ToInt32(),
				LinuxFiclone,
				source.SafeFileHandle.DangerousGetHandle().ToInt32()
			);
			if ( result == 0 ) {
				destination.Position = destination.Length;
				return true;
			}
		} catch ( Exception exception ) when ( exception is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException ) {
		}
		ResetCopyStreams( source, destination );
		return false;
	}

	private static bool TryCopyFileRange( FileStream source, FileStream destination, CancellationToken cancellationToken ) {
		if ( !OperatingSystem.IsLinux() ) return false;
		try {
			ResetCopyStreams( source, destination );
			var remaining = source.Length;
			while ( remaining > 0 ) {
				cancellationToken.ThrowIfCancellationRequested();
				var requested = (nuint)Math.Min( remaining, 1024 * 1024 );
				var copied = copy_file_range(
					source.SafeFileHandle.DangerousGetHandle().ToInt32(),
					IntPtr.Zero,
					destination.SafeFileHandle.DangerousGetHandle().ToInt32(),
					IntPtr.Zero,
					requested,
					0
				);
				if ( copied <= 0 ) {
					ResetCopyStreams( source, destination );
					return false;
				}
				remaining -= (long)copied;
			}
			source.Position = source.Length;
			destination.Position = destination.Length;
			return true;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) when ( exception is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException ) {
			ResetCopyStreams( source, destination );
			return false;
		}
	}

	private static void ResetCopyStreams( FileStream source, FileStream destination ) {
		source.Position = 0;
		destination.Position = 0;
		destination.SetLength( 0 );
	}

	private static bool IsControlledException( Exception exception ) => exception is
		ArgumentException
		or IOException
		or UnauthorizedAccessException
		or NotSupportedException
		or System.Security.SecurityException;

	private const ulong LinuxFiclone = 0x40049409;

	[DllImport( "libc", SetLastError = true )]
	private static extern int ioctl( int fileDescriptor, ulong request, int argument );

	[DllImport( "libc", SetLastError = true )]
	private static extern nint copy_file_range(
		int sourceFileDescriptor,
		IntPtr sourceOffset,
		int destinationFileDescriptor,
		IntPtr destinationOffset,
		nuint count,
		uint flags
	);
}
