extern alias IcodPath;

using PathIndirectionInfo = IcodPath::Icod.Path.PathIndirectionInfo;

namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Identifies the observed kind of a filesystem entry.
/// </summary>
public enum FileSystemEntryKind {
	/// <summary>The provider cannot classify the entry.</summary>
	Unknown = 0,
	/// <summary>A regular file.</summary>
	File = 1,
	/// <summary>A directory.</summary>
	Directory = 2,
	/// <summary>A POSIX or Windows symbolic link that was not dereferenced.</summary>
	SymbolicLink = 3,
	/// <summary>Another filesystem object that the provider cannot classify more precisely.</summary>
	Other = 4,
	/// <summary>A block device.</summary>
	BlockDevice = 5,
	/// <summary>A character device.</summary>
	CharacterDevice = 6,
	/// <summary>A named pipe or FIFO.</summary>
	Fifo = 7,
	/// <summary>A local-domain socket.</summary>
	Socket = 8,
	/// <summary>A Windows name-surrogate reparse point, such as a junction, that was not dereferenced.</summary>
	NameSurrogate = 9,
	/// <summary>An uncharacterized Windows reparse point whose underlying kind cannot be used safely.</summary>
	ReparsePoint = 10
}

/// <summary>Controls terminal pathname-indirection dereferencing for one provider observation.</summary>
public enum PathDereferenceMode {
	/// <summary>Observe the physical pathname object without following it.</summary>
	NoFollow = 0,
	/// <summary>Follow only a characterized indirection whose target can be safely resolved as a pathname.</summary>
	FollowEligiblePathIndirection = 1
}

/// <summary>
/// Represents a provider-defined stable identity for one filesystem entry.
/// </summary>
/// <param name="Provider">The identity provider or platform name.</param>
/// <param name="Value">The provider-defined stable value.</param>
public readonly record struct FileSystemEntryIdentity( string? Provider, string? Value ) {
	/// <summary>
	/// Gets an unavailable identity value.
	/// </summary>
	public static FileSystemEntryIdentity Unavailable { get; } = default;

	/// <summary>
	/// Gets whether a stable identity is available.
	/// </summary>
	public bool IsAvailable => !string.IsNullOrEmpty( Provider ) && !string.IsNullOrEmpty( Value );

	/// <inheritdoc/>
	public override string ToString() => IsAvailable ? string.Concat( Provider, ":", Value ) : "unavailable";
}

/// <summary>
/// Represents a provider-defined filesystem, device, mount, or volume identity.
/// </summary>
/// <param name="Provider">The identity provider or platform name.</param>
/// <param name="Value">The provider-defined stable value.</param>
public readonly record struct FileSystemIdentity( string? Provider, string? Value ) {
	/// <summary>
	/// Gets an unavailable identity value.
	/// </summary>
	public static FileSystemIdentity Unavailable { get; } = default;

	/// <summary>
	/// Gets whether a stable identity is available.
	/// </summary>
	public bool IsAvailable => !string.IsNullOrEmpty( Provider ) && !string.IsNullOrEmpty( Value );

	/// <inheritdoc/>
	public override string ToString() => IsAvailable ? string.Concat( Provider, ":", Value ) : "unavailable";
}

