namespace Icod.Patch;

using System.Collections.ObjectModel;
using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Icod.CoreUtils.Shared.Temporary;
using Icod.Path;

/// <summary>Contains the E3 state observed for one potential artifact destination.</summary>
internal sealed class PatchFileObservation {
	/// <summary>Initializes a missing-path observation.</summary>
	public PatchFileObservation( string path ) {
		this.Path = path ?? throw new ArgumentNullException( nameof( path ) );
	}

	/// <summary>Initializes an existing-path observation.</summary>
	public PatchFileObservation( string path, FileSystemMetadata metadata ) {
		this.Path = path ?? throw new ArgumentNullException( nameof( path ) );
		this.Metadata = metadata ?? throw new ArgumentNullException( nameof( metadata ) );
	}

	/// <summary>Gets the observed path.</summary>
	public string Path { get; }
	/// <summary>Gets whether the pathname existed.</summary>
	public bool Exists => null != this.Metadata;
	/// <summary>Gets the authoritative E3 metadata.</summary>
	public FileSystemMetadata? Metadata { get; }
	/// <summary>Gets the effective observed entry kind.</summary>
	public FileSystemEntryKind? Kind => this.Metadata?.Kind;
	/// <summary>Gets the dereference policy represented by the observation.</summary>
	public PathDereferenceMode DereferenceMode => this.Metadata?.WasDereferenced == true
		? PathDereferenceMode.FollowEligiblePathIndirection
		: PathDereferenceMode.NoFollow;
	/// <summary>Gets the portable mode bits when available.</summary>
	public int? Mode => this.Metadata?.Mode.IsAvailable == true
		? checked( (int)(this.Metadata.Mode.GetRequiredValue() & 0x0fffU) )
		: null;
	/// <summary>Gets the access time when available.</summary>
	public DateTimeOffset? AccessTime => this.Metadata?.AccessTime.IsAvailable == true
		? this.Metadata.AccessTime.GetRequiredValue()
		: null;
	/// <summary>Gets the modification time when available.</summary>
	public DateTimeOffset? ModificationTime => this.Metadata?.ModificationTime.IsAvailable == true
		? this.Metadata.ModificationTime.GetRequiredValue()
		: null;
	/// <summary>Gets the numeric owner identifier when available.</summary>
	public uint? UserId => this.Metadata?.UserId.IsAvailable == true
		? this.Metadata.UserId.GetRequiredValue()
		: null;
	/// <summary>Gets the numeric group identifier when available.</summary>
	public uint? GroupId => this.Metadata?.GroupId.IsAvailable == true
		? this.Metadata.GroupId.GetRequiredValue()
		: null;
}

/// <summary>Identifies a testable transaction lifecycle boundary.</summary>
internal enum PatchTransactionStage {
	/// <summary>Before observing and validating destinations.</summary>
	Validate,
	/// <summary>Before creating a secure sibling temporary file.</summary>
	CreateTemporary,
	/// <summary>Before writing staged artifact bytes.</summary>
	WriteTemporary,
	/// <summary>Before flushing staged artifact bytes to stable storage.</summary>
	FlushTemporary,
	/// <summary>Before preserving a rollback copy.</summary>
	PreserveRollback,
	/// <summary>Before revalidating one destination immediately prior to commit.</summary>
	Revalidate,
	/// <summary>Before committing one staged artifact.</summary>
	Commit,
	/// <summary>Before applying mode or timestamp metadata.</summary>
	ApplyMetadata,
	/// <summary>Before publishing a retained backup.</summary>
	PublishBackup,
	/// <summary>Before restoring metadata during rollback.</summary>
	RestoreMetadata,
	/// <summary>Before rolling back a committed artifact.</summary>
	Rollback,
	/// <summary>Before deleting temporary files.</summary>
	Cleanup,
	/// <summary>Before flushing a containing directory.</summary>
	FlushDirectory
}

/// <summary>Injects deterministic failures into the Patch-facing E6 transaction boundary.</summary>
internal interface IPatchTransactionFailureInjector {
	/// <summary>Observes one lifecycle stage and may throw a test exception.</summary>
	ValueTask OnStageAsync(
		PatchTransactionStage stage,
		PatchArtifact artifact,
		CancellationToken cancellationToken = default
	);
}

/// <summary>Represents a transaction without injected failures.</summary>
internal sealed class NullPatchTransactionFailureInjector : IPatchTransactionFailureInjector {
	private NullPatchTransactionFailureInjector() {
	}

	/// <summary>Gets the shared no-op injector.</summary>
	public static NullPatchTransactionFailureInjector Instance { get; } = new();

