using System.Text;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.TransactionalReplacement;

/// <summary>Tests staging, recovery units, backups, atomicity policy, rollback, and cleanup.</summary>
public sealed class TransactionalFileReplacementTransactionTests {
	/// <summary>Verifies complete replacement with a retained GNU simple backup.</summary>
	[Fact]
	public async Task ReplacesFileAndRetainsSimpleBackup() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var path = Path.GetFullPath( "destination" );
		fileSystem.Seed( path, "old" );
		var artifact = CreateReplacement( fileSystem, "unit", path, "new" );
		var options = new TransactionalReplacementOptions {
			BackupPolicy = new TransactionalReplacementBackupPolicy {
				Mode = TransactionalReplacementBackupMode.Simple,
				Retention = TransactionalReplacementBackupRetention.RetainAfterSuccess
			}
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { artifact },
			fileSystem,
			options
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.Succeeded, result.Outcome );
		Assert.Equal( "new", fileSystem.Read( path ) );
		Assert.Equal( "old", fileSystem.Read( string.Concat( path, "~" ) ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that one recovery unit restores every earlier artifact after a later commit failure.</summary>
	[Fact]
	public async Task RollsBackWholeRecoveryUnitAfterLaterCommitFailure() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var first = Path.GetFullPath( "first" );
		var second = Path.GetFullPath( "second" );
		fileSystem.Seed( first, "first-old" );
		fileSystem.Seed( second, "second-old" );
		var injector = new OneShotFailureInjector(
			TransactionalReplacementStage.Commit,
			second
		);
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] {
				CreateReplacement( fileSystem, "unit", first, "first-new" ),
				CreateReplacement( fileSystem, "unit", second, "second-new" )
			},
			fileSystem,
			failureInjector: injector
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedRolledBack, result.Outcome );
		Assert.Equal( "first-old", fileSystem.Read( first ) );
		Assert.Equal( "second-old", fileSystem.Read( second ) );
		Assert.Equal( new[] { "unit" }, result.RolledBackRecoveryUnitIds );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that rollback restores both the destination and a preexisting simple backup.</summary>
	[Fact]
	public async Task RestoresPreexistingBackupWhenRecoveryUnitFails() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var first = Path.GetFullPath( "backup-first" );
		var second = Path.GetFullPath( "backup-second" );
		fileSystem.Seed( first, "first-old" );
		fileSystem.Seed( string.Concat( first, "~" ), "older-backup" );
		fileSystem.Seed( second, "second-old" );
		var options = new TransactionalReplacementOptions {
			BackupPolicy = new TransactionalReplacementBackupPolicy {
				Mode = TransactionalReplacementBackupMode.Simple,
				Retention = TransactionalReplacementBackupRetention.RetainAfterSuccess
			}
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] {
				CreateReplacement( fileSystem, "unit", first, "first-new" ),
				CreateReplacement( fileSystem, "unit", second, "second-new" )
			},
			fileSystem,
			options,
			failureInjector: new OneShotFailureInjector( TransactionalReplacementStage.Commit, second )
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedRolledBack, result.Outcome );
		Assert.Equal( "first-old", fileSystem.Read( first ) );
		Assert.Equal( "older-backup", fileSystem.Read( string.Concat( first, "~" ) ) );
		Assert.Equal( "second-old", fileSystem.Read( second ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that rollback removes a newly published destination that did not exist before staging.</summary>
	[Fact]
	public async Task RemovesNewDestinationWhenRecoveryUnitFails() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var first = Path.GetFullPath( "new-first" );
		var second = Path.GetFullPath( "new-second" );
		fileSystem.Seed( second, "second-old" );
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] {
				CreateReplacement( fileSystem, "unit", first, "first-new" ),
				CreateReplacement( fileSystem, "unit", second, "second-new" )
			},
			fileSystem,
			failureInjector: new OneShotFailureInjector( TransactionalReplacementStage.Commit, second )
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedRolledBack, result.Outcome );
		Assert.False( fileSystem.Exists( first ) );
		Assert.Equal( "second-old", fileSystem.Read( second ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that independent recovery units may continue after one unit fails.</summary>
	[Fact]
	public async Task ContinuesIndependentRecoveryUnitsAndReportsPartialCommit() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var first = Path.GetFullPath( "first-unit" );
		var second = Path.GetFullPath( "second-unit" );
		var third = Path.GetFullPath( "third-unit" );
		fileSystem.Seed( first, "one-old" );
		fileSystem.Seed( second, "two-old" );
		fileSystem.Seed( third, "three-old" );
		var options = new TransactionalReplacementOptions {
			CommitPolicy = TransactionalReplacementCommitPolicy.ContinueIndependentUnits
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] {
				CreateReplacement( fileSystem, "one", first, "one-new" ),
				CreateReplacement( fileSystem, "two", second, "two-new" ),
				CreateReplacement( fileSystem, "three", third, "three-new" )
			},
			fileSystem,
			options,
			failureInjector: new OneShotFailureInjector( TransactionalReplacementStage.Commit, second )
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedPartiallyCommitted, result.Outcome );
		Assert.Equal( "one-new", fileSystem.Read( first ) );
		Assert.Equal( "two-old", fileSystem.Read( second ) );
		Assert.Equal( "three-new", fileSystem.Read( third ) );
		Assert.Equal( new[] { "one", "three" }, result.CommittedRecoveryUnitIds );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that deletion can retain the original as a public backup.</summary>
	[Fact]
	public async Task DeletesFileAndRetainsBackup() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var path = Path.GetFullPath( "deleted" );
		fileSystem.Seed( path, "old" );
		var observation = fileSystem.Observe( path );
		var artifact = new TransactionalReplacementArtifact(
			"unit",
			path,
			TransactionalReplacementAction.Delete,
			FileSystemMutationPrecondition.FromObservation(
				observation.Metadata!.Kind,
				observation.Metadata.EntryIdentity,
				PathDereferenceMode.NoFollow
			)
		);
		var options = new TransactionalReplacementOptions {
			BackupPolicy = new TransactionalReplacementBackupPolicy {
				Mode = TransactionalReplacementBackupMode.Simple,
				Retention = TransactionalReplacementBackupRetention.RetainAfterSuccess
			}
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { artifact },
			fileSystem,
			options
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.Succeeded, result.Outcome );
		Assert.False( fileSystem.Exists( path ) );
		Assert.Equal( "old", fileSystem.Read( string.Concat( path, "~" ) ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that an E3 identity change after staging is rejected before commit.</summary>
	[Fact]
	public async Task RevalidatesIdentityImmediatelyBeforeCommit() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var path = Path.GetFullPath( "identity-race" );
		fileSystem.Seed( path, "old" );
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { CreateReplacement( fileSystem, "unit", path, "new" ) },
			fileSystem
		);
		await transaction.StageAsync();
		fileSystem.Seed( path, "external" );
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedBeforeCommit, result.Outcome );
		Assert.Equal( "external", fileSystem.Read( path ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that cancellation during staging removes every created sibling file.</summary>
	[Fact]
	public async Task CleansStagingArtifactsAfterCancellation() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var path = Path.GetFullPath( "cancelled" );
		fileSystem.Seed( path, "old" );
		using var cancellation = new CancellationTokenSource();
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { CreateReplacement( fileSystem, "unit", path, "new" ) },
			fileSystem,
			failureInjector: new CancellationInjector(
				TransactionalReplacementStage.FlushTemporary,
				cancellation
			)
		);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => {
				_ = await transaction.CommitAsync( cancellation.Token );
			}
		);
		Assert.Equal( "old", fileSystem.Read( path ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that mandatory atomicity fails before changing a destination.</summary>
	[Fact]
	public async Task FailsBeforeCommitWhenAtomicityIsRequiredButUnavailable() {
		var fileSystem = new FakeTransactionalReplacementFileSystem(
			new TransactionalReplacementCapabilities( false, false, false, true )
		);
		var path = Path.GetFullPath( "atomic" );
		fileSystem.Seed( path, "old" );
		var options = new TransactionalReplacementOptions {
			AtomicityPolicy = TransactionalReplacementAtomicityPolicy.RequireAtomic
		};
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { CreateReplacement( fileSystem, "unit", path, "new" ) },
			fileSystem,
			options
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedAtomicityUnavailable, result.Outcome );
		Assert.Equal( "old", fileSystem.Read( path ) );
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that cleanup continues after an injected failure and succeeds on the terminal retry.</summary>
	[Fact]
	public async Task RetriesFailedCleanupBeforeReturning() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var path = Path.GetFullPath( "cleanup" );
		fileSystem.Seed( path, "old" );
		await using var transaction = new TransactionalFileReplacementTransaction(
			new[] { CreateReplacement( fileSystem, "unit", path, "new" ) },
			fileSystem,
			failureInjector: new OneShotFailureInjector( TransactionalReplacementStage.Cleanup, path )
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.Succeeded, result.Outcome );
		Assert.Contains(
			result.Diagnostics,
			diagnostic => TransactionalReplacementDiagnosticCode.CleanupFailed == diagnostic.Code
		);
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	/// <summary>Verifies that a terminal cleanup failure is explicit and disposal retries without the injector.</summary>
	[Fact]
	public async Task ReportsTerminalCleanupFailureAndDisposalRetries() {
		var fileSystem = new FakeTransactionalReplacementFileSystem();
		var path = Path.GetFullPath( "cleanup-incomplete" );
		fileSystem.Seed( path, "old" );
		var transaction = new TransactionalFileReplacementTransaction(
			new[] { CreateReplacement( fileSystem, "unit", path, "new" ) },
			fileSystem,
			failureInjector: new PersistentFailureInjector( TransactionalReplacementStage.Cleanup )
		);
		var result = await transaction.CommitAsync();
		Assert.Equal( TransactionalReplacementOutcome.FailedCleanupIncomplete, result.Outcome );
		Assert.True( 0 < fileSystem.TemporaryCount );
		await transaction.DisposeAsync();
		Assert.Equal( 0, fileSystem.TemporaryCount );
	}

	private static TransactionalReplacementArtifact CreateReplacement(
		FakeTransactionalReplacementFileSystem fileSystem,
		string unitId,
		string path,
		string content
	) {
		var observation = fileSystem.Observe( path );
		var precondition = observation.Exists
			? FileSystemMutationPrecondition.FromObservation(
				observation.Metadata!.Kind,
				observation.Metadata.EntryIdentity,
				PathDereferenceMode.NoFollow
			)
			: FileSystemMutationPrecondition.DestinationMustNotExist();
		var bytes = Encoding.UTF8.GetBytes( content );
		return new TransactionalReplacementArtifact(
			unitId,
			path,
			TransactionalReplacementAction.Replace,
			precondition,
			(destination, cancellationToken) => destination.WriteAsync( bytes.AsMemory(), cancellationToken ),
			path
		);
	}

	private sealed class CancellationInjector : ITransactionalReplacementFailureInjector {
		private readonly TransactionalReplacementStage stage;
		private readonly CancellationTokenSource cancellation;
		private int remaining = 1;

		public CancellationInjector(
			TransactionalReplacementStage stage,
			CancellationTokenSource cancellation
		) {
			this.stage = stage;
			this.cancellation = cancellation;
		}

		public ValueTask OnStageAsync(
			TransactionalReplacementStage stage,
			TransactionalReplacementArtifact artifact,
			CancellationToken cancellationToken = default
		) {
			if ( this.stage == stage && 1 == Interlocked.Exchange( ref remaining, 0 ) ) {
				cancellation.Cancel();
			}
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class PersistentFailureInjector : ITransactionalReplacementFailureInjector {
		private readonly TransactionalReplacementStage stage;

		public PersistentFailureInjector( TransactionalReplacementStage stage ) {
			this.stage = stage;
		}

		public ValueTask OnStageAsync(
			TransactionalReplacementStage stage,
			TransactionalReplacementArtifact artifact,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.stage == stage ) {
				throw new IOException( string.Concat( "Injected persistent E6 failure at ", stage, "." ) );
			}
			return ValueTask.CompletedTask;
		}
	}

	private sealed class OneShotFailureInjector : ITransactionalReplacementFailureInjector {
		private readonly TransactionalReplacementStage stage;
		private readonly string artifactPath;
		private int remaining = 1;

		public OneShotFailureInjector( TransactionalReplacementStage stage, string artifactPath ) {
			this.stage = stage;
			this.artifactPath = Path.GetFullPath( artifactPath );
		}

		public ValueTask OnStageAsync(
			TransactionalReplacementStage stage,
			TransactionalReplacementArtifact artifact,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.stage == stage
				&& Path.GetFullPath( artifact.Path ) == artifactPath
				&& 1 == Interlocked.Exchange( ref remaining, 0 ) ) {
				throw new IOException( string.Concat( "Injected E6 failure at ", stage, "." ) );
			}
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FakeTransactionalReplacementFileSystem : ITransactionalReplacementFileSystem {
		private readonly Dictionary<string, Entry> entries;
		private long identity;
		private long temporarySequence;

		public FakeTransactionalReplacementFileSystem(
			TransactionalReplacementCapabilities? capabilities = null
		) {
			entries = new Dictionary<string, Entry>( HostComparer() );
			Capabilities = capabilities ?? new TransactionalReplacementCapabilities( true, true, true, true );
		}

		public TransactionalReplacementCapabilities Capabilities { get; }
		public int TemporaryCount => entries.Values.Count( entry => entry.IsTemporary );

		public void Seed( string path, string content ) {
			entries[Normalize( path )] = CreateEntry( Encoding.UTF8.GetBytes( content ), false );
		}

		public string Read( string path ) {
			return Encoding.UTF8.GetString( entries[Normalize( path )].Content );
		}

		public bool Exists( string path ) {
			return entries.ContainsKey( Normalize( path ) );
		}

		public TransactionalReplacementObservation Observe( string path ) {
			var normalized = Normalize( path );
			if ( !entries.TryGetValue( normalized, out var entry ) ) {
				return new TransactionalReplacementObservation( normalized, false, null );
			}
			return new TransactionalReplacementObservation(
				normalized,
				true,
				new FileSystemMetadata(
					normalized,
					FileSystemEntryKind.File,
					false,
					false,
					new FileSystemEntryIdentity( "fake", entry.Identity ),
					new FileSystemIdentity( "fake", "filesystem" )
				)
			);
		}

		public ValueTask<TransactionalReplacementObservation> ObserveAsync(
			string path,
			PathDereferenceMode dereferenceMode,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Assert.Equal( PathDereferenceMode.NoFollow, dereferenceMode );
			return ValueTask.FromResult( Observe( path ) );
		}

		public ValueTask<bool> AnyNumberedBackupExistsAsync(
			string destinationPath,
			int maximumNumberedBackup,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			var prefix = string.Concat( Normalize( destinationPath ), ".~" );
			foreach ( var path in entries.Keys ) {
				if ( !path.StartsWith( prefix, HostComparison() ) || !path.EndsWith( '~' ) ) {
					continue;
				}
				var numberText = path.AsSpan( prefix.Length, path.Length - prefix.Length - 1 );
				if ( int.TryParse( numberText, out var number )
					&& 1 <= number
					&& number <= maximumNumberedBackup ) {
					return ValueTask.FromResult( true );
				}
			}
			return ValueTask.FromResult( false );
		}

		public ValueTask<string> CreateSiblingTemporaryFileAsync(
			string destinationPath,
			string purpose,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			var directory = Path.GetDirectoryName( Normalize( destinationPath ) )!;
			var path = Path.Combine(
				directory,
				string.Concat( ".fake-e6-", purpose, "-", Interlocked.Increment( ref temporarySequence ) )
			);
			entries[path] = CreateEntry( Array.Empty<byte>(), true );
			return ValueTask.FromResult( path );
		}

		public async ValueTask WriteTemporaryFileAsync(
			string path,
			TransactionalReplacementContentWriter writer,
			CancellationToken cancellationToken = default
		) {
			var normalized = Normalize( path );
			await using var stream = new MemoryStream();
			await writer( stream, cancellationToken );
			entries[normalized].Content = stream.ToArray();
		}

		public ValueTask CopyTemporaryFileAsync(
			string sourcePath,
			string destinationPath,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			entries[Normalize( destinationPath )].Content = entries[Normalize( sourcePath )].Content.ToArray();
			return ValueTask.CompletedTask;
		}

		public ValueTask<TransactionalReplacementDurabilityResult> FlushFileAsync(
			string path,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				new TransactionalReplacementDurabilityResult( TransactionalReplacementDurability.Durable )
			);
		}

		public ValueTask<TransactionalReplacementCommitResult> CommitFileAsync(
			string stagedPath,
			string destinationPath,
			bool replaceExisting,
			bool allowNonAtomicFallback,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			var staged = Normalize( stagedPath );
			var destination = Normalize( destinationPath );
			if ( replaceExisting != entries.ContainsKey( destination ) ) {
				throw new IOException( "The fake destination existence changed." );
			}
			var entry = entries[staged];
			entries.Remove( staged );
			entry.IsTemporary = false;
			entries[destination] = entry;
			return ValueTask.FromResult(
				new TransactionalReplacementCommitResult( TransactionalReplacementAtomicity.Atomic )
			);
		}

		public ValueTask<TransactionalReplacementCommitResult> DeleteFileAsync(
			string path,
			FileSystemMutationPrecondition precondition,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( !entries.Remove( Normalize( path ) ) ) {
				throw new IOException( "The fake destination disappeared." );
			}
			return ValueTask.FromResult(
				new TransactionalReplacementCommitResult( TransactionalReplacementAtomicity.Atomic )
			);
		}

		public ValueTask ApplyMetadataAsync(
			string path,
			FileSystemMetadata sourceMetadata,
			RecursiveMetadataPreservationPlan plan,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask RestoreMetadataAsync(
			string path,
			FileSystemMetadata originalMetadata,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask<TransactionalReplacementDurabilityResult> FlushContainingDirectoryAsync(
			string path,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				new TransactionalReplacementDurabilityResult( TransactionalReplacementDurability.Durable )
			);
		}

		public ValueTask DeleteTemporaryFileAsync(
			string path,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			entries.Remove( Normalize( path ) );
			return ValueTask.CompletedTask;
		}

		private Entry CreateEntry( byte[] content, bool temporary ) {
			return new Entry(
				content,
				string.Concat( "entry-", Interlocked.Increment( ref identity ) ),
				temporary
			);
		}

		private static string Normalize( string path ) {
			return Path.TrimEndingDirectorySeparator( Path.GetFullPath( path ) );
		}

		private static StringComparer HostComparer() {
			return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		}

		private static StringComparison HostComparison() {
			return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		}

		private sealed class Entry {
			public Entry( byte[] content, string identity, bool isTemporary ) {
				Content = content;
				Identity = identity;
				IsTemporary = isTemporary;
			}

			public byte[] Content { get; set; }
			public string Identity { get; }
			public bool IsTemporary { get; set; }
		}
	}
}
