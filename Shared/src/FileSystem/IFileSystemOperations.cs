namespace Icod.CoreUtils.Shared.FileSystem;

using Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Supplies injectable, capability-aware durable-flush and sparse-file operations.
/// Implementations never take ownership of caller-supplied streams.
/// </summary>
/// <remarks>
/// The caller must keep every supplied stream open and must not concurrently dispose it or mutate its
/// native position until the returned operation has completed. Implementations preserve the managed
/// stream position where the individual operation documents that behavior.
/// </remarks>
public interface IFileSystemOperations {
	/// <summary>Gets the operating-system API capability report.</summary>
	FileSystemCapabilities Capabilities { get; }

	/// <summary>Flushes a specific file using the requested durability semantics.</summary>
	ValueTask<PlatformOperationResult> FlushFileAsync(
		FileStream file,
		FileFlushMode mode,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Opens and flushes a pathname using the requested durability semantics.
	/// Implementations should support directories and special files where the host APIs permit it.
	/// </summary>
	ValueTask<PlatformOperationResult> FlushFileAsync(
		string path,
		FileFlushMode mode,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult(
		PlatformOperationResult.Unsupported(
			"pathname-specific file flushing is not implemented by this provider"
		)
	);

	/// <summary>Flushes the filesystem containing the supplied path.</summary>
	ValueTask<PlatformOperationResult> FlushFileSystemAsync(
		string path,
		CancellationToken cancellationToken = default
	);

	/// <summary>Requests a flush of all mounted filesystems.</summary>
	ValueTask<PlatformOperationResult> FlushAllFileSystemsAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Extends a file while requesting sparse allocation semantics and preserves the stream position.
	/// </summary>
	ValueTask<PlatformOperationResult<SparseExtensionInfo>> ExtendSparseAsync(
		FileStream file,
		long newLength,
		CancellationToken cancellationToken = default
	);

	/// <summary>Queries allocated logical ranges for an open file without changing its stream position.</summary>
	ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
		FileStream file,
		CancellationToken cancellationToken = default
	);

	/// <summary>Queries allocated logical ranges for a pathname.</summary>
	ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
		string path,
		CancellationToken cancellationToken = default
	);
}
