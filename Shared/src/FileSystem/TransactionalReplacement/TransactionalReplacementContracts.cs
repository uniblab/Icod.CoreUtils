using System.Collections.ObjectModel;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Identifies the requested mutation for one transaction artifact.</summary>
public enum TransactionalReplacementAction {
	/// <summary>Write a complete staged file and publish it at the destination.</summary>
	Replace = 0,
	/// <summary>Remove the destination while retaining a recoverable staged copy until the unit commits.</summary>
	Delete = 1,
	/// <summary>Validate the destination without changing it.</summary>
	ValidateOnly = 2
}

/// <summary>Identifies one injectable lifecycle boundary in a transactional replacement.</summary>
public enum TransactionalReplacementStage {
	/// <summary>Before path containment and the initial E3 observation are validated.</summary>
	Validate = 0,
	/// <summary>Before one secure sibling temporary file is created.</summary>
	CreateTemporary = 1,
	/// <summary>Before bytes are written to a staged or recovery file.</summary>
	WriteTemporary = 2,
	/// <summary>Before a staged file is flushed.</summary>
	FlushTemporary = 3,
	/// <summary>Before an existing destination is copied into recoverable storage.</summary>
	PreserveRollback = 4,
	/// <summary>Before the E3 identity is revalidated immediately prior to commit.</summary>
	Revalidate = 5,
	/// <summary>Before one staged destination is committed.</summary>
	Commit = 6,
	/// <summary>Before requested E5 metadata is applied to a committed file.</summary>
	ApplyMetadata = 7,
	/// <summary>Before a retained backup is published.</summary>
	PublishBackup = 8,
	/// <summary>Before original metadata is restored during rollback.</summary>
	RestoreMetadata = 9,
	/// <summary>Before one committed pathname is rolled back.</summary>
	Rollback = 10,
	/// <summary>Before one temporary pathname is removed.</summary>
	Cleanup = 11,
	/// <summary>Before the containing directory is flushed after namespace mutation.</summary>
	FlushDirectory = 12
}

/// <summary>Identifies the terminal outcome of a replacement transaction.</summary>
public enum TransactionalReplacementOutcome {
	/// <summary>Every recovery unit committed and cleanup completed.</summary>
	Succeeded = 0,
	/// <summary>No recovery unit remained committed.</summary>
	FailedBeforeCommit = 1,
	/// <summary>The failing recovery unit changed one or more paths and was fully rolled back.</summary>
	FailedRolledBack = 2,
	/// <summary>One or more earlier or independent recovery units remain committed.</summary>
	FailedPartiallyCommitted = 3,
	/// <summary>At least one rollback action failed.</summary>
	FailedRollbackIncomplete = 4,
	/// <summary>Commit or rollback completed but deterministic cleanup was incomplete.</summary>
	FailedCleanupIncomplete = 5,
	/// <summary>The caller required atomic publication but the provider could not supply it.</summary>
	FailedAtomicityUnavailable = 6
}

/// <summary>Describes the atomicity supplied by one namespace mutation.</summary>
public enum TransactionalReplacementAtomicity {
	/// <summary>The provider used an operating-system primitive with same-filesystem atomic namespace semantics.</summary>
	Atomic = 0,
	/// <summary>The operation completed through a documented non-atomic fallback.</summary>
	NonAtomic = 1,
	/// <summary>The provider cannot characterize the operation's atomicity.</summary>
	Unknown = 2
}

/// <summary>Describes the durable-flush state of staged bytes or a containing directory.</summary>
public enum TransactionalReplacementDurability {
	/// <summary>The requested data-and-metadata flush succeeded.</summary>
	Durable = 0,
	/// <summary>The host reported that the requested flush primitive is unavailable.</summary>
	Unsupported = 1,
	/// <summary>The operation completed without a durability request.</summary>
	NotRequested = 2
}

/// <summary>Controls whether non-atomic publication is acceptable.</summary>
public enum TransactionalReplacementAtomicityPolicy {
	/// <summary>Fail before commit when atomic publication is unavailable.</summary>
	RequireAtomic = 0,
	/// <summary>Prefer atomic publication and emit a diagnostic if a fallback is used.</summary>
	PreferAtomic = 1,
	/// <summary>Permit a non-atomic fallback while still reporting it.</summary>
	AllowNonAtomic = 2
}

