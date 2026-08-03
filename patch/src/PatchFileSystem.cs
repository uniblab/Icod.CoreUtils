namespace Icod.Patch;

using System.Collections.ObjectModel;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
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
}

/// <summary>Identifies a testable transaction lifecycle boundary.</summary>
internal enum PatchTransactionStage {
	/// <summary>Before observing and validating destinations.</summary>
	Validate,
	/// <summary>Before creating a secure sibling temporary file.</summary>
	CreateTemporary,
	/// <summary>Before writing staged artifact bytes.</summary>
	WriteTemporary,
	/// <summary>Before preserving a rollback copy.</summary>
	PreserveRollback,
	/// <summary>Before committing one staged artifact.</summary>
	Commit,
	/// <summary>Before applying mode or timestamp metadata.</summary>
	ApplyMetadata,
	/// <summary>Before rolling back a committed artifact.</summary>
	Rollback,
	/// <summary>Before deleting temporary files.</summary>
	Cleanup
}

/// <summary>Injects deterministic failures into the initial P9 transaction boundary.</summary>
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

/// <summary>Contains the result of one Patch artifact transaction.</summary>
internal sealed class PatchTransactionResult {
	/// <summary>Initializes a transaction result.</summary>
	public PatchTransactionResult(
		bool succeeded,
		IReadOnlyList<string> diagnostics,
		Exception? exception = null
	) {
		ArgumentNullException.ThrowIfNull( diagnostics );
		this.Succeeded = succeeded;
		this.Diagnostics = new ReadOnlyCollection<string>( diagnostics.ToArray() );
		this.Exception = exception;
	}

	/// <summary>Gets whether every requested artifact committed successfully.</summary>
	public bool Succeeded { get; }
	/// <summary>Gets deterministic controlled diagnostics.</summary>
	public IReadOnlyList<string> Diagnostics { get; }
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
	private readonly IFileSystemMutationProvider mutationProvider;
	private readonly CanonicalPathResolver pathResolver;

	/// <summary>Initializes the host adapter.</summary>
	public SystemPatchFileSystem()
		: this( SystemFileSystemMetadataProvider.Instance, SystemFileSystemMutationProvider.Instance ) {
	}

	/// <summary>Initializes an adapter over injected E3 and E4 providers.</summary>
	public SystemPatchFileSystem(
		IFileSystemMetadataProvider metadataProvider,
		IFileSystemMutationProvider mutationProvider
	) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.mutationProvider = mutationProvider ?? throw new ArgumentNullException( nameof( mutationProvider ) );
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
		var lexical = this.pathResolver.NormalizeLexically( path, workingDirectory );
		if ( !lexical.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( lexical.Failure! ) );
		}
		if ( !followPathIndirection ) {
			var inspection = await this.pathResolver.InspectLinkAsync(
				lexical.Path!,
				workingDirectory,
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
				BasePath = workingDirectory,
				MissingComponentPolicy = MissingPathComponentPolicy.AllowMissingSuffix,
				FollowSymbolicLinks = true,
				FollowFinalSymbolicLink = followPathIndirection
			},
			cancellationToken
		).ConfigureAwait( false );
		if ( !physical.Succeeded ) {
			throw new PatchApplicationException( FormatPathFailure( physical.Failure! ) );
		}
		return physical.Path!;
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
		IPatchTransaction transaction = new SystemPatchTransaction(
			plan,
			this.metadataProvider,
			this.mutationProvider,
			failureInjector ?? NullPatchTransactionFailureInjector.Instance
		);
		return ValueTask.FromResult( transaction );
	}
}

