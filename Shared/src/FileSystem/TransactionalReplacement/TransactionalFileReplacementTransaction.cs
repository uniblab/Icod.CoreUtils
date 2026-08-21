using Path = global::System.IO.Path;
using System.Globalization;
using System.IO;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CommandFramework.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>
/// Stages complete sibling files, commits independent recovery units, and deterministically restores or cleans every artifact.
/// </summary>
public sealed class TransactionalFileReplacementTransaction : IAsyncDisposable {
	private readonly IReadOnlyList<TransactionalReplacementArtifact> artifacts;
	private readonly ITransactionalReplacementFileSystem fileSystem;
	private readonly TransactionalReplacementOptions options;
	private readonly TransactionalReplacementPathSafety pathSafety;
	private readonly TransactionalBackupNameGenerator backupNameGenerator;
	private readonly ITransactionalReplacementFailureInjector failureInjector;
	private readonly List<StagedArtifact> staged = new();
	private readonly List<TransactionalReplacementDiagnostic> diagnostics = new();
	private bool stageAttempted;
	private bool stageCompleted;
	private bool commitAttempted;
	private bool disposed;

	/// <summary>Initializes one immutable replacement transaction.</summary>
	/// <param name="artifacts">The artifacts in deterministic plan order.</param>
	/// <param name="fileSystem">The injectable E3/E4/E6 filesystem boundary.</param>
	/// <param name="options">The transaction options.</param>
	/// <param name="pathSafety">Optional injected containment policy.</param>
	/// <param name="backupNameGenerator">Optional injected backup-name generator.</param>
	/// <param name="failureInjector">Optional lifecycle failure injector.</param>
	public TransactionalFileReplacementTransaction(
		IReadOnlyList<TransactionalReplacementArtifact> artifacts,
		ITransactionalReplacementFileSystem fileSystem,
		TransactionalReplacementOptions? options = null,
		TransactionalReplacementPathSafety? pathSafety = null,
		TransactionalBackupNameGenerator? backupNameGenerator = null,
		ITransactionalReplacementFailureInjector? failureInjector = null
	) {
		ArgumentNullException.ThrowIfNull( artifacts );
		if ( 0 == artifacts.Count ) {
			throw new ArgumentException( "A replacement transaction requires at least one artifact.", nameof( artifacts ) );
		}
		this.artifacts = Array.AsReadOnly( artifacts.ToArray() );
		this.fileSystem = fileSystem ?? throw new ArgumentNullException( nameof( fileSystem ) );
		this.options = options ?? TransactionalReplacementOptions.Default;
		this.options.Validate();
		this.pathSafety = pathSafety ?? new TransactionalReplacementPathSafety();
		this.backupNameGenerator = backupNameGenerator ?? new TransactionalBackupNameGenerator();
		this.failureInjector = failureInjector ?? NullTransactionalReplacementFailureInjector.Instance;
		ValidateDistinctDestinations( this.artifacts );
	}

