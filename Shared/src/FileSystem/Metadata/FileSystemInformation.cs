using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.Metadata;

/// <summary>
/// Describes the filesystem, volume, or mount containing one pathname.
/// </summary>
public sealed class FileSystemInformation {
	/// <summary>
	/// Initializes one filesystem observation.
	/// </summary>
	/// <param name="path">The pathname used to select the containing filesystem.</param>
	/// <param name="identity">The E1 filesystem identity.</param>
	public FileSystemInformation( string path, FileSystemIdentity identity ) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		Path = path;
		Identity = identity;
	}

	/// <summary>Gets the pathname used for the observation.</summary>
	public string Path { get; }

	/// <summary>Gets the stable E1 filesystem identity.</summary>
	public FileSystemIdentity Identity { get; }

	/// <summary>Gets the selected mount point or volume root.</summary>
	public FileSystemMetadataValue<string> MountPoint { get; init; }

	/// <summary>Gets the filesystem type or drive format.</summary>
	public FileSystemMetadataValue<string> FileSystemType { get; init; }

	/// <summary>Gets the volume label.</summary>
	public FileSystemMetadataValue<string> VolumeName { get; init; }

	/// <summary>Gets the total filesystem capacity in bytes.</summary>
	public FileSystemMetadataValue<ulong> TotalBytes { get; init; }

	/// <summary>Gets the total free capacity in bytes.</summary>
	public FileSystemMetadataValue<ulong> FreeBytes { get; init; }

	/// <summary>Gets the free capacity available to the current caller in bytes.</summary>
	public FileSystemMetadataValue<ulong> AvailableBytes { get; init; }

	/// <summary>Gets the fundamental filesystem block size in bytes.</summary>
	public FileSystemMetadataValue<ulong> BlockSize { get; init; }

	/// <summary>Gets the filesystem fragment or allocation-unit size in bytes.</summary>
	public FileSystemMetadataValue<ulong> FragmentSize { get; init; }

	/// <summary>Gets the maximum component-name length.</summary>
	public FileSystemMetadataValue<ulong> MaximumNameLength { get; init; }

	/// <summary>Gets whether the containing filesystem is read-only.</summary>
	public FileSystemMetadataValue<bool> IsReadOnly { get; init; }
}