/// <summary>Stages sibling temporary files and provides provisional rollback pending Completion Gate E6.</summary>
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
			await this.CleanupAsync( CancellationToken.None, injectFailure: false ).ConfigureAwait( false );
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
		try {
			await this.StageAsync( cancellationToken ).ConfigureAwait( false );
			foreach ( var item in this.staged ) {
				cancellationToken.ThrowIfCancellationRequested();
				await this.failureInjector.OnStageAsync(
					PatchTransactionStage.Commit,
					item.Artifact,
					cancellationToken
				).ConfigureAwait( false );
				await this.ValidateObservationAsync( item.Artifact, cancellationToken ).ConfigureAwait( false );
				if ( PatchArtifactAction.ValidateOnly == item.Artifact.Action ) {
					continue;
				}
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
			await this.CleanupAsync( CancellationToken.None, injectFailure: true ).ConfigureAwait( false );
			return new PatchTransactionResult( true, diagnostics );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await this.TryRollbackAndCleanupAsync( diagnostics ).ConfigureAwait( false );
			throw;
		} catch ( Exception exception ) {
			diagnostics.Add( exception.Message );
			await this.TryRollbackAndCleanupAsync( diagnostics ).ConfigureAwait( false );
			return new PatchTransactionResult( false, diagnostics, exception );
		}
	}

	private async Task<string> StageContentAsync(
		PatchArtifact artifact,
		CancellationToken cancellationToken
	) {
		await this.failureInjector.OnStageAsync(
			PatchTransactionStage.CreateTemporary,
			artifact,
			cancellationToken
		).ConfigureAwait( false );
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
			await using var input = OpenExistingForRead( sourcePath );
			await using var output = OpenTemporaryForWrite( temporaryPath );
			await input.CopyToAsync( output, BufferSize, cancellationToken ).ConfigureAwait( false );
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
		if ( artifact.Metadata.Mode.HasValue && this.mutationProvider.Capabilities.CanSetModes ) {
			var result = await this.mutationProvider.SetModeAsync(
				artifact.Path,
				new PosixFileMode( artifact.Metadata.Mode.Value ),
				PathDereferenceMode.NoFollow,
				new FileSystemMutationPrecondition(
					FileSystemMutationExistence.MustExist,
					PathDereferenceMode.NoFollow
				),
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

	private async Task RollbackAsync(
		ICollection<string> diagnostics,
		CancellationToken cancellationToken
	) {
		for ( var index = this.staged.Count - 1; 0 <= index; index-- ) {
			var item = this.staged[index];
			if ( !item.Committed ) {
				continue;
			}
			await this.failureInjector.OnStageAsync(
				PatchTransactionStage.Rollback,
				item.Artifact,
				cancellationToken
			).ConfigureAwait( false );
			if ( null != item.RollbackPath ) {
				CommitTemporary( item.RollbackPath, item.Artifact.Path );
				item.RollbackPath = null;
				await this.RestoreObservedMetadataAsync( item.Artifact, cancellationToken ).ConfigureAwait( false );
			} else {
				TryDelete( item.Artifact.Path );
			}
			item.Committed = false;
			diagnostics.Add( string.Concat( "rolled back ", item.Artifact.DisplayName ) );
		}
	}

	private async Task CleanupAsync( CancellationToken cancellationToken, bool injectFailure ) {
		if ( injectFailure ) {
			foreach ( var item in this.staged ) {
				await this.failureInjector.OnStageAsync(
					PatchTransactionStage.Cleanup,
					item.Artifact,
					cancellationToken
				).ConfigureAwait( false );
			}
		}
		foreach ( var item in this.staged ) {
			TryDelete( item.TemporaryPath );
			TryDelete( item.RollbackPath );
			item.TemporaryPath = null;
			item.RollbackPath = null;
		}
	}

	private async Task TryRollbackAndCleanupAsync( ICollection<string> diagnostics ) {
		try {
			await this.RollbackAsync( diagnostics, CancellationToken.None ).ConfigureAwait( false );
		} catch ( Exception rollbackException ) {
			diagnostics.Add( string.Concat( "rollback failed: ", rollbackException.Message ) );
		}
		try {
			await this.CleanupAsync( CancellationToken.None, injectFailure: false ).ConfigureAwait( false );
		} catch ( Exception cleanupException ) {
			diagnostics.Add( string.Concat( "cleanup failed: ", cleanupException.Message ) );
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
		if ( observation.Mode.HasValue && this.mutationProvider.Capabilities.CanSetModes ) {
			var modeResult = await this.mutationProvider.SetModeAsync(
				artifact.Path,
				new PosixFileMode( observation.Mode.Value ),
				PathDereferenceMode.NoFollow,
				new FileSystemMutationPrecondition( FileSystemMutationExistence.MustExist ),
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
		await this.CleanupAsync( CancellationToken.None, injectFailure: false ).ConfigureAwait( false );
	}
}
