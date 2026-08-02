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
	/// <summary>A symbolic link or reparse-point link that was not dereferenced.</summary>
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
	Socket = 8
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
	/// <param name="isSymbolicLink">Whether the source pathname names a symbolic link or reparse-point link.</param>
	/// <param name="wasDereferenced">Whether the provider dereferenced the link for this observation.</param>
	/// <param name="linkTarget">The immediate provider-reported link target, when available.</param>
	/// <param name="entryIdentity">The effective entry identity.</param>
	/// <param name="fileSystemIdentity">The effective filesystem identity.</param>
	public ReadOnlyFileSystemEntry(
		string accessPath,
		string name,
		FileSystemEntryKind kind,
		bool isSymbolicLink,
		bool wasDereferenced,
		string? linkTarget,
		FileSystemEntryIdentity entryIdentity,
		FileSystemIdentity fileSystemIdentity
	) {
		ArgumentException.ThrowIfNullOrEmpty( accessPath );
		ArgumentException.ThrowIfNullOrEmpty( name );
		if ( !Enum.IsDefined( typeof( FileSystemEntryKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		AccessPath = accessPath;
		Name = name;
		Kind = kind;
		IsSymbolicLink = isSymbolicLink;
		WasDereferenced = wasDereferenced;
		LinkTarget = linkTarget;
		EntryIdentity = entryIdentity;
		FileSystemIdentity = fileSystemIdentity;
	}

	/// <summary>Gets the operational pathname.</summary>
	public string AccessPath { get; }

	/// <summary>Gets the entry basename.</summary>
	public string Name { get; }

	/// <summary>Gets the effective entry kind.</summary>
	public FileSystemEntryKind Kind { get; }

	/// <summary>Gets whether the source pathname names a symbolic link or reparse-point link.</summary>
	public bool IsSymbolicLink { get; }

	/// <summary>Gets whether the provider dereferenced the link.</summary>
	public bool WasDereferenced { get; }

	/// <summary>Gets the immediate provider-reported link target, when available.</summary>
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