/// <summary>Controls behavior after one independent recovery unit fails.</summary>
public enum TransactionalReplacementCommitPolicy {
	/// <summary>Stop after the first failed recovery unit.</summary>
	StopAfterFailedUnit = 0,
	/// <summary>Continue committing later independent recovery units.</summary>
	ContinueIndependentUnits = 1
}

/// <summary>Identifies GNU-compatible backup-name selection.</summary>
public enum TransactionalReplacementBackupMode {
	/// <summary>Do not retain a public backup.</summary>
	None = 0,
	/// <summary>Use the destination plus the configured simple suffix.</summary>
	Simple = 1,
	/// <summary>Use the first unused numbered name of the form <c>.~N~</c>.</summary>
	Numbered = 2,
	/// <summary>Use numbered naming when numbered backups already exist; otherwise use the simple suffix.</summary>
	Existing = 3
}

/// <summary>Identifies whether internal recovery copies become public backups after success.</summary>
public enum TransactionalReplacementBackupRetention {
	/// <summary>Discard internal recovery copies after a successful unit commit.</summary>
	DiscardAfterSuccess = 0,
	/// <summary>Publish and retain a backup after a successful replacement or deletion.</summary>
	RetainAfterSuccess = 1
}

/// <summary>Identifies a stable transaction diagnostic category.</summary>
public enum TransactionalReplacementDiagnosticCode {
	/// <summary>A path failed normalization, containment, or escape validation.</summary>
	UnsafePath = 0,
	/// <summary>The current E3 observation no longer satisfies the frozen precondition.</summary>
	PreconditionFailed = 1,
	/// <summary>Secure temporary-file creation or staging failed.</summary>
	StagingFailed = 2,
	/// <summary>A requested durable flush was unsupported or failed.</summary>
	DurabilityUnavailable = 3,
	/// <summary>Atomic publication was unavailable or a fallback was used.</summary>
	AtomicityUnavailable = 4,
	/// <summary>A destination commit failed.</summary>
	CommitFailed = 5,
	/// <summary>Requested E5 metadata application failed.</summary>
	MetadataFailed = 6,
	/// <summary>Backup naming or publication failed.</summary>
	BackupFailed = 7,
	/// <summary>A rollback action failed.</summary>
	RollbackFailed = 8,
	/// <summary>A deterministic cleanup action failed.</summary>
	CleanupFailed = 9
}

/// <summary>Writes complete replacement content to a caller-owned staging stream.</summary>
/// <param name="destination">The staging stream. The delegate must not dispose it.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A value task representing the write.</returns>
public delegate ValueTask TransactionalReplacementContentWriter(
	Stream destination,
	CancellationToken cancellationToken
);

/// <summary>Configures a complete staged file before durability flushing and namespace publication.</summary>
/// <param name="stagingPath">The securely created sibling staging pathname.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A value task representing the configuration.</returns>
public delegate ValueTask TransactionalReplacementStagedFileConfigurator(
	string stagingPath,
	CancellationToken cancellationToken
);

/// <summary>Configures GNU-compatible backup naming and retention.</summary>
public sealed class TransactionalReplacementBackupPolicy {
	/// <summary>Gets a policy that retains no public backup.</summary>
	public static TransactionalReplacementBackupPolicy None { get; } = new();

	/// <summary>Gets or initializes the naming mode.</summary>
	public TransactionalReplacementBackupMode Mode { get; init; } = TransactionalReplacementBackupMode.None;
	/// <summary>Gets or initializes the simple backup suffix.</summary>
	public string SimpleSuffix { get; init; } = "~";
	/// <summary>Gets or initializes the retention behavior.</summary>
	public TransactionalReplacementBackupRetention Retention { get; init; } = TransactionalReplacementBackupRetention.DiscardAfterSuccess;
	/// <summary>Gets or initializes the maximum numbered candidate to inspect.</summary>
	public int MaximumNumberedBackup { get; init; } = 1_000_000;

