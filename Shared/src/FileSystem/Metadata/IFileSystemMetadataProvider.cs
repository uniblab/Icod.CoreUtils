using Icod.CoreUtils.Shared.Platform;

namespace Icod.CoreUtils.Shared.FileSystem.Metadata;

/// <summary>
/// Provides injectable authoritative filesystem metadata and timestamp mutation.
/// </summary>
public interface IFileSystemMetadataProvider {
	/// <summary>Observes one filesystem entry.</summary>
	/// <param name="path">The pathname to observe.</param>
	/// <param name="followSymbolicLink">Whether to dereference a terminal symbolic link or reparse-point link.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The metadata observation.</returns>
	ValueTask<FileSystemMetadata> GetMetadataAsync(
		string path,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	);

	/// <summary>Observes the filesystem containing one pathname.</summary>
	/// <param name="path">The pathname whose containing filesystem should be observed.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The filesystem information.</returns>
	ValueTask<FileSystemInformation> GetFileSystemInformationAsync(
		string path,
		CancellationToken cancellationToken = default
	);

	/// <summary>Applies one timestamp-mutation request.</summary>
	/// <param name="path">The pathname to mutate.</param>
	/// <param name="request">The requested changes.</param>
	/// <param name="followSymbolicLink">Whether to mutate a terminal link target rather than the link object.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The controlled platform operation result.</returns>
	ValueTask<PlatformOperationResult> SetTimestampsAsync(
		string path,
		FileTimestampMutationRequest request,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	);
}