	/// <inheritdoc/>
	public ValueTask OnStageAsync(
		PatchTransactionStage stage,
		PatchArtifact artifact,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

/// <summary>Identifies the terminal outcome of one Patch transaction.</summary>
internal enum PatchTransactionOutcome {
	/// <summary>Every transaction unit committed and cleaned up.</summary>
	Succeeded,
	/// <summary>No transaction unit committed.</summary>
	FailedBeforeCommit,
	/// <summary>The failing transaction unit was fully rolled back.</summary>
	FailedRolledBack,
	/// <summary>Earlier transaction units committed before a later unit failed and was rolled back.</summary>
	FailedPartiallyCommitted,
	/// <summary>Rollback did not completely recover the failing transaction unit.</summary>
	FailedRollbackIncomplete,
	/// <summary>Commit completed but deterministic temporary cleanup was incomplete.</summary>
	FailedCleanupIncomplete,
	/// <summary>Atomic publication was mandatory but unavailable.</summary>
	FailedAtomicityUnavailable
}

/// <summary>Contains the result of one Patch artifact transaction.</summary>
internal sealed class PatchTransactionResult {
	/// <summary>Initializes a legacy success-or-failure transaction result.</summary>
	public PatchTransactionResult(
		bool succeeded,
		IReadOnlyList<string> diagnostics,
		Exception? exception = null
	) : this(
		succeeded ? PatchTransactionOutcome.Succeeded : PatchTransactionOutcome.FailedBeforeCommit,
		diagnostics,
		exception: exception
	) {
	}

	/// <summary>Initializes a detailed transaction result.</summary>
	public PatchTransactionResult(
		PatchTransactionOutcome outcome,
		IReadOnlyList<string> diagnostics,
		IReadOnlyList<string>? committedUnitIds = null,
		IReadOnlyList<string>? rolledBackUnitIds = null,
		Exception? exception = null
	) {
		ArgumentNullException.ThrowIfNull( diagnostics );
		this.Outcome = outcome;
		this.Diagnostics = new ReadOnlyCollection<string>( diagnostics.ToArray() );
		this.CommittedUnitIds = new ReadOnlyCollection<string>(
			(committedUnitIds ?? Array.Empty<string>()).ToArray()
		);
		this.RolledBackUnitIds = new ReadOnlyCollection<string>(
			(rolledBackUnitIds ?? Array.Empty<string>()).ToArray()
		);
		this.Exception = exception;
	}

	/// <summary>Gets whether every requested transaction unit committed and cleaned up successfully.</summary>
	public bool Succeeded => PatchTransactionOutcome.Succeeded == this.Outcome;
	/// <summary>Gets the terminal transaction outcome.</summary>
	public PatchTransactionOutcome Outcome { get; }
	/// <summary>Gets deterministic controlled diagnostics.</summary>
	public IReadOnlyList<string> Diagnostics { get; }
	/// <summary>Gets transaction units that remain committed.</summary>
	public IReadOnlyList<string> CommittedUnitIds { get; }
	/// <summary>Gets transaction units recovered after a failed commit attempt.</summary>
	public IReadOnlyList<string> RolledBackUnitIds { get; }
	/// <summary>Gets whether at least one earlier patch-file unit remains committed.</summary>
	public bool HasPartialCommit => 0 < this.CommittedUnitIds.Count && !this.Succeeded;
	/// <summary>Gets the underlying operational exception.</summary>
	public Exception? Exception { get; }
}

/// <summary>Models one staged Patch transaction.</summary>
internal interface IPatchTransaction : IAsyncDisposable {
	/// <summary>Stages every artifact before any destination is changed.</summary>
	Task StageAsync( CancellationToken cancellationToken = default );

	/// <summary>Commits staged artifacts and rolls back completed changes after a later failure.</summary>
	Task<PatchTransactionResult> CommitAsync( CancellationToken cancellationToken = default );
}

/// <summary>Provides the Patch-facing E3/E4 filesystem boundary.</summary>
internal interface IPatchFileSystem {
	/// <summary>Gets the frozen Patch-facing Completion Gate E6 requirements.</summary>
	PatchE6TransactionContract TransactionContract => PatchE6TransactionContract.Current;

	/// <summary>Gets the Patch-facing transaction capability profile.</summary>
	PatchTransactionCapabilities TransactionCapabilities => PatchTransactionCapabilities.ProvisionalHost;

	/// <summary>Observes one artifact path using explicit terminal-indirection policy.</summary>
	ValueTask<PatchFileObservation> ObserveAsync(
		string path,
		bool followPathIndirection,
		CancellationToken cancellationToken = default
	);

	/// <summary>Resolves one user-selected artifact pathname under explicit final-indirection policy.</summary>
	ValueTask<string> ResolveArtifactPathAsync(
		string path,
		string workingDirectory,
		bool followPathIndirection,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates a transaction over an immutable artifact plan.</summary>
	ValueTask<IPatchTransaction> CreateTransactionAsync(
		PatchArtifactPlan plan,
		IPatchTransactionFailureInjector? failureInjector = null,
		CancellationToken cancellationToken = default
	);
}

/// <summary>Uses Completion Gates E3 and E4 for Patch observations, mutation, modes, and timestamps.</summary>
internal sealed class SystemPatchFileSystem : IPatchFileSystem {
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly ITransactionalReplacementFileSystem replacementFileSystem;
	private readonly CanonicalPathResolver pathResolver;

	/// <inheritdoc/>
	public PatchE6TransactionContract TransactionContract => PatchE6TransactionContract.Current;

	/// <inheritdoc/>
	public PatchTransactionCapabilities TransactionCapabilities => PatchTransactionCapabilities.ProvisionalHost;

	/// <summary>Initializes the host adapter.</summary>
	public SystemPatchFileSystem()
		: this( SystemFileSystemMetadataProvider.Instance, SystemFileSystemMutationProvider.Instance ) {
	}

	/// <summary>Initializes an adapter over injected E3 and E4 providers.</summary>
	public SystemPatchFileSystem(
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider
	) : this(
		metadataProvider,
		mutationProvider,
		new SystemTransactionalReplacementFileSystem(
			metadataProvider,
			mutationProvider,
			SystemFileSystemOperations.Instance,
			SecureTemporaryObjectCreator.System
		)
	) {
	}

	/// <summary>Initializes an adapter over injected E3, E4, and E6 providers.</summary>
	public SystemPatchFileSystem(
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		ITransactionalReplacementFileSystem replacementFileSystem
	) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		this.replacementFileSystem = replacementFileSystem ?? throw new ArgumentNullException( nameof( replacementFileSystem ) );
		this.pathResolver = new CanonicalPathResolver( SystemCanonicalPathFileSystemProvider.Instance );
	}

	/// <inheritdoc/>
	public async ValueTask<PatchFileObservation> ObserveAsync(
		string path,
		bool followPathIndirection,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		try {
			var metadata = await this.metadataProvider.GetMetadataAsync(
				path,
				followPathIndirection
					? PathDereferenceMode.FollowEligiblePathIndirection
					: PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			return new PatchFileObservation( path, metadata );
		} catch ( FileNotFoundException ) {
			return new PatchFileObservation( path );
		} catch ( DirectoryNotFoundException ) {
			return new PatchFileObservation( path );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<string> ResolveArtifactPathAsync(
		string path,
		string workingDirectory,
		bool followPathIndirection,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		ArgumentException.ThrowIfNullOrEmpty( workingDirectory );
		if ( 0 <= path.IndexOfAny( new[] { '\r', '\n' } ) ) {
			throw new PatchApplicationException( "an artifact pathname cannot contain a newline" );
		}
		var lexicalRoot = this.pathResolver.NormalizeLexically(
			workingDirectory,
			Directory.GetCurrentDirectory()
		);
		if ( !lexicalRoot.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( lexicalRoot.Failure! ) );
		}
		var physicalRoot = await this.pathResolver.ResolvePhysicalAsync(
			lexicalRoot.Path!,
			new CanonicalPathResolutionOptions {
				BasePath = Directory.GetCurrentDirectory(),
				MissingComponentPolicy = MissingPathComponentPolicy.RequireExisting,
				FollowSymbolicLinks = true,
				RequireFinalDirectory = true
			},
			cancellationToken
		).ConfigureAwait( false );
		if ( !physicalRoot.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( physicalRoot.Failure! ) );
		}
		var lexical = this.pathResolver.NormalizeLexically( path, lexicalRoot.Path! );
		if ( !lexical.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( lexical.Failure! ) );
		}
		EnsureContained( this.pathResolver, lexicalRoot.Path!, lexical.Path!, "artifact pathname" );
		if ( !followPathIndirection ) {
			var inspection = await this.pathResolver.InspectLinkAsync(
				lexical.Path!,
				lexicalRoot.Path!,
				cancellationToken
			).ConfigureAwait( false );
			if ( inspection.Succeeded && (inspection.IsSymbolicLink || inspection.IsReparsePoint) ) {
				throw new PatchApplicationException(
					string.Concat( lexical.Path, ": artifact pathname is a link or reparse point; use --follow-symlinks to follow it" )
				);
			}
			if ( !inspection.Succeeded
				&& inspection.Failure!.Code is not CanonicalPathFailureCode.NotFound ) {
				throw new PatchApplicationException( FormatPathFailure( inspection.Failure ) );
			}
		}
		var physical = await this.pathResolver.ResolvePhysicalAsync(
			lexical.Path!,
			new CanonicalPathResolutionOptions {
				BasePath = physicalRoot.Path!,
				MissingComponentPolicy = MissingPathComponentPolicy.AllowMissingSuffix,
				FollowSymbolicLinks = true,
				FollowFinalSymbolicLink = followPathIndirection
			},
			cancellationToken
		).ConfigureAwait( false );
		if ( !physical.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( physical.Failure! ) );
		}
		EnsureContained( this.pathResolver, physicalRoot.Path!, physical.Path!, "resolved artifact pathname" );
		return physical.Path!;
	}

	private static void EnsureContained(
		CanonicalPathResolver resolver,
		string workingDirectory,
		string path,
		string description
	) {
		var containment = resolver.EvaluateContainment( workingDirectory, path );
		if ( !containment.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( containment.Failure! ) );
		}
		if ( !containment.IsContained ) {
			throw new PatchApplicationException(
				string.Concat( path, ": ", description, " escapes the patch working directory" )
			);
		}
	}