	/// <summary>Validates the policy.</summary>
	/// <exception cref="ArgumentOutOfRangeException">A named enum or numbered limit is invalid.</exception>
	/// <exception cref="ArgumentException">The suffix is empty, contains a directory separator, or retention conflicts with naming.</exception>
	public void Validate() {
		if ( !Enum.IsDefined( typeof( TransactionalReplacementBackupMode ), Mode ) ) {
			throw new ArgumentOutOfRangeException( nameof( Mode ) );
		}
		if ( !Enum.IsDefined( typeof( TransactionalReplacementBackupRetention ), Retention ) ) {
			throw new ArgumentOutOfRangeException( nameof( Retention ) );
		}
		if ( 1 > MaximumNumberedBackup ) {
			throw new ArgumentOutOfRangeException( nameof( MaximumNumberedBackup ) );
		}
		if ( string.IsNullOrEmpty( SimpleSuffix )
			|| 0 <= SimpleSuffix.IndexOf( Path.DirectorySeparatorChar )
			|| 0 <= SimpleSuffix.IndexOf( Path.AltDirectorySeparatorChar ) ) {
			throw new ArgumentException( "The simple backup suffix must be a nonempty filename suffix.", nameof( SimpleSuffix ) );
		}
		if ( TransactionalReplacementBackupRetention.RetainAfterSuccess == Retention
			&& TransactionalReplacementBackupMode.None == Mode ) {
			throw new ArgumentException( "Retained backups require a backup naming mode." );
		}
	}
}

/// <summary>Controls one transactional replacement plan.</summary>
public sealed class TransactionalReplacementOptions {
	/// <summary>Gets the default options.</summary>
	public static TransactionalReplacementOptions Default { get; } = new();

	/// <summary>Gets or initializes an optional E2-resolved containment root.</summary>
	public string? ContainmentRootPath { get; init; }
	/// <summary>Gets or initializes the atomicity policy.</summary>
	public TransactionalReplacementAtomicityPolicy AtomicityPolicy { get; init; } = TransactionalReplacementAtomicityPolicy.PreferAtomic;
	/// <summary>Gets or initializes independent recovery-unit continuation policy.</summary>
	public TransactionalReplacementCommitPolicy CommitPolicy { get; init; } = TransactionalReplacementCommitPolicy.StopAfterFailedUnit;
	/// <summary>Gets or initializes backup naming and retention.</summary>
	public TransactionalReplacementBackupPolicy BackupPolicy { get; init; } = TransactionalReplacementBackupPolicy.None;
	/// <summary>Gets or initializes whether staged file flush support is mandatory.</summary>
	public bool RequireStagedDurability { get; init; } = true;
	/// <summary>Gets or initializes whether containing-directory flush support is mandatory after namespace mutation.</summary>
	public bool RequireDirectoryDurability { get; init; }

	/// <summary>Validates the options.</summary>
	/// <exception cref="ArgumentOutOfRangeException">A named enum is invalid.</exception>
	public void Validate() {
		if ( !Enum.IsDefined( typeof( TransactionalReplacementAtomicityPolicy ), AtomicityPolicy ) ) {
			throw new ArgumentOutOfRangeException( nameof( AtomicityPolicy ) );
		}
		if ( !Enum.IsDefined( typeof( TransactionalReplacementCommitPolicy ), CommitPolicy ) ) {
			throw new ArgumentOutOfRangeException( nameof( CommitPolicy ) );
		}
		ArgumentNullException.ThrowIfNull( BackupPolicy );
		BackupPolicy.Validate();
	}
}

