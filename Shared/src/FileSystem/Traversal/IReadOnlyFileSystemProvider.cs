namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Supplies injectable one-directory-level read-only filesystem observation.
/// </summary>
public interface IReadOnlyFileSystemProvider {
	/// <summary>
	/// Observes one pathname.
	/// </summary>
	/// <param name="path">The operational pathname.</param>
	/// <param name="followSymbolicLink">Whether a symbolic-link or reparse-point target is observed instead of the link object.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The observation.</returns>
	ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
		string path,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Enumerates one directory level without recursively descending.
	/// </summary>
	/// <param name="directoryPath">The operational directory pathname.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The directory children.</returns>
	IAsyncEnumerable<ReadOnlyDirectoryEntry> EnumerateDirectoryAsync(
		string directoryPath,
		CancellationToken cancellationToken = default
	);
}