	private static string FormatPathFailure( CanonicalPathFailure failure ) {
		return string.Concat( failure.Path, ": ", failure.Message );
	}

	/// <inheritdoc/>
	public ValueTask<IPatchTransaction> CreateTransactionAsync(
		PatchArtifactPlan plan,
		IPatchTransactionFailureInjector? failureInjector = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( plan );
		cancellationToken.ThrowIfCancellationRequested();
		IPatchTransaction transaction = new PatchE6Transaction(
			plan,
			this.replacementFileSystem,
			failureInjector ?? NullPatchTransactionFailureInjector.Instance
		);
		return ValueTask.FromResult( transaction );
	}
}

/// <summary>Retains the unreachable P9 implementation for deliberate removal during Phase P11B.</summary>
internal sealed class SystemPatchTransaction : IPatchTransaction {
	private sealed class StagedArtifact {
		/// <summary>Initializes staged state.</summary>
		public StagedArtifact( PatchArtifact artifact ) {
			this.Artifact = artifact;
		}
		/// <summary>Gets the source artifact.</summary>
		public PatchArtifact Artifact { get; }
		/// <summary>Gets or sets the complete-content temporary path.</summary>
		public string? TemporaryPath { get; set; }
		/// <summary>Gets or sets the pre-commit rollback path.</summary>
		public string? RollbackPath { get; set; }
		/// <summary>Gets or sets whether the artifact was committed.</summary>
		public bool Committed { get; set; }
	}