/// <summary>Describes one immutable file artifact in a replacement transaction.</summary>
public sealed class TransactionalReplacementArtifact {
	/// <summary>Initializes one artifact.</summary>
	/// <param name="recoveryUnitId">The identity of the recovery unit that must commit or roll back together.</param>
	/// <param name="path">The destination pathname.</param>
	/// <param name="action">The requested action.</param>
	/// <param name="precondition">The E3/E4 observation that must still hold at commit.</param>
	/// <param name="contentWriter">The complete-file writer required for <see cref="TransactionalReplacementAction.Replace"/>.</param>
	/// <param name="displayName">An optional stable diagnostic name.</param>
	/// <param name="sourceMetadata">Optional authoritative E3 source metadata to apply after publication.</param>
	/// <param name="metadataPlan">Optional E5 requested-versus-required metadata plan.</param>
	/// <param name="recursiveEntry">Optional E5 recursive mutation provenance.</param>
	/// <param name="explicitBackupPath">An optional caller-selected backup pathname.</param>
	/// <param name="retainBackup">Whether this artifact retains its recoverable original as a public backup.</param>
	/// <param name="stagedFileConfigurator">An optional configurator invoked after content is complete and before it is flushed or published.</param>
	public TransactionalReplacementArtifact(
		string recoveryUnitId,
		string path,
		TransactionalReplacementAction action,
		FileSystemMutationPrecondition precondition,
		TransactionalReplacementContentWriter? contentWriter = null,
		string? displayName = null,
		FileSystemMetadata? sourceMetadata = null,
		RecursiveMetadataPreservationPlan? metadataPlan = null,
		RecursiveMutationEntry? recursiveEntry = null,
		string? explicitBackupPath = null,
		bool retainBackup = false,
		TransactionalReplacementStagedFileConfigurator? stagedFileConfigurator = null
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( recoveryUnitId );
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		if ( !Enum.IsDefined( typeof( TransactionalReplacementAction ), action ) ) {
			throw new ArgumentOutOfRangeException( nameof( action ) );
		}
		ArgumentNullException.ThrowIfNull( precondition );
		if ( PathDereferenceMode.NoFollow != precondition.DereferenceMode ) {
			throw new ArgumentException(
				"E6 artifacts require a no-follow destination precondition; callers must resolve any followed target before planning the transaction.",
				nameof( precondition )
			);
		}
		if ( TransactionalReplacementAction.Replace == action && contentWriter is null ) {
			throw new ArgumentNullException( nameof( contentWriter ) );
		}
		if ( TransactionalReplacementAction.Replace != action && contentWriter is not null ) {
			throw new ArgumentException( "Only replacement artifacts may supply a content writer.", nameof( contentWriter ) );
		}
		if ( TransactionalReplacementAction.Replace != action && stagedFileConfigurator is not null ) {
			throw new ArgumentException( "Only replacement artifacts may supply a staged-file configurator.", nameof( stagedFileConfigurator ) );
		}
		if ( metadataPlan is not null && sourceMetadata is null ) {
			throw new ArgumentException( "An E5 metadata plan requires authoritative source metadata.", nameof( metadataPlan ) );
		}
		if ( metadataPlan is not null && !metadataPlan.CanProceed ) {
			throw new ArgumentException( "The E5 metadata plan is missing required metadata.", nameof( metadataPlan ) );
		}
		RecoveryUnitId = recoveryUnitId;
		Path = path;
		Action = action;
		Precondition = precondition;
		ContentWriter = contentWriter;
		DisplayName = string.IsNullOrWhiteSpace( displayName ) ? path : displayName;
		SourceMetadata = sourceMetadata;
		MetadataPlan = metadataPlan;
		RecursiveEntry = recursiveEntry;
		ExplicitBackupPath = explicitBackupPath;
		RetainBackup = retainBackup;
		StagedFileConfigurator = stagedFileConfigurator;
	}

	/// <summary>Gets the recovery-unit identity.</summary>
	public string RecoveryUnitId { get; }
	/// <summary>Gets the destination pathname.</summary>
	public string Path { get; }
	/// <summary>Gets the requested action.</summary>
	public TransactionalReplacementAction Action { get; }
	/// <summary>Gets the frozen E3/E4 precondition.</summary>
	public FileSystemMutationPrecondition Precondition { get; }
	/// <summary>Gets the complete-file content writer.</summary>
	public TransactionalReplacementContentWriter? ContentWriter { get; }
	/// <summary>Gets the stable diagnostic display name.</summary>
	public string DisplayName { get; }
	/// <summary>Gets optional authoritative source metadata.</summary>
	public FileSystemMetadata? SourceMetadata { get; }
	/// <summary>Gets the optional E5 metadata-preservation plan.</summary>
	public RecursiveMetadataPreservationPlan? MetadataPlan { get; }
	/// <summary>Gets optional E5 traversal and mutation provenance.</summary>
	public RecursiveMutationEntry? RecursiveEntry { get; }
	/// <summary>Gets an optional caller-selected backup pathname.</summary>
	public string? ExplicitBackupPath { get; }
	/// <summary>Gets whether this artifact retains its recoverable original as a public backup.</summary>
	public bool RetainBackup { get; }
	/// <summary>Gets the optional pre-publication staged-file configurator.</summary>
	public TransactionalReplacementStagedFileConfigurator? StagedFileConfigurator { get; }