/// <summary>
/// Describes one read-only filesystem observation.
/// </summary>
public sealed class ReadOnlyFileSystemEntry {
	/// <summary>
	/// Initializes an observation.
	/// </summary>
	/// <param name="accessPath">The operational pathname.</param>
	/// <param name="name">The entry basename.</param>
	/// <param name="kind">The effective entry kind.</param>
	/// <param name="isSymbolicLink">Whether the source pathname names a symbolic link.</param>
	/// <param name="wasDereferenced">Whether the provider dereferenced a supported pathname indirection.</param>
	/// <param name="linkTarget">The immediate provider-reported target, when available.</param>
	/// <param name="entryIdentity">The effective entry identity.</param>
	/// <param name="fileSystemIdentity">The effective filesystem identity.</param>
	/// <param name="indirection">The physical indirection and reparse-point characterization.</param>
	public ReadOnlyFileSystemEntry(
		string accessPath,
		string name,
		FileSystemEntryKind kind,
		bool isSymbolicLink,
		bool wasDereferenced,
		string? linkTarget,
		FileSystemEntryIdentity entryIdentity,
		FileSystemIdentity fileSystemIdentity,
		PathIndirectionInfo? indirection = null
	) {
		ArgumentException.ThrowIfNullOrEmpty( accessPath );
		ArgumentException.ThrowIfNullOrEmpty( name );
		if ( !Enum.IsDefined( typeof( FileSystemEntryKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		AccessPath = accessPath;
		Name = name;
		Kind = kind;
		Indirection = indirection ?? (isSymbolicLink
			? PathIndirectionInfo.PosixSymbolicLink( linkTarget )
			: PathIndirectionInfo.None);
		IsSymbolicLink = indirection is null ? isSymbolicLink : Indirection.IsSymbolicLink;
		WasDereferenced = wasDereferenced;
		LinkTarget = Indirection.Target ?? linkTarget;
		EntryIdentity = entryIdentity;
		FileSystemIdentity = fileSystemIdentity;
	}

	/// <summary>Gets the operational pathname.</summary>
	public string AccessPath { get; }

	/// <summary>Gets the entry basename.</summary>
	public string Name { get; }

	/// <summary>Gets the effective entry kind.</summary>
	public FileSystemEntryKind Kind { get; }

	/// <summary>Gets the complete physical indirection and reparse-point characterization.</summary>
	public PathIndirectionInfo Indirection { get; }

	/// <summary>Gets whether the source pathname is specifically a symbolic link.</summary>
	public bool IsSymbolicLink { get; }

	/// <summary>Gets whether the source pathname participates in pathname indirection.</summary>
	public bool IsPathIndirection => Indirection.IsPathIndirection || IsSymbolicLink;

	/// <summary>Gets whether the source pathname is a Windows name surrogate.</summary>
	public bool IsNameSurrogate => Indirection.IsNameSurrogate;

	/// <summary>Gets whether the source pathname is a Windows directory junction.</summary>
	public bool IsJunction => Indirection.IsJunction;

	/// <summary>Gets whether the source pathname is a mounted Windows volume.</summary>
	public bool IsVolumeMountPoint => Indirection.IsVolumeMountPoint;

	/// <summary>Gets whether the source pathname is a recognized Cloud Files placeholder.</summary>
	public bool IsCloudPlaceholder => Indirection.IsCloudPlaceholder;

	/// <summary>Gets whether the source pathname carries the Windows reparse-point attribute.</summary>
	public bool IsReparsePoint => Indirection.IsReparsePoint;

	/// <summary>Gets whether the provider dereferenced a supported pathname indirection.</summary>
	public bool WasDereferenced { get; }

	/// <summary>Gets the immediate provider-reported target, when available.</summary>
	public string? LinkTarget { get; }

	/// <summary>Gets the effective entry identity.</summary>
	public FileSystemEntryIdentity EntryIdentity { get; }

	/// <summary>Gets the effective filesystem identity.</summary>
	public FileSystemIdentity FileSystemIdentity { get; }
}

/// <summary>
/// Describes one child returned by one-level directory enumeration.
/// </summary>
public sealed class ReadOnlyDirectoryEntry {
	/// <summary>
	/// Initializes a directory child.
	/// </summary>
	/// <param name="name">The child basename.</param>
	/// <param name="accessPath">The operational child pathname.</param>
	public ReadOnlyDirectoryEntry( string name, string accessPath ) {
		ArgumentException.ThrowIfNullOrEmpty( name );
		ArgumentException.ThrowIfNullOrEmpty( accessPath );
		if (
			name is "." or ".."
			|| name.Contains( Path.DirectorySeparatorChar )
			|| (
				Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
				&& name.Contains( Path.AltDirectorySeparatorChar )
			)
		) {
			throw new ArgumentException( "A directory child name must be one basename.", nameof( name ) );
		}
		Name = name;
		AccessPath = accessPath;
	}

	/// <summary>Gets the child basename.</summary>
	public string Name { get; }

	/// <summary>Gets the operational child pathname.</summary>
	public string AccessPath { get; }
}
