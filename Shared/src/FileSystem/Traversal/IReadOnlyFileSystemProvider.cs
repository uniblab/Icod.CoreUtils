namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Supplies injectable one-directory-level read-only filesystem observation.
/// </summary>
public interface IReadOnlyFileSystemProvider {
	/// <summary>
	/// Observes one pathname.
	/// </summary>
	/// <param name="path">The operational pathname.</param>
	/// <param name="followSymbolicLink">Whether a supported pathname-indirection target is observed instead of the physical object.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The observation.</returns>
	ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
		string path,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	);

	/// <summary>Observes one filesystem entry under an explicit dereference policy.</summary>
	/// <param name="path">The operational pathname.</param>
	/// <param name="dereferenceMode">The terminal pathname-indirection policy.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The read-only entry observation.</returns>
	ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		return ObserveAsync(
			path,
			dereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		);
	}

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
