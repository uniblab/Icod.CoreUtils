using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
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
	) {
		this.readOnlyProvider = readOnlyProvider ?? throw new ArgumentNullException( nameof( readOnlyProvider ) );
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.replacementFileSystem = replacementFileSystem ?? throw new ArgumentNullException( nameof( replacementFileSystem ) );
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
				? Path.Combine( destinationPath, GetSourceName( source ) )
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

		if ( source.Kind == FileSystemEntryKind.Directory && !options.Recursive && options.Operation == CopyMoveOperationKind.Copy ) {
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
				DirectRename( sourcePath, destinationPath, source.Kind, approvedOverwrite.Value.DestinationExists );
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
			await CreateRequestedLinkAsync( sourcePath, destinationPath, source.Kind, options, overwrite.DestinationExists, cancellationToken ).ConfigureAwait( false );
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
			&& initialDestination.Exists
			&& Directory.Exists( destinationPath )
			&& Directory.EnumerateFileSystemEntries( destinationPath ).Any() ) {
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Failed, "The destination directory is not empty." );
		}

		var copied = source.Kind == FileSystemEntryKind.Directory
			? await CopyDirectoryAsync( sourcePath, destinationPath, options, cancellationToken ).ConfigureAwait( false )
			: await CopySingleEntryAsync(
				sourcePath,
				destinationPath,
				source.Kind,
				source.WasDereferenced,
				source.IsPathIndirection,
				source.LinkTarget,
				null,
				approvedOverwrite,
				options,
				cancellationToken
			).ConfigureAwait( false );
		if ( !copied.Succeeded ) {
			return new CopyMoveItemResult( sourcePath, destinationPath, CopyMoveItemOutcome.Failed, copied.Message );
		}

		if ( options.Operation == CopyMoveOperationKind.Move ) {
			RemoveSourceAfterCopy( sourcePath, source.Kind );
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
			SparseFilePolicy = options.SparseFilePolicy
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
							entry.TraversalEntry.IsPathIndirection,
							entry.TraversalEntry.LinkTarget,
							entry,
							null,
							options,
							cancellationToken
						).ConfigureAwait( false );
						if ( !result.Succeeded ) return result;
						break;
					}
					case RecursiveMutationEventKind.LeaveDirectory:
						ApplyDirectoryMetadataBestEffort( item.Entry!, options );
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
		bool sourceIsPathIndirection,
		string? sourceLinkTarget,
		RecursiveMutationEntry? recursiveEntry,
		OverwriteDecision? approvedOverwrite,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
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
		if ( sourceIsPathIndirection && !sourceWasDereferenced ) {
			return await CopyPathIndirectionAsync( sourcePath, destinationPath, sourceLinkTarget, approvedOverwrite, options, cancellationToken ).ConfigureAwait( false );
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
			RemovePath( destinationPath );
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
		if ( options.ReflinkPolicy != CopyMoveReflinkPolicy.Never ) {
			if ( TryCloneFile( source, destination ) ) return (true, null);
			if ( options.ReflinkPolicy == CopyMoveReflinkPolicy.Always ) return (false, "A reflink was required but the host did not create one." );
		}
		if ( TryCopyFileRange( source, destination, cancellationToken ) ) return (true, null);
		var sparse = await sparseCopier.CopyAsync( source, destination, options.SparseFilePolicy, CopyBufferSize, cancellationToken ).ConfigureAwait( false );
		return (sparse.Succeeded, sparse.Message);
	}

	private async ValueTask<(bool Succeeded, string? Message)> CopyPathIndirectionAsync(
		string sourcePath,
		string destinationPath,
		string? sourceLinkTarget,
		OverwriteDecision? approvedOverwrite,
		CopyMoveOptions options,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		var overwrite = approvedOverwrite
			?? await EvaluateOverwriteAsync( sourcePath, destinationPath, options, cancellationToken ).ConfigureAwait( false );
		if ( !overwrite.Proceed ) return (true, overwrite.Message);
		if ( overwrite.DestinationExists ) RemovePath( destinationPath );
		var target = sourceLinkTarget;
		if ( string.IsNullOrEmpty( target ) ) {
			target = new FileInfo( sourcePath ).LinkTarget ?? new DirectoryInfo( sourcePath ).LinkTarget;
		}
		if ( string.IsNullOrEmpty( target ) ) return (false, string.Concat( "The symbolic-link target is unavailable: ", sourcePath ));
		try {
			var attributes = File.GetAttributes( sourcePath );
			if ( (attributes & FileAttributes.Directory) != 0 ) Directory.CreateSymbolicLink( destinationPath, target );
			else File.CreateSymbolicLink( destinationPath, target );
			return (true, null);
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return (false, exception.Message);
		}
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
			if ( overwrite.DestinationExists ) RemovePath( destinationPath );
			File.CreateHardLink( destinationPath, firstDestinationPath );
			return (true, null);
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return (false, exception.Message);
		}
	}

	private async ValueTask CreateRequestedLinkAsync(
		string sourcePath,
		string destinationPath,
		FileSystemEntryKind sourceKind,
		CopyMoveOptions options,
		bool destinationExists,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( destinationExists ) RemovePath( destinationPath );
		if ( options.CopyAsHardLink ) File.CreateHardLink( destinationPath, sourcePath );
		else if ( sourceKind == FileSystemEntryKind.Directory ) Directory.CreateSymbolicLink( destinationPath, sourcePath );
		else File.CreateSymbolicLink( destinationPath, sourcePath );
		await ValueTask.CompletedTask;
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
		&& sourceIdentity == destination.Metadata.EntryIdentity.GetRequiredValue();

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
		var trimmed = Path.TrimEndingDirectorySeparator( sourcePath );
		var name = Path.GetFileName( trimmed );
		if ( string.IsNullOrEmpty( name ) ) throw new ArgumentException( string.Concat( "The source has no usable basename: ", sourcePath ) );
		return name;
	}

	private static void DirectRename(
		string sourcePath,
		string destinationPath,
		FileSystemEntryKind sourceKind,
		bool destinationExists
	) {
		if ( sourceKind == FileSystemEntryKind.Directory ) {
			if ( destinationExists ) throw new IOException( "A directory destination already exists." );
			Directory.Move( sourcePath, destinationPath );
			return;
		}
		File.Move( sourcePath, destinationPath, destinationExists );
	}

	private static void RemoveSourceAfterCopy( string sourcePath, FileSystemEntryKind sourceKind ) {
		if ( sourceKind == FileSystemEntryKind.Directory ) Directory.Delete( sourcePath, recursive: true );
		else RemovePath( sourcePath );
	}

	private static void RemovePath( string path ) {
		var attributes = File.GetAttributes( path );
		if ( (attributes & FileAttributes.Directory) != 0 ) Directory.Delete( path, recursive: false );
		else File.Delete( path );
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