	/// <summary>Creates an artifact directly from an E5 recursive mutation entry.</summary>
	/// <param name="recoveryUnitId">The recovery-unit identity.</param>
	/// <param name="entry">The E5 entry whose mapped destination and E4 precondition are consumed.</param>
	/// <param name="contentWriter">The complete-file writer.</param>
	/// <param name="sourceMetadata">The authoritative E3 source metadata.</param>
	/// <param name="metadataPlan">The E5 metadata-preservation plan.</param>
	/// <returns>The integrated E5/E6 artifact.</returns>
	public static TransactionalReplacementArtifact FromRecursiveEntry(
		string recoveryUnitId,
		RecursiveMutationEntry entry,
		TransactionalReplacementContentWriter contentWriter,
		FileSystemMetadata sourceMetadata,
		RecursiveMetadataPreservationPlan metadataPlan
	) {
		ArgumentNullException.ThrowIfNull( entry );
		if ( string.IsNullOrEmpty( entry.DestinationPath ) ) {
			throw new ArgumentException( "The recursive entry does not have a mapped destination.", nameof( entry ) );
		}
		return new TransactionalReplacementArtifact(
			recoveryUnitId,
			entry.DestinationPath!,
			TransactionalReplacementAction.Replace,
			entry.Precondition,
			contentWriter,
			entry.TraversalEntry.DisplayPath,
			sourceMetadata,
			metadataPlan,
			entry
		);
	}
}

/// <summary>Describes one E3 destination observation.</summary>
/// <param name="Path">The observed pathname.</param>
/// <param name="Exists">Whether the pathname exists.</param>
/// <param name="Metadata">Authoritative metadata when the pathname exists.</param>
public sealed record TransactionalReplacementObservation(
	string Path,
	bool Exists,
	FileSystemMetadata? Metadata
) {
	/// <summary>Gets or initializes the ordinary-file length when it was observable.</summary>
	public long? Length { get; init; }
	/// <summary>Gets or initializes the last-write time when it was observable.</summary>
	public DateTimeOffset? ModificationTime { get; init; }
}

/// <summary>Describes provider-level transactional replacement capabilities.</summary>
/// <param name="SupportsAtomicReplaceExisting">Whether replacement of an existing sibling file is atomic.</param>
/// <param name="SupportsAtomicPublishNew">Whether publication to an absent sibling pathname is atomic and no-replace.</param>
/// <param name="SupportsAtomicDelete">Whether removal of one observed file pathname is atomic.</param>
/// <param name="SupportsDirectoryDurability">Whether containing directories can be durably flushed.</param>
public sealed record TransactionalReplacementCapabilities(
	bool SupportsAtomicReplaceExisting,
	bool SupportsAtomicPublishNew,
	bool SupportsAtomicDelete,
	bool SupportsDirectoryDurability
);

/// <summary>Describes one namespace commit operation.</summary>
/// <param name="Atomicity">The supplied atomicity.</param>
/// <param name="Message">An optional capability or fallback diagnostic.</param>
public sealed record TransactionalReplacementCommitResult(
	TransactionalReplacementAtomicity Atomicity,
	string? Message = null
);

/// <summary>Describes one durable-flush attempt.</summary>
/// <param name="Durability">The resulting durability state.</param>
/// <param name="Message">An optional unsupported or degraded diagnostic.</param>
public sealed record TransactionalReplacementDurabilityResult(
	TransactionalReplacementDurability Durability,
	string? Message = null
);

/// <summary>Describes one controlled transaction diagnostic.</summary>
/// <param name="Code">The stable category.</param>
/// <param name="Stage">The lifecycle stage.</param>
/// <param name="RecoveryUnitId">The affected recovery unit.</param>
/// <param name="Path">The affected pathname.</param>
/// <param name="Message">The consumer-independent message.</param>
/// <param name="Exception">The optional underlying exception.</param>
public sealed record TransactionalReplacementDiagnostic(
	TransactionalReplacementDiagnosticCode Code,
	TransactionalReplacementStage Stage,
	string RecoveryUnitId,
	string Path,
	string Message,
	Exception? Exception = null
);

/// <summary>Reports the terminal state of one planned artifact.</summary>
/// <param name="RecoveryUnitId">The recovery-unit identity.</param>
/// <param name="Path">The destination pathname.</param>
/// <param name="Committed">Whether the destination remains committed.</param>
/// <param name="RolledBack">Whether a committed change was recovered.</param>
/// <param name="BackupPath">The retained backup pathname, when any.</param>
/// <param name="Atomicity">The weakest atomicity observed for the artifact.</param>
/// <param name="StagedDurability">The staged-file durability state.</param>
/// <param name="DirectoryDurability">The containing-directory durability state.</param>
public sealed record TransactionalReplacementArtifactReport(
	string RecoveryUnitId,
	string Path,
	bool Committed,
	bool RolledBack,
	string? BackupPath,
	TransactionalReplacementAtomicity Atomicity,
	TransactionalReplacementDurability StagedDurability,
	TransactionalReplacementDurability DirectoryDurability
);

