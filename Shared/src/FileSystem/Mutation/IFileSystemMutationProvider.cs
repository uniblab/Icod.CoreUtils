using Icod.CommandFramework.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.Mutation;

/// <summary>
/// Provides injectable race-aware single-path mutation primitives.
/// </summary>
public interface IFileSystemMutationProvider {
	/// <summary>Gets the host capabilities.</summary>
	FileSystemMutationCapabilities Capabilities { get; }

	/// <summary>Creates one directory without creating missing parents.</summary>
	ValueTask<FileSystemMutationResult> CreateDirectoryAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one empty ordinary file exclusively.</summary>
	ValueTask<FileSystemMutationResult> CreateFileAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one hard link to an existing non-directory entry.</summary>
	ValueTask<FileSystemMutationResult> CreateHardLinkAsync(
		string path,
		string existingPath,
		PathDereferenceMode existingPathDereferenceMode,
		FileSystemMutationPrecondition? destinationPrecondition = null,
		FileSystemMutationPrecondition? existingPathPrecondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one file or directory symbolic link.</summary>
	ValueTask<FileSystemMutationResult> CreateSymbolicLinkAsync(
		string path,
		string target,
		bool targetIsDirectory,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one Windows directory junction backed by an NTFS or ReFS mount-point reparse point.</summary>
	ValueTask<FileSystemMutationResult> CreateJunctionAsync(
		string path,
		string target,
		FileSystemMutationPrecondition? destinationPrecondition = null,
		FileSystemMutationPrecondition? targetPrecondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one FIFO without substituting an ordinary file.</summary>
	ValueTask<FileSystemMutationResult> CreateFifoAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Creates one block or character device node without emulation.</summary>
	ValueTask<FileSystemMutationResult> CreateDeviceNodeAsync(
		string path,
		FileSystemEntryKind kind,
		DeviceNumber deviceNumber,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Removes one file, link object, explicitly accepted reparse object, FIFO, socket, or device node.</summary>
	ValueTask<FileSystemMutationResult> RemoveFileAsync(
		string path,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Removes one empty physical directory without following terminal indirection.</summary>
	ValueTask<FileSystemMutationResult> RemoveDirectoryAsync(
		string path,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Sets the mode of one existing object under an explicit E3R dereference policy.</summary>
	ValueTask<FileSystemMutationResult> SetModeAsync(
		string path,
		PosixFileMode mode,
		PathDereferenceMode dereferenceMode,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>Sets the numeric owner and/or group of one existing object under an explicit E3R dereference policy.</summary>
	/// <param name="path">The pathname to mutate.</param>
	/// <param name="userId">The replacement user ID, or <see langword="null"/> to retain it.</param>
	/// <param name="groupId">The replacement group ID, or <see langword="null"/> to retain it.</param>
	/// <param name="dereferenceMode">The terminal pathname-indirection policy.</param>
	/// <param name="precondition">The optional E3/E3R observation to revalidate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The controlled mutation result.</returns>
	ValueTask<FileSystemMutationResult> SetOwnershipAsync(
		string path,
		uint? userId,
		uint? groupId,
		PathDereferenceMode dereferenceMode,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult(
		FileSystemMutationResult.Unsupported( path, "POSIX ownership mutation is not supported by this provider." )
	);
}