	private const int BufferSize = 64 * 1024;
	private readonly PatchArtifactPlan plan;
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly IFileSystemMutationProvider mutationProvider;
	private readonly IPatchTransactionFailureInjector failureInjector;
	private readonly List<StagedArtifact> staged = new();
	private bool stageCompleted;
	private bool commitAttempted;
	private bool disposed;

	/// <summary>Initializes the host transaction.</summary>
	public SystemPatchTransaction(
		PatchArtifactPlan plan,
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider,
		IPatchTransactionFailureInjector failureInjector
	) {
		this.plan = plan ?? throw new ArgumentNullException( nameof( plan ) );
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.mutationProvider = mutationProvider ?? throw new ArgumentNullException( nameof( mutationProvider ) );
		this.failureInjector = failureInjector ?? throw new ArgumentNullException( nameof( failureInjector ) );
	}

	/// <inheritdoc/>
	public async Task StageAsync( CancellationToken cancellationToken = default ) {
		ObjectDisposedException.ThrowIf( this.disposed, this );
		if ( this.stageCompleted ) {
			return;
		}
		try {
			foreach ( var artifact in this.plan.Artifacts ) {
				cancellationToken.ThrowIfCancellationRequested();
				if ( PatchArtifactAction.WriteStandardOutput == artifact.Action ) {
					continue;
				}
				await this.failureInjector.OnStageAsync(
					PatchTransactionStage.Validate,
					artifact,
					cancellationToken
				).ConfigureAwait( false );
				await this.ValidateObservationAsync( artifact, cancellationToken ).ConfigureAwait( false );
				var item = new StagedArtifact( artifact );
				this.staged.Add( item );
				if ( PatchArtifactAction.ValidateOnly == artifact.Action ) {
					continue;
				}
				if ( artifact.ExpectedDestination.Exists ) {
					await this.failureInjector.OnStageAsync(
						PatchTransactionStage.PreserveRollback,
						artifact,
						cancellationToken
					).ConfigureAwait( false );
					item.RollbackPath = await this.StageExistingFileAsync(
						artifact.Path,
						artifact,
						"rollback",
						cancellationToken
					).ConfigureAwait( false );
				}
				if ( PatchArtifactAction.Write == artifact.Action ) {
					item.TemporaryPath = await this.StageContentAsync(
						artifact,
						cancellationToken
					).ConfigureAwait( false );
				}
			}
			this.stageCompleted = true;
		} catch {
			await this.CleanupAsync(
				this.staged,
				new List<string>(),
				injectFailure: false
			).ConfigureAwait( false );
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<PatchTransactionResult> CommitAsync( CancellationToken cancellationToken = default ) {
		ObjectDisposedException.ThrowIf( this.disposed, this );
		if ( this.commitAttempted ) {
			throw new InvalidOperationException( "the Patch transaction has already been committed" );
		}
		this.commitAttempted = true;
		var diagnostics = new List<string>();
		var committedUnits = new List<string>();
		var rolledBackUnits = new List<string>();
		try {
			await this.StageAsync( cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) {
			diagnostics.Add( exception.Message );
			return new PatchTransactionResult(
				PatchTransactionOutcome.FailedBeforeCommit,
				diagnostics,
				exception: exception
			);
		}

		foreach ( var unit in this.GetTransactionUnits() ) {
			try {
				await this.CommitUnitAsync( unit.Items, cancellationToken ).ConfigureAwait( false );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				await this.RollbackUnitAsync(
					unit.Id,
					unit.Items,
					diagnostics,
					rolledBackUnits
				).ConfigureAwait( false );
				await this.CleanupAsync(
					this.staged,
					diagnostics,
					injectFailure: false
				).ConfigureAwait( false );
				throw;
			} catch ( Exception exception ) {
				diagnostics.Add( exception.Message );
				var unitHadCommittedArtifact = unit.Items.Any( item => item.Committed );
				var rollbackSucceeded = await this.RollbackUnitAsync(
					unit.Id,
					unit.Items,
					diagnostics,
					rolledBackUnits
				).ConfigureAwait( false );
				var cleanupSucceeded = await this.CleanupAsync(
					this.staged,
					diagnostics,
					injectFailure: false
				).ConfigureAwait( false );
				var outcome = !rollbackSucceeded
					? PatchTransactionOutcome.FailedRollbackIncomplete
					: !cleanupSucceeded
						? PatchTransactionOutcome.FailedCleanupIncomplete
						: 0 < committedUnits.Count
							? PatchTransactionOutcome.FailedPartiallyCommitted
							: unitHadCommittedArtifact
								? PatchTransactionOutcome.FailedRolledBack
								: PatchTransactionOutcome.FailedBeforeCommit;
				if ( 0 < committedUnits.Count ) {
					diagnostics.Add(
						string.Concat(
							committedUnits.Count.ToString( System.Globalization.CultureInfo.InvariantCulture ),
							" earlier patch-file transaction unit(s) remain committed"
						)
					);
				}
				return new PatchTransactionResult(
					outcome,
					diagnostics,
					committedUnits,
					rolledBackUnits,
					exception
				);
			}

			committedUnits.Add( unit.Id );
			var unitCleanupSucceeded = await this.CleanupAsync(
				unit.Items,
				diagnostics,
				injectFailure: true
			).ConfigureAwait( false );
			if ( !unitCleanupSucceeded ) {
				await this.CleanupAsync(
					this.staged,
					diagnostics,
					injectFailure: false
				).ConfigureAwait( false );
				return new PatchTransactionResult(
					PatchTransactionOutcome.FailedCleanupIncomplete,
					diagnostics,
					committedUnits,
					rolledBackUnits
				);
			}
		}

		return new PatchTransactionResult(
			PatchTransactionOutcome.Succeeded,
			diagnostics,
			committedUnits,
			rolledBackUnits
		);
	}

	private async Task CommitUnitAsync(
		IReadOnlyList<StagedArtifact> items,
		CancellationToken cancellationToken
	) {
		foreach ( var item in items ) {
			cancellationToken.ThrowIfCancellationRequested();
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.Revalidate,
				item.Artifact,
				cancellationToken
			).ConfigureAwait( false );
			await this.ValidateObservationAsync( item.Artifact, cancellationToken ).ConfigureAwait( false );
			if ( PatchArtifactAction.ValidateOnly == item.Artifact.Action ) {
				continue;
			}
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.Commit,
				item.Artifact,
				cancellationToken
			).ConfigureAwait( false );
			if ( PatchArtifactAction.Delete == item.Artifact.Action ) {
				await this.DeleteArtifactAsync( item.Artifact, cancellationToken ).ConfigureAwait( false );
			} else {
				CommitTemporary( item.TemporaryPath!, item.Artifact.Path );
				item.TemporaryPath = null;
			}
			item.Committed = true;
			if ( PatchArtifactAction.Delete != item.Artifact.Action ) {
				await this.ApplyMetadataAsync( item.Artifact, cancellationToken ).ConfigureAwait( false );
			}
		}
	}

	private IReadOnlyList<(string Id, IReadOnlyList<StagedArtifact> Items)> GetTransactionUnits() {
		var comparer = OperatingSystem.IsWindows()
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;
		return this.staged
			.GroupBy( item => item.Artifact.TransactionUnitId, comparer )
			.Select(
				group => (
					Id: group.Key,
					Items: (IReadOnlyList<StagedArtifact>)group.ToArray()
				)
			)
			.ToArray();
	}

	private async Task<string> StageContentAsync(
		PatchArtifact artifact,
		CancellationToken cancellationToken
	) {
		var temporaryPath = await this.CreateTemporaryAsync(
			artifact.Path,
			artifact,
			"stage",
			cancellationToken
		).ConfigureAwait( false );
		try {
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.WriteTemporary,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await using var output = OpenTemporaryForWrite( temporaryPath );
			await artifact.Content!.WriteToAsync( output, cancellationToken ).ConfigureAwait( false );
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.FlushTemporary,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await output.FlushAsync( cancellationToken ).ConfigureAwait( false );
			output.Flush( flushToDisk: true );
			return temporaryPath;
		} catch {
			TryDelete( temporaryPath );
			throw;
		}
	}

	private async Task<string> StageExistingFileAsync(
		string sourcePath,
		PatchArtifact artifact,
		string purpose,
		CancellationToken cancellationToken
	) {
		var temporaryPath = await this.CreateTemporaryAsync(
			sourcePath,
			artifact,
			purpose,
			cancellationToken
		).ConfigureAwait( false );
		try {
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.WriteTemporary,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await using var input = OpenExistingForRead( sourcePath );
			await using var output = OpenTemporaryForWrite( temporaryPath );
			await input.CopyToAsync( output, BufferSize, cancellationToken ).ConfigureAwait( false );
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.FlushTemporary,
				artifact,
				cancellationToken
			).ConfigureAwait( false );
			await output.FlushAsync( cancellationToken ).ConfigureAwait( false );
			output.Flush( flushToDisk: true );
			return temporaryPath;
		} catch {
			TryDelete( temporaryPath );
			throw;
		}
	}

