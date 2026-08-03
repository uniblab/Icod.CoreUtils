using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Provides the injectable host boundary consumed by the E6 transaction engine.</summary>
public interface ITransactionalReplacementFileSystem {
	/// <summary>Gets the provider's atomicity and durability capabilities.</summary>
	TransactionalReplacementCapabilities Capabilities { get; }

	/// <summary>Observes one pathname through the authoritative E3 provider.</summary>
	/// <param name="path">The pathname to observe.</param>
	/// <param name="dereferenceMode">The terminal-indirection policy.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The observation, including a controlled missing-path state.</returns>
	ValueTask<TransactionalReplacementObservation> ObserveAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken = default
	);

	/// <summary>Determines whether any GNU numbered backup exists within the configured bound.</summary>
	/// <param name="destinationPath">The destination whose sibling backups are inspected.</param>
	/// <param name="maximumNumberedBackup">The largest numbered suffix that counts.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when a sibling of the form <c>.~N~</c> exists.</returns>
	ValueTask<bool> AnyNumberedBackupExistsAsync(
		string destinationPath,
		int maximumNumberedBackup,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one securely randomized, exclusive ordinary file beside a destination.</summary>
	/// <param name="destinationPath">The destination whose parent directory receives the temporary file.</param>
	/// <param name="purpose">A stable filename-safe purpose component.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The created temporary pathname.</returns>
	ValueTask<string> CreateSiblingTemporaryFileAsync(
		string destinationPath,
		string purpose,
		CancellationToken cancellationToken = default
	);

	/// <summary>Writes complete content to an already-created temporary file.</summary>
	/// <param name="path">The temporary pathname.</param>
	/// <param name="writer">The caller's complete-file writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the write.</returns>
	ValueTask WriteTemporaryFileAsync(
		string path,
		TransactionalReplacementContentWriter writer,
		CancellationToken cancellationToken = default
	);

	/// <summary>Copies one complete ordinary file into an already-created temporary file.</summary>
	/// <param name="sourcePath">The source pathname.</param>
	/// <param name="destinationPath">The already-created temporary pathname.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the copy.</returns>
	ValueTask CopyTemporaryFileAsync(
		string sourcePath,
		string destinationPath,
		CancellationToken cancellationToken = default
	);

	/// <summary>Flushes one staged ordinary file using data-and-metadata durability.</summary>
	/// <param name="path">The pathname to flush.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The explicit durability result.</returns>
	ValueTask<TransactionalReplacementDurabilityResult> FlushFileAsync(
		string path,
		CancellationToken cancellationToken = default
	);

	/// <summary>Publishes one staged sibling file at a destination.</summary>
	/// <param name="stagedPath">The complete staged sibling file.</param>
	/// <param name="destinationPath">The destination pathname.</param>
	/// <param name="replaceExisting">Whether the operation may replace an observed existing destination.</param>
	/// <param name="allowNonAtomicFallback">Whether a provider fallback is permitted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The supplied atomicity.</returns>
	ValueTask<TransactionalReplacementCommitResult> CommitFileAsync(
		string stagedPath,
		string destinationPath,
		bool replaceExisting,
		bool allowNonAtomicFallback,
		CancellationToken cancellationToken = default
	);

	/// <summary>Removes one destination after its E4 precondition has been revalidated by the transaction.</summary>
	/// <param name="path">The pathname to remove.</param>
	/// <param name="precondition">The E4 identity-bearing precondition.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The supplied atomicity.</returns>
	ValueTask<TransactionalReplacementCommitResult> DeleteFileAsync(
		string path,
		FileSystemMutationPrecondition precondition,
		CancellationToken cancellationToken = default
	);

	/// <summary>Applies metadata selected by an E5 preservation plan.</summary>
	/// <param name="path">The committed pathname.</param>
	/// <param name="sourceMetadata">The authoritative source metadata.</param>
	/// <param name="plan">The requested-versus-required E5 plan.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing metadata application.</returns>
	ValueTask ApplyMetadataAsync(
		string path,
		FileSystemMetadata sourceMetadata,
		RecursiveMetadataPreservationPlan plan,
		CancellationToken cancellationToken = default
	);

	/// <summary>Restores all representable metadata from an original E3 observation.</summary>
	/// <param name="path">The recovered pathname.</param>
	/// <param name="originalMetadata">The original authoritative metadata.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing metadata restoration.</returns>
	ValueTask RestoreMetadataAsync(
		string path,
		FileSystemMetadata originalMetadata,
		CancellationToken cancellationToken = default
	);

	/// <summary>Flushes the directory containing one committed pathname.</summary>
	/// <param name="path">The committed pathname.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The explicit directory durability result.</returns>
	ValueTask<TransactionalReplacementDurabilityResult> FlushContainingDirectoryAsync(
		string path,
		CancellationToken cancellationToken = default
	);

	/// <summary>Removes one temporary file; absence is successful.</summary>
	/// <param name="path">The temporary pathname.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing cleanup.</returns>
	ValueTask DeleteTemporaryFileAsync(
		string path,
		CancellationToken cancellationToken = default
	);
}
