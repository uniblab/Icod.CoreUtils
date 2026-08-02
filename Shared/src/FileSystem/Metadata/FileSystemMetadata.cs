extern alias IcodPath;

using PathIndirectionInfo = IcodPath::Icod.Path.PathIndirectionInfo;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.Metadata;

/// <summary>
/// Describes authoritative metadata for one filesystem entry while retaining explicit platform gaps.
/// </summary>
public sealed class FileSystemMetadata {
	/// <summary>
	/// Initializes one metadata observation.
	/// </summary>
	/// <param name="path">The operational pathname.</param>
	/// <param name="kind">The effective entry kind.</param>
	/// <param name="isSymbolicLink">Whether the source pathname specifically names a symbolic link.</param>
	/// <param name="wasDereferenced">Whether the source link was dereferenced.</param>
	/// <param name="entryIdentity">The effective entry identity established by Completion Gate E1.</param>
	/// <param name="fileSystemIdentity">The effective filesystem identity established by Completion Gate E1.</param>
	/// <param name="indirection">The physical indirection and reparse-point characterization.</param>
	public FileSystemMetadata(
		string path,
		FileSystemEntryKind kind,
		bool isSymbolicLink,
		bool wasDereferenced,
		FileSystemEntryIdentity entryIdentity,
		FileSystemIdentity fileSystemIdentity,
		PathIndirectionInfo? indirection = null
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		if ( !Enum.IsDefined( typeof( FileSystemEntryKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		Path = path;
		Kind = kind;
		Indirection = indirection ?? (isSymbolicLink
			? PathIndirectionInfo.PosixSymbolicLink( null )
			: PathIndirectionInfo.None);
		IsSymbolicLink = indirection is null ? isSymbolicLink : Indirection.IsSymbolicLink;
		WasDereferenced = wasDereferenced;
		EntryIdentity = entryIdentity;
		FileSystemIdentity = fileSystemIdentity;
	}

	/// <summary>Gets the operational pathname.</summary>
	public string Path { get; }

	/// <summary>Gets the effective entry kind.</summary>
	public FileSystemEntryKind Kind { get; }

	/// <summary>Gets the complete physical indirection and reparse-point characterization.</summary>
	public PathIndirectionInfo Indirection { get; }

	/// <summary>Gets whether the source pathname specifically names a symbolic link.</summary>
	public bool IsSymbolicLink { get; }

	/// <summary>Gets whether the source pathname participates in pathname indirection.</summary>
	public bool IsPathIndirection => Indirection.IsPathIndirection || IsSymbolicLink;

	/// <summary>Gets whether the source pathname is a Windows directory junction.</summary>
	public bool IsJunction => Indirection.IsJunction;

	/// <summary>Gets whether the source pathname is a mounted Windows volume.</summary>
	public bool IsVolumeMountPoint => Indirection.IsVolumeMountPoint;

	/// <summary>Gets whether the source pathname is a recognized Cloud Files placeholder.</summary>
	public bool IsCloudPlaceholder => Indirection.IsCloudPlaceholder;

	/// <summary>Gets whether the source pathname carries the Windows reparse-point attribute.</summary>
	public bool IsReparsePoint => Indirection.IsReparsePoint;

	/// <summary>Gets whether the source link was dereferenced.</summary>
	public bool WasDereferenced { get; }

	/// <summary>Gets the effective entry identity supplied by the E1 identity contract.</summary>
	public FileSystemEntryIdentity EntryIdentity { get; }

	/// <summary>Gets the effective filesystem identity supplied by the E1 identity contract.</summary>
	public FileSystemIdentity FileSystemIdentity { get; }

	/// <summary>Gets the immediate pathname-indirection target text.</summary>
	public FileSystemMetadataValue<string> LinkTarget { get; init; }

	/// <summary>Gets the Windows reparse tag when the source object carries one.</summary>
	public FileSystemMetadataValue<uint> ReparseTag { get; init; }

	/// <summary>Gets the identity of the source link object when the source pathname is a link.</summary>
	public FileSystemMetadataValue<FileSystemEntryIdentity> LinkIdentity { get; init; }

	/// <summary>Gets the logical size in bytes.</summary>
	public FileSystemMetadataValue<ulong> Size { get; init; }

	/// <summary>Gets the hard-link count.</summary>
	public FileSystemMetadataValue<ulong> LinkCount { get; init; }

	/// <summary>Gets the complete native mode value, including the file-type bits where supplied by the host.</summary>
	public FileSystemMetadataValue<uint> Mode { get; init; }

	/// <summary>Gets the numeric owner identifier.</summary>
	public FileSystemMetadataValue<uint> UserId { get; init; }

	/// <summary>Gets the numeric group identifier.</summary>
	public FileSystemMetadataValue<uint> GroupId { get; init; }

	/// <summary>Gets the resolved owner account name, or a platform identifier when no display name can be resolved.</summary>
	public FileSystemMetadataValue<string> OwnerName { get; init; }

	/// <summary>Gets the resolved group account name, or a platform identifier when no display name can be resolved.</summary>
	public FileSystemMetadataValue<string> GroupName { get; init; }

	/// <summary>Gets the last-access instant.</summary>
	public FileSystemMetadataValue<DateTimeOffset> AccessTime { get; init; }

	/// <summary>Gets the last-data-modification instant.</summary>
	public FileSystemMetadataValue<DateTimeOffset> ModificationTime { get; init; }

	/// <summary>Gets the last-inode- or metadata-change instant.</summary>
	public FileSystemMetadataValue<DateTimeOffset> ChangeTime { get; init; }

	/// <summary>Gets the birth or creation instant.</summary>
	public FileSystemMetadataValue<DateTimeOffset> BirthTime { get; init; }

	/// <summary>Gets the native device, volume, or equivalent identifier.</summary>
	public FileSystemMetadataValue<string> DeviceIdentifier { get; init; }

	/// <summary>Gets the native inode or platform-equivalent object number.</summary>
	public FileSystemMetadataValue<ulong> InodeNumber { get; init; }

	/// <summary>Gets the special-device identifier for character or block devices.</summary>
	public FileSystemMetadataValue<string> SpecialDeviceIdentifier { get; init; }

	/// <summary>Gets the number of allocated blocks.</summary>
	public FileSystemMetadataValue<ulong> AllocatedBlocks { get; init; }

	/// <summary>Gets the byte size represented by each value in <see cref="AllocatedBlocks"/>.</summary>
	public FileSystemMetadataValue<ulong> AllocationBlockSize { get; init; }

	/// <summary>Gets the preferred I/O block size reported for the entry.</summary>
	public FileSystemMetadataValue<ulong> PreferredIoBlockSize { get; init; }

	/// <summary>Gets host file attributes when exposed.</summary>
	public FileSystemMetadataValue<FileAttributes> Attributes { get; init; }

	/// <summary>Gets the timestamp mutations supported for this entry.</summary>
	public FileSystemMetadataValue<FileTimestampMutationCapabilities> TimestampMutationCapabilities { get; init; }

	/// <summary>
	/// Gets allocated bytes when both block count and block size are available and their product fits in <see cref="ulong"/>.
	/// </summary>
	public FileSystemMetadataValue<ulong> AllocatedBytes {
		get {
			if ( !AllocatedBlocks.IsAvailable || !AllocationBlockSize.IsAvailable ) {
				return FileSystemMetadataValue<ulong>.Unavailable(
					"Allocated bytes require both allocated-block count and block size."
				);
			}
			try {
				return FileSystemMetadataValue<ulong>.Available(
					checked( AllocatedBlocks.GetRequiredValue() * AllocationBlockSize.GetRequiredValue() )
				);
			} catch ( OverflowException ) {
				return FileSystemMetadataValue<ulong>.Unavailable( "The allocated-byte count exceeds UInt64." );
			}
		}
	}
}