	private async Task<string> CreateTemporaryAsync(
		string destinationPath,
		PatchArtifact artifact,
		string purpose,
		CancellationToken cancellationToken
	) {
		await this.failureInjector.OnStageAsync(
			PatchTransactionStage.CreateTemporary,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
		var directory = System.IO.Path.GetDirectoryName( destinationPath );
		if ( string.IsNullOrEmpty( directory ) ) {
			directory = Directory.GetCurrentDirectory();
		}
		var basename = System.IO.Path.GetFileName( destinationPath );
		for ( var attempt = 0; attempt < 128; attempt++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			var candidate = System.IO.Path.Combine(
				directory,
				string.Concat(
					".",
					basename,
					".patch-",
					purpose,
					"-",
					Convert.ToHexString( System.Security.Cryptography.RandomNumberGenerator.GetBytes( 12 ) ).ToLowerInvariant()
				)
			);
			var result = await this.mutationProvider.CreateFileAsync(
				candidate,
				new PosixFileMode( 0x0180 ),
				FileCreationMask.None,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken
			).ConfigureAwait( false );
			if ( result.Succeeded ) {
				return candidate;
			}
			if ( FileSystemMutationErrorCode.AlreadyExists == result.ErrorCode ) {
				continue;
			}
			throw new PatchApplicationException(
				result.Message ?? string.Concat( "cannot create temporary file for ", artifact.DisplayName )
			);
		}
		throw new PatchApplicationException( string.Concat( "cannot allocate temporary file for ", artifact.DisplayName ) );
	}

	private async Task ValidateObservationAsync(
		PatchArtifact artifact,
		CancellationToken cancellationToken
	) {
		var expected = artifact.ExpectedDestination;
		try {
			var current = await this.metadataProvider.GetMetadataAsync(
				artifact.Path,
				expected.DereferenceMode,
				cancellationToken
			).ConfigureAwait( false );
			if ( !expected.Exists ) {
				throw new PatchApplicationException(
					string.Concat( artifact.DisplayName, ": destination appeared after planning" )
				);
			}
			var prior = expected.Metadata!;
			if ( prior.Kind != current.Kind
				|| (prior.EntryIdentity.IsAvailable && !prior.EntryIdentity.Equals( current.EntryIdentity ))
				|| MetadataValueChanged( prior.Size, current.Size )
				|| MetadataValueChanged( prior.ModificationTime, current.ModificationTime )
				|| MetadataValueChanged( prior.ChangeTime, current.ChangeTime ) ) {
				throw new PatchApplicationException(
					string.Concat( artifact.DisplayName, ": destination changed after planning" )
				);
			}
		} catch ( FileNotFoundException ) {
			if ( expected.Exists ) {
				throw new PatchApplicationException(
					string.Concat( artifact.DisplayName, ": destination disappeared after planning" )
				);
			}
		} catch ( DirectoryNotFoundException ) {
			if ( expected.Exists ) {
				throw new PatchApplicationException(
					string.Concat( artifact.DisplayName, ": destination parent disappeared after planning" )
				);
			}
		}
	}

	private static bool MetadataValueChanged<T>(
		FileSystemMetadataValue<T> expected,
		FileSystemMetadataValue<T> current
	) {
		return expected.IsAvailable
			&& current.IsAvailable
			&& !EqualityComparer<T>.Default.Equals(
				expected.GetRequiredValue(),
				current.GetRequiredValue()
			);
	}

	private async Task DeleteArtifactAsync(
		PatchArtifact artifact,
		CancellationToken cancellationToken
	) {
		if ( !artifact.ExpectedDestination.Exists ) {
			return;
		}
		var metadata = artifact.ExpectedDestination.Metadata!;
		var precondition = FileSystemMutationPrecondition.FromObservation(
			metadata.Kind,
			metadata.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		var result = await this.mutationProvider.RemoveFileAsync(
			artifact.Path,
			precondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( !result.Succeeded ) {
			throw new PatchApplicationException(
				result.Message ?? string.Concat( "cannot remove ", artifact.DisplayName )
			);
		}
	}

	private async Task ApplyMetadataAsync(
		PatchArtifact artifact,
		CancellationToken cancellationToken
	) {
		await this.failureInjector.OnStageAsync(
			PatchTransactionStage.ApplyMetadata,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
		var current = await this.metadataProvider.GetMetadataAsync(
			artifact.Path,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		var metadataPrecondition = FileSystemMutationPrecondition.FromObservation(
			current.Kind,
			current.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		await this.ApplyOwnershipAsync(
			artifact,
			current,
			artifact.Metadata.UserId,
			artifact.Metadata.GroupId,
			metadataPrecondition,
			"cannot set ownership of ",
			cancellationToken
		).ConfigureAwait( false );
		if ( artifact.Metadata.Mode.HasValue && this.mutationProvider.Capabilities.CanSetModes ) {
			var result = await this.mutationProvider.SetModeAsync(
				artifact.Path,
				new PosixFileMode( artifact.Metadata.Mode.Value ),
				PathDereferenceMode.NoFollow,
				metadataPrecondition,
				cancellationToken
			).ConfigureAwait( false );
			if ( !result.Succeeded ) {
				throw new PatchApplicationException(
					result.Message ?? string.Concat( "cannot set mode of ", artifact.DisplayName )
				);
			}
		}
		if ( artifact.Metadata.AccessTime.HasValue || artifact.Metadata.ModificationTime.HasValue ) {
			var request = new FileTimestampMutationRequest {
				AccessTime = artifact.Metadata.AccessTime.HasValue
					? FileTimestampChange.At( artifact.Metadata.AccessTime.Value )
					: FileTimestampChange.Unchanged,
				ModificationTime = artifact.Metadata.ModificationTime.HasValue
					? FileTimestampChange.At( artifact.Metadata.ModificationTime.Value )
					: FileTimestampChange.Unchanged
			};
			var result = await this.metadataProvider.SetTimestampsAsync(
				artifact.Path,
				request,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( !result.Succeeded ) {
				throw new PatchApplicationException(
					result.Message ?? string.Concat( "cannot set timestamps of ", artifact.DisplayName )
				);
			}
		}
	}

	private async Task<bool> RollbackUnitAsync(
		string unitId,
		IReadOnlyList<StagedArtifact> items,
		ICollection<string> diagnostics,
		ICollection<string> rolledBackUnits
	) {
		var succeeded = true;
		var recoveredAny = false;
		for ( var index = items.Count - 1; 0 <= index; index-- ) {
			var item = items[index];
			if ( !item.Committed ) {
				continue;
			}
			try {
				await this.failureInjector.OnStageAsync(
					PatchTransactionStage.Rollback,
					item.Artifact,
					CancellationToken.None
				).ConfigureAwait( false );
				if ( null != item.RollbackPath ) {
					CommitTemporary( item.RollbackPath, item.Artifact.Path );
					item.RollbackPath = null;
					await this.failureInjector.OnStageAsync(
						PatchTransactionStage.RestoreMetadata,
						item.Artifact,
						CancellationToken.None
					).ConfigureAwait( false );
					await this.RestoreObservedMetadataAsync(
						item.Artifact,
						CancellationToken.None
					).ConfigureAwait( false );
				} else {
					File.Delete( item.Artifact.Path );
				}
				item.Committed = false;
				recoveredAny = true;
				diagnostics.Add( string.Concat( "rolled back ", item.Artifact.DisplayName ) );
			} catch ( Exception exception ) {
				succeeded = false;
				diagnostics.Add(
					string.Concat(
						"rollback failed for ",
						item.Artifact.DisplayName,
						": ",
						exception.Message
					)
				);
			}
		}
		if ( recoveredAny && succeeded ) {
			rolledBackUnits.Add( unitId );
		}
		return succeeded;
	}

	private async Task<bool> CleanupAsync(
		IEnumerable<StagedArtifact> items,
		ICollection<string> diagnostics,
		bool injectFailure
	) {
		var succeeded = true;
		foreach ( var item in items ) {
			if ( injectFailure ) {
				try {
					await this.failureInjector.OnStageAsync(
						PatchTransactionStage.Cleanup,
						item.Artifact,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					succeeded = false;
					diagnostics.Add(
						string.Concat(
							"cleanup failed for ",
							item.Artifact.DisplayName,
							": ",
							exception.Message
						)
					);
				}
			}
			if ( !TryDeleteTemporary( item.TemporaryPath, item.Artifact, diagnostics ) ) {
				succeeded = false;
			}
			if ( !TryDeleteTemporary( item.RollbackPath, item.Artifact, diagnostics ) ) {
				succeeded = false;
			}
			item.TemporaryPath = null;
			item.RollbackPath = null;
		}
		return succeeded;
	}

	private static bool TryDeleteTemporary(
		string? path,
		PatchArtifact artifact,
		ICollection<string> diagnostics
	) {
		if ( string.IsNullOrEmpty( path ) ) {
			return true;
		}
		try {
			File.Delete( path );
			return true;
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException ) {
			diagnostics.Add(
				string.Concat(
					"cleanup failed for ",
					artifact.DisplayName,
					": ",
					exception.Message
				)
			);
			return false;
		}
	}

	private async Task ApplyOwnershipAsync(
		PatchArtifact artifact,
		FileSystemMetadata current,
		uint? requestedUserId,
		uint? requestedGroupId,
		FileSystemMutationPrecondition precondition,
		string diagnosticPrefix,
		CancellationToken cancellationToken
	) {
		if ( !requestedUserId.HasValue && !requestedGroupId.HasValue ) {
			return;
		}
		var userId = requestedUserId;
		if ( userId.HasValue
			&& current.UserId.IsAvailable
			&& userId.Value == current.UserId.GetRequiredValue() ) {
			userId = null;
		}
		var groupId = requestedGroupId;
		if ( groupId.HasValue
			&& current.GroupId.IsAvailable
			&& groupId.Value == current.GroupId.GetRequiredValue() ) {
			groupId = null;
		}
		if ( !userId.HasValue && !groupId.HasValue ) {
			return;
		}
		var ownershipResult = await this.mutationProvider.SetOwnershipAsync(
			artifact.Path,
			userId,
			groupId,
			PathDereferenceMode.NoFollow,
			precondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( !ownershipResult.Succeeded ) {
			throw new PatchApplicationException(
				ownershipResult.Message ?? string.Concat( diagnosticPrefix, artifact.DisplayName )
			);
		}
	}

	private async Task RestoreObservedMetadataAsync(
		PatchArtifact artifact,
		CancellationToken cancellationToken
	) {
		var observation = artifact.ExpectedDestination;
		if ( !observation.Exists ) {
			return;
		}
		var current = await this.metadataProvider.GetMetadataAsync(
			artifact.Path,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		var metadataPrecondition = FileSystemMutationPrecondition.FromObservation(
			current.Kind,
			current.EntryIdentity,
			PathDereferenceMode.NoFollow
		);
		await this.ApplyOwnershipAsync(
			artifact,
			current,
			observation.UserId,
			observation.GroupId,
			metadataPrecondition,
			"cannot restore ownership of ",
			cancellationToken
		).ConfigureAwait( false );
		if ( observation.Mode.HasValue && this.mutationProvider.Capabilities.CanSetModes ) {
			var modeResult = await this.mutationProvider.SetModeAsync(
				artifact.Path,
				new PosixFileMode( observation.Mode.Value ),
				PathDereferenceMode.NoFollow,
				metadataPrecondition,
				cancellationToken
			).ConfigureAwait( false );
			if ( !modeResult.Succeeded ) {
				throw new PatchApplicationException(
					modeResult.Message ?? string.Concat( "cannot restore mode of ", artifact.DisplayName )
				);
			}
		}
		if ( observation.AccessTime.HasValue || observation.ModificationTime.HasValue ) {
			var timestampResult = await this.metadataProvider.SetTimestampsAsync(
				artifact.Path,
				new FileTimestampMutationRequest {
					AccessTime = observation.AccessTime.HasValue
						? FileTimestampChange.At( observation.AccessTime.Value )
						: FileTimestampChange.Unchanged,
					ModificationTime = observation.ModificationTime.HasValue
						? FileTimestampChange.At( observation.ModificationTime.Value )
						: FileTimestampChange.Unchanged
				},
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( !timestampResult.Succeeded ) {
				throw new PatchApplicationException(
					timestampResult.Message ?? string.Concat( "cannot restore timestamps of ", artifact.DisplayName )
				);
			}
		}
	}

	private static FileStream OpenExistingForRead( string path ) {
		return new FileStream(
			path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			BufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
	}

	private static FileStream OpenTemporaryForWrite( string path ) {
		var stream = new FileStream(
			path,
			FileMode.Open,
			FileAccess.Write,
			FileShare.None,
			BufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough
		);
		stream.SetLength( 0 );
		return stream;
	}

	private static void CommitTemporary( string temporaryPath, string destinationPath ) {
		if ( File.Exists( destinationPath ) || Directory.Exists( destinationPath ) ) {
			File.Move( temporaryPath, destinationPath, overwrite: true );
		} else {
			File.Move( temporaryPath, destinationPath );
		}
	}

	private static void TryDelete( string? path ) {
		if ( string.IsNullOrEmpty( path ) ) {
			return;
		}
		try {
			File.Delete( path );
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		if ( this.disposed ) {
			return;
		}
		this.disposed = true;
		await this.CleanupAsync(
			this.staged,
			new List<string>(),
			injectFailure: false
		).ConfigureAwait( false );
	}
}