	/// <summary>Stages every artifact and every recoverable original before changing a destination.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing staging.</returns>
	public async Task StageAsync( CancellationToken cancellationToken = default ) {
		ObjectDisposedException.ThrowIf( disposed, this );
		if ( stageCompleted ) {
			return;
		}
		if ( stageAttempted ) {
			throw new InvalidOperationException( "The replacement transaction staging attempt has already failed." );
		}
		stageAttempted = true;
		var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		var reservedPaths = new HashSet<string>( artifacts.Select( artifact => NormalizePath( artifact.Path ) ), comparer );
		try {
			if ( options.RequireDirectoryDurability && !fileSystem.Capabilities.SupportsDirectoryDurability ) {
				throw CreateFailure(
					TransactionalReplacementDiagnosticCode.DurabilityUnavailable,
					TransactionalReplacementStage.Validate,
					artifacts[0],
					artifacts[0].Path,
					"The provider cannot durably flush containing directories."
				);
			}
			foreach ( var artifact in artifacts ) {
				cancellationToken.ThrowIfCancellationRequested();
				var item = new StagedArtifact( artifact );
				staged.Add( item );
				await failureInjector.OnStageAsync(
					TransactionalReplacementStage.Validate,
					artifact,
					cancellationToken
				).ConfigureAwait( false );
				await ValidateContainedAsync( artifact, artifact.Path, cancellationToken ).ConfigureAwait( false );
				item.OriginalObservation = await fileSystem.ObserveAsync(
					artifact.Path,
					artifact.Precondition.DereferenceMode,
					cancellationToken
				).ConfigureAwait( false );
				RequirePrecondition( artifact, artifact.Precondition, item.OriginalObservation! );
				RequireMutableOrdinaryFile( artifact, item.OriginalObservation! );
				RequireAtomicCapabilityBeforeCommit( artifact, item.OriginalObservation!.Exists );
				if ( TransactionalReplacementAction.ValidateOnly == artifact.Action ) {
					continue;
				}
				if ( item.OriginalObservation!.Exists ) {
					await failureInjector.OnStageAsync(
						TransactionalReplacementStage.PreserveRollback,
						artifact,
						cancellationToken
					).ConfigureAwait( false );
					item.RollbackPath = await StageCopyAsync(
						artifact,
						artifact.Path,
						artifact.Path,
						"rollback",
						value => item.RollbackPath = value,
						cancellationToken
					).ConfigureAwait( false );
				}
				if ( TransactionalReplacementAction.Replace == artifact.Action ) {
					item.TemporaryPath = await StageContentAsync(
						artifact,
						value => item.TemporaryPath = value,
						cancellationToken
					).ConfigureAwait( false );
				}
				var retainBackup = artifact.RetainBackup
					|| TransactionalReplacementBackupRetention.RetainAfterSuccess == options.BackupPolicy.Retention;
				if ( item.OriginalObservation!.Exists && retainBackup ) {
					if ( artifact.ExplicitBackupPath is null
						&& TransactionalReplacementBackupRetention.RetainAfterSuccess != options.BackupPolicy.Retention ) {
						throw CreateFailure(
							TransactionalReplacementDiagnosticCode.BackupFailed,
							TransactionalReplacementStage.Validate,
							artifact,
							artifact.Path,
							"Per-artifact backup retention requires an explicit backup pathname or a transaction backup policy."
						);
					}
					item.BackupPath = artifact.ExplicitBackupPath
						?? await backupNameGenerator.GenerateAsync(
							artifact.Path,
							options.BackupPolicy,
							(path, token) => PathExistsOrReservedAsync( path, reservedPaths, token ),
							(path, maximum, token) => AnyNumberedBackupExistsAsync( path, maximum, reservedPaths, token ),
							cancellationToken
						).ConfigureAwait( false );
					if ( string.IsNullOrEmpty( item.BackupPath ) ) {
						throw CreateFailure(
							TransactionalReplacementDiagnosticCode.BackupFailed,
							TransactionalReplacementStage.Validate,
							artifact,
							artifact.Path,
							"Backup retention was requested but no backup pathname was generated."
						);
					}
					await ValidateContainedAsync( artifact, item.BackupPath!, cancellationToken ).ConfigureAwait( false );
					var normalizedBackup = NormalizePath( item.BackupPath! );
					if ( !reservedPaths.Add( normalizedBackup ) ) {
						throw CreateFailure(
							TransactionalReplacementDiagnosticCode.UnsafePath,
							TransactionalReplacementStage.Validate,
							artifact,
							item.BackupPath!,
							"A backup pathname collides with another transaction destination or backup."
						);
					}
					item.BackupObservation = await fileSystem.ObserveAsync(
						item.BackupPath!,
						PathDereferenceMode.NoFollow,
						cancellationToken
					).ConfigureAwait( false );
					RequireMutableOrdinaryFile( artifact, item.BackupObservation!, item.BackupPath! );
					RequireAtomicBackupCapabilityBeforeCommit( artifact, item.BackupObservation!.Exists );
					if ( item.BackupObservation!.Exists ) {
						item.BackupRollbackPath = await StageCopyAsync(
							artifact,
							item.BackupPath!,
							item.BackupPath!,
							"backup-rollback",
							value => item.BackupRollbackPath = value,
							cancellationToken
						).ConfigureAwait( false );
					}
					item.BackupStagePath = await StageCopyAsync(
						artifact,
						item.RollbackPath!,
						item.BackupPath!,
						"backup",
						value => item.BackupStagePath = value,
						cancellationToken
					).ConfigureAwait( false );
				}
			}
			stageCompleted = true;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			_ = await CleanupAsync( staged, injectFailure: false ).ConfigureAwait( false );
			throw;
		} catch ( TransactionFailureException exception ) {
			diagnostics.Add( exception.Diagnostic );
			_ = await CleanupAsync( staged, injectFailure: false ).ConfigureAwait( false );
			throw;
		} catch ( Exception exception ) {
			var artifact = staged.LastOrDefault()?.Artifact ?? artifacts[0];
			diagnostics.Add(
				new TransactionalReplacementDiagnostic(
					TransactionalReplacementDiagnosticCode.StagingFailed,
					TransactionalReplacementStage.WriteTemporary,
					artifact.RecoveryUnitId,
					artifact.Path,
					exception.Message,
					exception
				)
			);
			_ = await CleanupAsync( staged, injectFailure: false ).ConfigureAwait( false );
			throw;
		}
	}