/// <summary>Contains the complete outcome of one replacement transaction.</summary>
public sealed class TransactionalReplacementResult {
	/// <summary>Initializes a result.</summary>
	/// <param name="outcome">The terminal outcome.</param>
	/// <param name="diagnostics">Controlled diagnostics in observation order.</param>
	/// <param name="artifactReports">Artifact reports in plan order.</param>
	/// <param name="committedRecoveryUnitIds">Recovery units that remain committed.</param>
	/// <param name="rolledBackRecoveryUnitIds">Recovery units fully recovered after partial commit.</param>
	public TransactionalReplacementResult(
		TransactionalReplacementOutcome outcome,
		IReadOnlyList<TransactionalReplacementDiagnostic> diagnostics,
		IReadOnlyList<TransactionalReplacementArtifactReport> artifactReports,
		IReadOnlyList<string> committedRecoveryUnitIds,
		IReadOnlyList<string> rolledBackRecoveryUnitIds
	) {
		if ( !Enum.IsDefined( typeof( TransactionalReplacementOutcome ), outcome ) ) {
			throw new ArgumentOutOfRangeException( nameof( outcome ) );
		}
		ArgumentNullException.ThrowIfNull( diagnostics );
		ArgumentNullException.ThrowIfNull( artifactReports );
		ArgumentNullException.ThrowIfNull( committedRecoveryUnitIds );
		ArgumentNullException.ThrowIfNull( rolledBackRecoveryUnitIds );
		Outcome = outcome;
		Diagnostics = new ReadOnlyCollection<TransactionalReplacementDiagnostic>( diagnostics.ToArray() );
		ArtifactReports = new ReadOnlyCollection<TransactionalReplacementArtifactReport>( artifactReports.ToArray() );
		CommittedRecoveryUnitIds = new ReadOnlyCollection<string>( committedRecoveryUnitIds.ToArray() );
		RolledBackRecoveryUnitIds = new ReadOnlyCollection<string>( rolledBackRecoveryUnitIds.ToArray() );
	}

	/// <summary>Gets whether every recovery unit committed and cleaned up.</summary>
	public bool Succeeded => TransactionalReplacementOutcome.Succeeded == Outcome;
	/// <summary>Gets the terminal outcome.</summary>
	public TransactionalReplacementOutcome Outcome { get; }
	/// <summary>Gets controlled diagnostics.</summary>
	public IReadOnlyList<TransactionalReplacementDiagnostic> Diagnostics { get; }
	/// <summary>Gets artifact reports in plan order.</summary>
	public IReadOnlyList<TransactionalReplacementArtifactReport> ArtifactReports { get; }
	/// <summary>Gets recovery units that remain committed.</summary>
	public IReadOnlyList<string> CommittedRecoveryUnitIds { get; }
	/// <summary>Gets recovery units that were completely rolled back.</summary>
	public IReadOnlyList<string> RolledBackRecoveryUnitIds { get; }
}

/// <summary>Injects deterministic failures at every E6 lifecycle boundary.</summary>
public interface ITransactionalReplacementFailureInjector {
	/// <summary>Observes one lifecycle stage and may throw a deterministic test exception.</summary>
	/// <param name="stage">The current stage.</param>
	/// <param name="artifact">The affected artifact.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A value task representing the observation.</returns>
	ValueTask OnStageAsync(
		TransactionalReplacementStage stage,
		TransactionalReplacementArtifact artifact,
		CancellationToken cancellationToken = default
	);
}

/// <summary>Provides the no-op failure injector used by production callers.</summary>
public sealed class NullTransactionalReplacementFailureInjector : ITransactionalReplacementFailureInjector {
	private NullTransactionalReplacementFailureInjector() {
	}

	/// <summary>Gets the shared no-op injector.</summary>
	public static NullTransactionalReplacementFailureInjector Instance { get; } = new();

	/// <inheritdoc/>
	public ValueTask OnStageAsync(
		TransactionalReplacementStage stage,
		TransactionalReplacementArtifact artifact,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}