	/// <summary>Commits staged recovery units and restores the failing unit after any later artifact failure.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The complete structured transaction result.</returns>
	public async Task<TransactionalReplacementResult> CommitAsync(
		CancellationToken cancellationToken = default
	) {
		ObjectDisposedException.ThrowIf( disposed, this );
		if ( commitAttempted ) {
			throw new InvalidOperationException( "The replacement transaction has already been committed." );
		}
		commitAttempted = true;
		try {
			await StageAsync( cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( TransactionFailureException exception ) {
			var cleanupSucceeded = await CleanupAsync( staged, injectFailure: false ).ConfigureAwait( false );
			var o = !cleanupSucceeded
				? TransactionalReplacementOutcome.FailedCleanupIncomplete
				: TransactionalReplacementDiagnosticCode.AtomicityUnavailable == exception.Diagnostic.Code
					? TransactionalReplacementOutcome.FailedAtomicityUnavailable
					: TransactionalReplacementOutcome.FailedBeforeCommit;
			return CreateResult( o, Array.Empty<string>(), Array.Empty<string>() );
		} catch {
			var cleanupSucceeded = await CleanupAsync( staged, injectFailure: false ).ConfigureAwait( false );
			return CreateResult(
				cleanupSucceeded
					? TransactionalReplacementOutcome.FailedBeforeCommit
					: TransactionalReplacementOutcome.FailedCleanupIncomplete,
				Array.Empty<string>(),
				Array.Empty<string>()
			);
		}

		var committedUnits = new List<string>();
		var rolledBackUnits = new List<string>();
		var failed = false;
		var changedThenRolledBack = false;
		var rollbackIncomplete = false;
		var cleanupIncomplete = false;
		foreach ( var unit in GetRecoveryUnits() ) {
			var unitChanged = false;
			try {
				unitChanged = await CommitUnitAsync( unit.Items, cancellationToken ).ConfigureAwait( false );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				var rollbackSucceeded = await RollbackUnitAsync(
					unit.Id,
					unit.Items,
					rolledBackUnits
				).ConfigureAwait( false );
				if ( !rollbackSucceeded ) {
					rollbackIncomplete = true;
				}
				if ( !await CleanupAsync( staged, injectFailure: true ).ConfigureAwait( false ) ) {
					cleanupIncomplete = true;
				}
				throw;
			} catch ( TransactionFailureException exception ) {
				diagnostics.Add( exception.Diagnostic );
				failed = true;
				unitChanged = unit.Items.Any( item => item.Committed || item.BackupCommitted );
				var rollbackSucceeded = await RollbackUnitAsync(
					unit.Id,
					unit.Items,
					rolledBackUnits
				).ConfigureAwait( false );
				changedThenRolledBack |= unitChanged && rollbackSucceeded;
				rollbackIncomplete |= !rollbackSucceeded;
				_ = await CleanupAsync( unit.Items, injectFailure: true ).ConfigureAwait( false );
				if ( TransactionalReplacementCommitPolicy.StopAfterFailedUnit == options.CommitPolicy ) {
					break;
				}
				continue;
			} catch ( Exception exception ) {
				var artifact = unit.Items.FirstOrDefault( item => item.Committed || item.BackupCommitted )?.Artifact
					?? unit.Items[0].Artifact;
				diagnostics.Add(
					new TransactionalReplacementDiagnostic(
						TransactionalReplacementDiagnosticCode.CommitFailed,
						TransactionalReplacementStage.Commit,
						artifact.RecoveryUnitId,
						artifact.Path,
						exception.Message,
						exception
					)
				);
				failed = true;
				unitChanged = unit.Items.Any( item => item.Committed || item.BackupCommitted );
				var rollbackSucceeded = await RollbackUnitAsync(
					unit.Id,
					unit.Items,
					rolledBackUnits
				).ConfigureAwait( false );
				changedThenRolledBack |= unitChanged && rollbackSucceeded;
				rollbackIncomplete |= !rollbackSucceeded;
				_ = await CleanupAsync( unit.Items, injectFailure: true ).ConfigureAwait( false );
				if ( TransactionalReplacementCommitPolicy.StopAfterFailedUnit == options.CommitPolicy ) {
					break;
				}
				continue;
			}

			committedUnits.Add( unit.Id );
			_ = await CleanupAsync( unit.Items, injectFailure: true ).ConfigureAwait( false );
		}

		cleanupIncomplete = !await CleanupAsync(
			staged,
			injectFailure: true
		).ConfigureAwait( false );
		var outcome = rollbackIncomplete
			? TransactionalReplacementOutcome.FailedRollbackIncomplete
			: cleanupIncomplete
				? TransactionalReplacementOutcome.FailedCleanupIncomplete
				: !failed
					? TransactionalReplacementOutcome.Succeeded
					: 0 < committedUnits.Count
						? TransactionalReplacementOutcome.FailedPartiallyCommitted
						: changedThenRolledBack
							? TransactionalReplacementOutcome.FailedRolledBack
							: TransactionalReplacementOutcome.FailedBeforeCommit;
		return CreateResult( outcome, committedUnits, rolledBackUnits );
	}

	private async Task<bool> CommitUnitAsync(
		IReadOnlyList<StagedArtifact> items,
		CancellationToken cancellationToken
	) {
		var changed = false;
		foreach ( var item in items ) {
			var artifact = item.Artifact;
			cancellationToken.ThrowIfCancellationRequested();
			await failureInjector.OnStageAsync(
				TransactionalReplacementStage.Revalidate,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			var current = await fileSystem.ObserveAsync(
				artifact.Path,
				artifact.Precondition.DereferenceMode,
				cancellationToken
			).ConfigureAwait( false );
			RequireObservationUnchanged( artifact, item.OriginalObservation!, current );
			if ( TransactionalReplacementAction.ValidateOnly == artifact.Action ) {
				continue;
			}
			await failureInjector.OnStageAsync(
				TransactionalReplacementStage.Commit,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			TransactionalReplacementCommitResult? commitResult = null;
			if ( TransactionalReplacementAction.Delete == artifact.Action ) {
				if ( item.OriginalObservation!.Exists ) {
					commitResult = await fileSystem.DeleteFileAsync(
						artifact.Path,
						artifact.Precondition,
						cancellationToken
					).ConfigureAwait( false );
					item.Committed = true;
					changed = true;
				}
			} else {
				commitResult = await fileSystem.CommitFileAsync(
					item.TemporaryPath!,
					artifact.Path,
					item.OriginalObservation!.Exists,
					TransactionalReplacementAtomicityPolicy.RequireAtomic != options.AtomicityPolicy,
					cancellationToken
				).ConfigureAwait( false );
				item.TemporaryPath = null;
				item.Committed = true;
				changed = true;
				var sourceMetadata = artifact.SourceMetadata;
				var metadataPlan = artifact.MetadataPlan;
				if ( sourceMetadata is not null && metadataPlan is not null ) {
					await failureInjector.OnStageAsync(
						TransactionalReplacementStage.ApplyMetadata,
						artifact,
						cancellationToken
					).ConfigureAwait( false );
					try {
						await fileSystem.ApplyMetadataAsync(
							artifact.Path,
							sourceMetadata,
							metadataPlan,
							cancellationToken
						).ConfigureAwait( false );
					} catch ( Exception exception ) {
						throw CreateFailure(
							TransactionalReplacementDiagnosticCode.MetadataFailed,
							TransactionalReplacementStage.ApplyMetadata,
							artifact,
							artifact.Path,
							exception.Message,
							exception
						);
					}
				}
			}
			if ( commitResult is not null ) {
				await RecordAtomicityAsync( item, commitResult, artifact.Path ).ConfigureAwait( false );
				await FlushDirectoryAsync( item, artifact.Path, cancellationToken ).ConfigureAwait( false );
			}
			if ( item.BackupStagePath is not null ) {
				await PublishBackupAsync( item, cancellationToken ).ConfigureAwait( false );
				changed = true;
			}
		}
		return changed;
	}

	private async Task PublishBackupAsync( StagedArtifact item, CancellationToken cancellationToken ) {
		var artifact = item.Artifact;
		await failureInjector.OnStageAsync(
			TransactionalReplacementStage.Revalidate,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
		var current = await fileSystem.ObserveAsync(
			item.BackupPath!,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		RequireObservationUnchanged( artifact, item.BackupObservation!, current, item.BackupPath! );
		await failureInjector.OnStageAsync(
			TransactionalReplacementStage.PublishBackup,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
		try {
			var result = await fileSystem.CommitFileAsync(
				item.BackupStagePath!,
				item.BackupPath!,
				item.BackupObservation!.Exists,
				TransactionalReplacementAtomicityPolicy.RequireAtomic != options.AtomicityPolicy,
				cancellationToken
			).ConfigureAwait( false );
			item.BackupStagePath = null;
			item.BackupCommitted = true;
			await RecordAtomicityAsync( item, result, item.BackupPath! ).ConfigureAwait( false );
			await fileSystem.RestoreMetadataAsync(
				item.BackupPath!,
				item.OriginalObservation!.Metadata!,
				cancellationToken
			).ConfigureAwait( false );
			await FlushDirectoryAsync( item, item.BackupPath!, cancellationToken ).ConfigureAwait( false );
		} catch ( TransactionFailureException ) {
			throw;
		} catch ( Exception exception ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.BackupFailed,
				TransactionalReplacementStage.PublishBackup,
				artifact,
				item.BackupPath!,
				exception.Message,
				exception
			);
		}
	}

	private async Task<bool> RollbackUnitAsync(
		string unitId,
		IReadOnlyList<StagedArtifact> items,
		ICollection<string> rolledBackUnits
	) {
		var succeeded = true;
		var recoveredAny = false;
		for ( var index = items.Count - 1; 0 <= index; index-- ) {
			var item = items[index];
			var artifact = item.Artifact;
			if ( item.BackupCommitted ) {
				try {
					await failureInjector.OnStageAsync(
						TransactionalReplacementStage.Rollback,
						artifact,
						CancellationToken.None
					).ConfigureAwait( false );
					await RestorePathAsync(
						artifact,
						item.BackupPath!,
						item.BackupObservation!,
						item.BackupRollbackPath,
						CancellationToken.None
					).ConfigureAwait( false );
					item.BackupRollbackPath = null;
					item.BackupCommitted = false;
					recoveredAny = true;
				} catch ( Exception exception ) {
					succeeded = false;
					diagnostics.Add(
						new TransactionalReplacementDiagnostic(
							TransactionalReplacementDiagnosticCode.RollbackFailed,
							TransactionalReplacementStage.Rollback,
							artifact.RecoveryUnitId,
							item.BackupPath!,
							exception.Message,
							exception
						)
					);
				}
			}
			if ( item.Committed ) {
				try {
					await failureInjector.OnStageAsync(
						TransactionalReplacementStage.Rollback,
						artifact,
						CancellationToken.None
					).ConfigureAwait( false );
					await RestorePathAsync(
						artifact,
						artifact.Path,
						item.OriginalObservation!,
						item.RollbackPath,
						CancellationToken.None
					).ConfigureAwait( false );
					item.RollbackPath = null;
					item.Committed = false;
					item.RolledBack = true;
					recoveredAny = true;
				} catch ( Exception exception ) {
					succeeded = false;
					diagnostics.Add(
						new TransactionalReplacementDiagnostic(
							TransactionalReplacementDiagnosticCode.RollbackFailed,
							TransactionalReplacementStage.Rollback,
							artifact.RecoveryUnitId,
							artifact.Path,
							exception.Message,
							exception
						)
					);
				}
			}
		}
		if ( recoveredAny && succeeded ) {
			rolledBackUnits.Add( unitId );
		}
		return succeeded;
	}

	private async Task RestorePathAsync(
		TransactionalReplacementArtifact artifact,
		string path,
		TransactionalReplacementObservation original,
		string? rollbackPath,
		CancellationToken cancellationToken
	) {
		if ( original.Exists ) {
			if ( string.IsNullOrEmpty( rollbackPath ) ) {
				throw new IOException( string.Concat( "No rollback file remains for ", path, "." ) );
			}
			var current = await fileSystem.ObserveAsync(
				path,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			_ = await fileSystem.CommitFileAsync(
				rollbackPath,
				path,
				current.Exists,
				allowNonAtomicFallback: true,
				cancellationToken
			).ConfigureAwait( false );
			await failureInjector.OnStageAsync(
				TransactionalReplacementStage.RestoreMetadata,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await fileSystem.RestoreMetadataAsync(
				path,
				original.Metadata!,
				cancellationToken
			).ConfigureAwait( false );
			_ = await fileSystem.FlushContainingDirectoryAsync( path, cancellationToken ).ConfigureAwait( false );
		} else {
			var current = await fileSystem.ObserveAsync(
				path,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( current.Exists ) {
				var precondition = FileSystemMutationPrecondition.FromObservation(
					current.Metadata!.Kind,
					current.Metadata.EntryIdentity,
					PathDereferenceMode.NoFollow
				);
				_ = await fileSystem.DeleteFileAsync( path, precondition, cancellationToken ).ConfigureAwait( false );
				_ = await fileSystem.FlushContainingDirectoryAsync( path, cancellationToken ).ConfigureAwait( false );
			}
		}
	}

	private async Task<bool> CleanupAsync(
		IEnumerable<StagedArtifact> items,
		bool injectFailure
	) {
		var materialized = items.ToArray();
		var journal = new RecursiveCleanupJournal();
		foreach ( var item in materialized ) {
			RegisterCleanup( journal, item, () => item.TemporaryPath, value => item.TemporaryPath = value, injectFailure );
			RegisterCleanup( journal, item, () => item.BackupStagePath, value => item.BackupStagePath = value, injectFailure );
			RegisterCleanup( journal, item, () => item.BackupRollbackPath, value => item.BackupRollbackPath = value, injectFailure );
			RegisterCleanup( journal, item, () => item.RollbackPath, value => item.RollbackPath = value, injectFailure );
		}
		var report = await journal.RollbackAsync( CancellationToken.None ).ConfigureAwait( false );
		foreach ( var failure in report.Failures ) {
			var separator = failure.Description.IndexOf( '|' );
			var unitId = 0 <= separator ? failure.Description[..separator] : "transaction";
			var path = 0 <= separator ? failure.Description[(separator + 1)..] : failure.Description;
			diagnostics.Add(
				new TransactionalReplacementDiagnostic(
					TransactionalReplacementDiagnosticCode.CleanupFailed,
					TransactionalReplacementStage.Cleanup,
					unitId,
					path,
					failure.Message,
					failure.Exception
				)
			);
		}
		return report.Succeeded;
	}

	private void RegisterCleanup(
		RecursiveCleanupJournal journal,
		StagedArtifact item,
		Func<string?> getPath,
		Action<string?> setPath,
		bool injectFailure
	) {
		var path = getPath();
		if ( string.IsNullOrEmpty( path ) ) {
			return;
		}
		journal.Register(
			string.Concat( item.Artifact.RecoveryUnitId, "|", path ),
			async _ => {
				if ( injectFailure ) {
					await failureInjector.OnStageAsync(
						TransactionalReplacementStage.Cleanup,
						item.Artifact,
						CancellationToken.None
					).ConfigureAwait( false );
				}
				await fileSystem.DeleteTemporaryFileAsync( path, CancellationToken.None ).ConfigureAwait( false );
				setPath( null );
			}
		);
	}

	private async Task<string> StageContentAsync(
		TransactionalReplacementArtifact artifact,
		Action<string?> preserveForCleanup,
		CancellationToken cancellationToken
	) {
		var path = await CreateTemporaryAsync( artifact, artifact.Path, "stage", cancellationToken ).ConfigureAwait( false );
		try {
			await failureInjector.OnStageAsync(
				TransactionalReplacementStage.WriteTemporary,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await fileSystem.WriteTemporaryFileAsync(
				path,
				artifact.ContentWriter!,
				cancellationToken
			).ConfigureAwait( false );
			if ( artifact.StagedFileConfigurator is not null ) {
				await artifact.StagedFileConfigurator( path, cancellationToken ).ConfigureAwait( false );
			}
			await FlushStagedAsync( artifact, path, cancellationToken ).ConfigureAwait( false );
			return path;
		} catch {
			if ( !await TryDeleteTemporaryAsync( artifact, path ).ConfigureAwait( false ) ) {
				preserveForCleanup( path );
			}
			throw;
		}
	}

	private async Task<string> StageCopyAsync(
		TransactionalReplacementArtifact artifact,
		string sourcePath,
		string siblingOfPath,
		string purpose,
		Action<string?> preserveForCleanup,
		CancellationToken cancellationToken
	) {
		var path = await CreateTemporaryAsync( artifact, siblingOfPath, purpose, cancellationToken ).ConfigureAwait( false );
		try {
			await failureInjector.OnStageAsync(
				TransactionalReplacementStage.WriteTemporary,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await fileSystem.CopyTemporaryFileAsync( sourcePath, path, cancellationToken ).ConfigureAwait( false );
			await FlushStagedAsync( artifact, path, cancellationToken ).ConfigureAwait( false );
			return path;
		} catch {
			if ( !await TryDeleteTemporaryAsync( artifact, path ).ConfigureAwait( false ) ) {
				preserveForCleanup( path );
			}
			throw;
		}
	}

	private async Task<string> CreateTemporaryAsync(
		TransactionalReplacementArtifact artifact,
		string destinationPath,
		string purpose,
		CancellationToken cancellationToken
	) {
		await failureInjector.OnStageAsync(
			TransactionalReplacementStage.CreateTemporary,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
		return await fileSystem.CreateSiblingTemporaryFileAsync(
			destinationPath,
			purpose,
			cancellationToken
		).ConfigureAwait( false );
	}

	private async Task FlushStagedAsync(
		TransactionalReplacementArtifact artifact,
		string path,
		CancellationToken cancellationToken
	) {
		await failureInjector.OnStageAsync(
			TransactionalReplacementStage.FlushTemporary,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
		var result = await fileSystem.FlushFileAsync( path, cancellationToken ).ConfigureAwait( false );
		var item = staged.Last( candidate => ReferenceEquals( candidate.Artifact, artifact ) );
		item.StagedDurability = WeakerDurability( item.StagedDurability, result.Durability );
		if ( TransactionalReplacementDurability.Unsupported == result.Durability ) {
			var diagnostic = new TransactionalReplacementDiagnostic(
				TransactionalReplacementDiagnosticCode.DurabilityUnavailable,
				TransactionalReplacementStage.FlushTemporary,
				artifact.RecoveryUnitId,
				path,
				result.Message ?? "Durable staged-file flushing is unavailable."
			);
			if ( options.RequireStagedDurability ) {
				throw new TransactionFailureException( diagnostic );
			}
			diagnostics.Add( diagnostic );
		}
	}

	private async Task FlushDirectoryAsync(
		StagedArtifact item,
		string path,
		CancellationToken cancellationToken
	) {
		await failureInjector.OnStageAsync(
			TransactionalReplacementStage.FlushDirectory,
			item.Artifact,
			cancellationToken
		).ConfigureAwait( false );
		var result = await fileSystem.FlushContainingDirectoryAsync( path, cancellationToken ).ConfigureAwait( false );
		item.DirectoryDurability = WeakerDurability( item.DirectoryDurability, result.Durability );
		if ( TransactionalReplacementDurability.Unsupported == result.Durability ) {
			var diagnostic = new TransactionalReplacementDiagnostic(
				TransactionalReplacementDiagnosticCode.DurabilityUnavailable,
				TransactionalReplacementStage.FlushDirectory,
				item.Artifact.RecoveryUnitId,
				path,
				result.Message ?? "Containing-directory durability is unavailable."
			);
			if ( options.RequireDirectoryDurability ) {
				throw new TransactionFailureException( diagnostic );
			}
			diagnostics.Add( diagnostic );
		}
	}

	private ValueTask RecordAtomicityAsync(
		StagedArtifact item,
		TransactionalReplacementCommitResult result,
		string path
	) {
		item.Atomicity = WeakerAtomicity( item.Atomicity, result.Atomicity );
		if ( TransactionalReplacementAtomicity.Atomic == result.Atomicity ) {
			return ValueTask.CompletedTask;
		}
		var diagnostic = new TransactionalReplacementDiagnostic(
			TransactionalReplacementDiagnosticCode.AtomicityUnavailable,
			TransactionalReplacementStage.Commit,
			item.Artifact.RecoveryUnitId,
			path,
			result.Message ?? "Atomic replacement was unavailable."
		);
		if ( TransactionalReplacementAtomicityPolicy.RequireAtomic == options.AtomicityPolicy ) {
			throw new TransactionFailureException( diagnostic );
		}
		diagnostics.Add( diagnostic );
		return ValueTask.CompletedTask;
	}

	private async ValueTask ValidateContainedAsync(
		TransactionalReplacementArtifact artifact,
		string path,
		CancellationToken cancellationToken
	) {
		var containmentRootPath = options.ContainmentRootPath;
		if ( string.IsNullOrEmpty( containmentRootPath ) ) {
			return;
		}
		try {
			_ = await pathSafety.RequireContainedAsync(
				containmentRootPath,
				path,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( Exception exception ) when ( exception is ArgumentException or InvalidOperationException or IOException ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.UnsafePath,
				TransactionalReplacementStage.Validate,
				artifact,
				path,
				exception.Message,
				exception
			);
		}
	}

	private void RequireAtomicCapabilityBeforeCommit(
		TransactionalReplacementArtifact artifact,
		bool destinationExists
	) {
		if ( TransactionalReplacementAtomicityPolicy.RequireAtomic != options.AtomicityPolicy
			|| TransactionalReplacementAction.ValidateOnly == artifact.Action ) {
			return;
		}
		var supported = TransactionalReplacementAction.Delete == artifact.Action
			? !destinationExists || fileSystem.Capabilities.SupportsAtomicDelete
			: destinationExists
				? fileSystem.Capabilities.SupportsAtomicReplaceExisting
				: fileSystem.Capabilities.SupportsAtomicPublishNew;
		if ( !supported ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.AtomicityUnavailable,
				TransactionalReplacementStage.Validate,
				artifact,
				artifact.Path,
				"The provider cannot satisfy the required atomicity policy for this destination."
			);
		}
	}

	private void RequireAtomicBackupCapabilityBeforeCommit(
		TransactionalReplacementArtifact artifact,
		bool backupExists
	) {
		if ( TransactionalReplacementAtomicityPolicy.RequireAtomic != options.AtomicityPolicy ) {
			return;
		}
		var supported = backupExists
			? fileSystem.Capabilities.SupportsAtomicReplaceExisting
			: fileSystem.Capabilities.SupportsAtomicPublishNew;
		if ( !supported ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.AtomicityUnavailable,
				TransactionalReplacementStage.Validate,
				artifact,
				artifact.ExplicitBackupPath ?? artifact.Path,
				"The provider cannot publish the retained backup atomically."
			);
		}
	}

	private static void RequireMutableOrdinaryFile(
		TransactionalReplacementArtifact artifact,
		TransactionalReplacementObservation observation,
		string? diagnosticPath = null
	) {
		if ( TransactionalReplacementAction.ValidateOnly == artifact.Action || !observation.Exists ) {
			return;
		}
		var metadata = observation.Metadata!;
		if ( FileSystemEntryKind.File != metadata.Kind ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				diagnosticPath ?? observation.Path,
				"E6 transactional replacement supports existing ordinary files only."
			);
		}
		if ( !metadata.EntryIdentity.IsAvailable ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				diagnosticPath ?? observation.Path,
				"The existing file has no stable E3 identity and cannot be revalidated safely."
			);
		}
	}

	private static void RequirePrecondition(
		TransactionalReplacementArtifact artifact,
		FileSystemMutationPrecondition precondition,
		TransactionalReplacementObservation observation
	) {
		if ( FileSystemMutationExistence.MustExist == precondition.Existence && !observation.Exists ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination does not exist."
			);
		}
		if ( FileSystemMutationExistence.MustNotExist == precondition.Existence && observation.Exists ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination already exists."
			);
		}
		if ( !observation.Exists ) {
			return;
		}
		var metadata = observation.Metadata!;
		if ( precondition.ExpectedKind.HasValue && precondition.ExpectedKind.Value != metadata.Kind ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination object kind changed."
			);
		}
		if ( precondition.ExpectedIdentity.HasValue
			&& !precondition.ExpectedIdentity.Value.Equals( metadata.EntryIdentity ) ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination identity changed."
			);
		}
		if ( precondition.ExpectedUserId.HasValue
			&& (!metadata.UserId.IsAvailable
				|| precondition.ExpectedUserId.Value != metadata.UserId.GetRequiredValue()) ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination owner changed."
			);
		}
		if ( precondition.ExpectedGroupId.HasValue
			&& (!metadata.GroupId.IsAvailable
				|| precondition.ExpectedGroupId.Value != metadata.GroupId.GetRequiredValue()) ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination group changed."
			);
		}
		if ( precondition.RejectUncharacterizedIndirection
			&& metadata.IsReparsePoint
			&& !metadata.IsSymbolicLink
			&& !metadata.IsJunction
			&& !metadata.IsVolumeMountPoint
			&& !metadata.IsCloudPlaceholder ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Validate,
				artifact,
				observation.Path,
				"The destination is an uncharacterized pathname-indirection object."
			);
		}
	}

	private static void RequireObservationUnchanged(
		TransactionalReplacementArtifact artifact,
		TransactionalReplacementObservation expected,
		TransactionalReplacementObservation current,
		string? diagnosticPath = null
	) {
		var path = diagnosticPath ?? artifact.Path;
		if ( expected.Exists != current.Exists ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Revalidate,
				artifact,
				path,
				expected.Exists
					? "The destination disappeared after staging."
					: "The destination appeared after staging."
			);
		}
		if ( !expected.Exists ) {
			return;
		}
		var before = expected.Metadata!;
		var after = current.Metadata!;
		if ( before.Kind != after.Kind || !before.EntryIdentity.Equals( after.EntryIdentity ) ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Revalidate,
				artifact,
				path,
				"The destination identity changed after staging."
			);
		}
		if ((expected.Length.HasValue
				&& current.Length.HasValue
				&& expected.Length.Value != current.Length.Value)
			|| (expected.ModificationTime.HasValue
				&& current.ModificationTime.HasValue
				&& expected.ModificationTime.Value != current.ModificationTime.Value)) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Revalidate,
				artifact,
				path,
				"The destination changed after staging."
			);
		}
		if ( diagnosticPath is not null ) {
			return;
		}
		var precondition = artifact.Precondition;
		if ( precondition.ExpectedUserId.HasValue
			&& (!after.UserId.IsAvailable
				|| precondition.ExpectedUserId.Value != after.UserId.GetRequiredValue()) ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Revalidate,
				artifact,
				path,
				"The destination owner changed after staging."
			);
		}
		if ( precondition.ExpectedGroupId.HasValue
			&& (!after.GroupId.IsAvailable
				|| precondition.ExpectedGroupId.Value != after.GroupId.GetRequiredValue()) ) {
			throw CreateFailure(
				TransactionalReplacementDiagnosticCode.PreconditionFailed,
				TransactionalReplacementStage.Revalidate,
				artifact,
				path,
				"The destination group changed after staging."
			);
		}
	}

	private async ValueTask<bool> PathExistsOrReservedAsync(
		string path,
		ISet<string> reservedPaths,
		CancellationToken cancellationToken
	) {
		if ( reservedPaths.Contains( NormalizePath( path ) ) ) {
			return true;
		}
		return (await fileSystem.ObserveAsync(
			path,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false )).Exists;
	}

	private async ValueTask<bool> AnyNumberedBackupExistsAsync(
		string destinationPath,
		int maximumNumberedBackup,
		ISet<string> reservedPaths,
		CancellationToken cancellationToken
	) {
		var prefix = string.Concat( NormalizePath( destinationPath ), ".~" );
		foreach ( var reservedPath in reservedPaths ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( !reservedPath.StartsWith( prefix, OperatingSystem.IsWindows()
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal ) || !reservedPath.EndsWith( '~' ) ) {
				continue;
			}
			var numberText = reservedPath.AsSpan( prefix.Length, reservedPath.Length - prefix.Length - 1 );
			if ( int.TryParse( numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number )
				&& 1 <= number
				&& number <= maximumNumberedBackup ) {
				return true;
			}
		}
		return await fileSystem.AnyNumberedBackupExistsAsync(
			destinationPath,
			maximumNumberedBackup,
			cancellationToken
		).ConfigureAwait( false );
	}

	private IReadOnlyList<RecoveryUnit> GetRecoveryUnits() {
		var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		var order = new List<string>();
		var groups = new Dictionary<string, List<StagedArtifact>>( comparer );
		foreach ( var item in staged ) {
			if ( !groups.TryGetValue( item.Artifact.RecoveryUnitId, out var group ) ) {
				group = new List<StagedArtifact>();
				groups.Add( item.Artifact.RecoveryUnitId, group );
				order.Add( item.Artifact.RecoveryUnitId );
			}
			group.Add( item );
		}
		return order.Select( id => new RecoveryUnit( id, groups[id] ) ).ToArray();
	}

	private TransactionalReplacementResult CreateResult(
		TransactionalReplacementOutcome outcome,
		IReadOnlyList<string> committedUnits,
		IReadOnlyList<string> rolledBackUnits
	) {
		var reports = staged.Select(
			item => new TransactionalReplacementArtifactReport(
				item.Artifact.RecoveryUnitId,
				item.Artifact.Path,
				item.Committed,
				item.RolledBack,
				item.BackupCommitted ? item.BackupPath : null,
				item.Atomicity,
				item.StagedDurability,
				item.DirectoryDurability
			)
		).ToArray();
		return new TransactionalReplacementResult(
			outcome,
			diagnostics,
			reports,
			committedUnits,
			rolledBackUnits
		);
	}

	private static TransactionFailureException CreateFailure(
		TransactionalReplacementDiagnosticCode code,
		TransactionalReplacementStage stage,
		TransactionalReplacementArtifact artifact,
		string path,
		string message,
		Exception? exception = null
	) {
		return new TransactionFailureException(
			new TransactionalReplacementDiagnostic(
				code,
				stage,
				artifact.RecoveryUnitId,
				path,
				message,
				exception
			)
		);
	}

	private static string NormalizePath( string path ) {
		return System.IO.Path.TrimEndingDirectorySeparator( System.IO.Path.GetFullPath( path ) );
	}

	private static void ValidateDistinctDestinations( IReadOnlyList<TransactionalReplacementArtifact> artifacts ) {
		var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		var paths = new HashSet<string>( comparer );
		foreach ( var artifact in artifacts ) {
			ArgumentNullException.ThrowIfNull( artifact );
			if ( !paths.Add( NormalizePath( artifact.Path ) ) ) {
				throw new ArgumentException(
					string.Concat( "The transaction contains duplicate destination path: ", artifact.Path ),
					nameof( artifacts )
				);
			}
		}
	}

	private async ValueTask<bool> TryDeleteTemporaryAsync( TransactionalReplacementArtifact artifact, string path ) {
		try {
			await fileSystem.DeleteTemporaryFileAsync( path, CancellationToken.None ).ConfigureAwait( false );
			return true;
		} catch ( Exception exception ) {
			diagnostics.Add(
				new TransactionalReplacementDiagnostic(
					TransactionalReplacementDiagnosticCode.CleanupFailed,
					TransactionalReplacementStage.Cleanup,
					artifact.RecoveryUnitId,
					path,
					exception.Message,
					exception
				)
			);
			return false;
		}
	}

	private static TransactionalReplacementAtomicity WeakerAtomicity(
		TransactionalReplacementAtomicity left,
		TransactionalReplacementAtomicity right
	) {
		if ( TransactionalReplacementAtomicity.NonAtomic == left
			|| TransactionalReplacementAtomicity.NonAtomic == right ) {
			return TransactionalReplacementAtomicity.NonAtomic;
		}
		if ( TransactionalReplacementAtomicity.Unknown == left
			|| TransactionalReplacementAtomicity.Unknown == right ) {
			return TransactionalReplacementAtomicity.Unknown;
		}
		return TransactionalReplacementAtomicity.Atomic;
	}

	private static TransactionalReplacementDurability WeakerDurability(
		TransactionalReplacementDurability left,
		TransactionalReplacementDurability right
	) {
		if ( TransactionalReplacementDurability.Unsupported == left
			|| TransactionalReplacementDurability.Unsupported == right ) {
			return TransactionalReplacementDurability.Unsupported;
		}
		if ( TransactionalReplacementDurability.NotRequested == left ) {
			return right;
		}
		if ( TransactionalReplacementDurability.NotRequested == right ) {
			return left;
		}
		return TransactionalReplacementDurability.Durable;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		if ( disposed ) {
			return;
		}
		disposed = true;
		_ = await CleanupAsync( staged, injectFailure: false ).ConfigureAwait( false );
	}

	private sealed class StagedArtifact {
		/// <summary>Initializes mutable state for one immutable artifact.</summary>
		public StagedArtifact( TransactionalReplacementArtifact artifact ) {
			Artifact = artifact;
		}

		/// <summary>Gets the immutable plan artifact.</summary>
		public TransactionalReplacementArtifact Artifact { get; }
		/// <summary>Gets or sets the initial destination observation.</summary>
		public TransactionalReplacementObservation? OriginalObservation { get; set; }
		/// <summary>Gets or sets the staged replacement pathname.</summary>
		public string? TemporaryPath { get; set; }
		/// <summary>Gets or sets the destination rollback pathname.</summary>
		public string? RollbackPath { get; set; }
		/// <summary>Gets or sets the public backup pathname.</summary>
		public string? BackupPath { get; set; }
		/// <summary>Gets or sets the initial backup observation.</summary>
		public TransactionalReplacementObservation? BackupObservation { get; set; }
		/// <summary>Gets or sets the staged public-backup pathname.</summary>
		public string? BackupStagePath { get; set; }
		/// <summary>Gets or sets the prior-backup rollback pathname.</summary>
		public string? BackupRollbackPath { get; set; }
		/// <summary>Gets or sets whether the destination remains committed.</summary>
		public bool Committed { get; set; }
		/// <summary>Gets or sets whether the public backup remains committed.</summary>
		public bool BackupCommitted { get; set; }
		/// <summary>Gets or sets whether a destination commit was rolled back.</summary>
		public bool RolledBack { get; set; }
		/// <summary>Gets or sets the weakest observed atomicity.</summary>
		public TransactionalReplacementAtomicity Atomicity { get; set; } = TransactionalReplacementAtomicity.Atomic;
		/// <summary>Gets or sets the weakest staged-file durability.</summary>
		public TransactionalReplacementDurability StagedDurability { get; set; } = TransactionalReplacementDurability.NotRequested;
		/// <summary>Gets or sets the weakest directory durability.</summary>
		public TransactionalReplacementDurability DirectoryDurability { get; set; } = TransactionalReplacementDurability.NotRequested;
	}

	private sealed class RecoveryUnit {
		/// <summary>Initializes one ordered recovery unit.</summary>
		public RecoveryUnit( string id, IReadOnlyList<StagedArtifact> items ) {
			Id = id;
			Items = items;
		}

		/// <summary>Gets the recovery-unit identity.</summary>
		public string Id { get; }
		/// <summary>Gets the artifacts in deterministic plan order.</summary>
		public IReadOnlyList<StagedArtifact> Items { get; }
	}

	private sealed class TransactionFailureException : Exception {
		/// <summary>Initializes a controlled internal transaction failure.</summary>
		public TransactionFailureException( TransactionalReplacementDiagnostic diagnostic )
			: base( diagnostic.Message, diagnostic.Exception ) {
			Diagnostic = diagnostic;
		}

		/// <summary>Gets the structured diagnostic.</summary>
		public TransactionalReplacementDiagnostic Diagnostic { get; }
	}
}
