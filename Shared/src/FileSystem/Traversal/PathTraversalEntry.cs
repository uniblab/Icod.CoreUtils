namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Represents one provenance-preserving traversal entry.
/// </summary>
public sealed class PathTraversalEntry {
	/// <summary>
	/// Initializes a traversal entry.
	/// </summary>
	/// <param name="root">The root that produced the entry.</param>
	/// <param name="accessPath">The operational pathname.</param>
	/// <param name="displayPath">The user-facing pathname.</param>
	/// <param name="relativePath">The path relative to the traversal root.</param>
	/// <param name="name">The entry basename.</param>
	/// <param name="depth">The zero-based traversal depth.</param>
	/// <param name="kind">The effective entry kind.</param>
	/// <param name="isSymbolicLink">Whether the source path names a symbolic link or reparse-point link.</param>
	/// <param name="isFollowedSymbolicLink">Whether the link was followed.</param>
	/// <param name="linkTarget">The immediate provider-reported link target.</param>
	/// <param name="entryIdentity">The effective entry identity.</param>
	/// <param name="fileSystemIdentity">The effective filesystem identity.</param>
	public PathTraversalEntry(
		PathTraversalRoot root,
		string accessPath,
		string displayPath,
		string relativePath,
		string name,
		int depth,
		FileSystemEntryKind kind,
		bool isSymbolicLink,
		bool isFollowedSymbolicLink,
		string? linkTarget,
		FileSystemEntryIdentity entryIdentity,
		FileSystemIdentity fileSystemIdentity
	) {
		ArgumentNullException.ThrowIfNull( root );
		ArgumentException.ThrowIfNullOrEmpty( accessPath );
		ArgumentException.ThrowIfNullOrEmpty( displayPath );
		ArgumentNullException.ThrowIfNull( relativePath );
		ArgumentException.ThrowIfNullOrEmpty( name );
		ArgumentOutOfRangeException.ThrowIfNegative( depth );
		if (
			depth > 0
			&& (
				name is "." or ".."
				|| name.Contains( Path.DirectorySeparatorChar )
				|| (
					Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
					&& name.Contains( Path.AltDirectorySeparatorChar )
				)
			)
		) {
			throw new ArgumentException( "A descendant entry name must be one basename.", nameof( name ) );
		}
		if ( !Enum.IsDefined( typeof( FileSystemEntryKind ), kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		Root = root;
		AccessPath = accessPath;
		DisplayPath = displayPath;
		RelativePath = relativePath;
		Name = name;
		Depth = depth;
		Kind = kind;
		IsSymbolicLink = isSymbolicLink;
		IsFollowedSymbolicLink = isFollowedSymbolicLink;
		LinkTarget = linkTarget;
		EntryIdentity = entryIdentity;
		FileSystemIdentity = fileSystemIdentity;
	}

	/// <summary>Gets the producing root.</summary>
	public PathTraversalRoot Root { get; }

	/// <summary>Gets the operational pathname.</summary>
	public string AccessPath { get; }

	/// <summary>Gets the user-facing pathname.</summary>
	public string DisplayPath { get; }

	/// <summary>Gets the path relative to the traversal root.</summary>
	public string RelativePath { get; }

	/// <summary>Gets the entry basename.</summary>
	public string Name { get; }

	/// <summary>Gets the zero-based traversal depth.</summary>
	public int Depth { get; }

	/// <summary>Gets the effective entry kind.</summary>
	public FileSystemEntryKind Kind { get; }

	/// <summary>Gets whether the source path names a symbolic link or reparse-point link.</summary>
	public bool IsSymbolicLink { get; }

	/// <summary>Gets whether the link was followed.</summary>
	public bool IsFollowedSymbolicLink { get; }

	/// <summary>Gets the immediate provider-reported link target.</summary>
	public string? LinkTarget { get; }

	/// <summary>Gets the effective entry identity.</summary>
	public FileSystemEntryIdentity EntryIdentity { get; }

	/// <summary>Gets the effective filesystem identity.</summary>
	public FileSystemIdentity FileSystemIdentity { get; }

	/// <summary>Gets whether this entry is the traversal root itself.</summary>
	public bool IsRoot => Depth == 0;

	/// <summary>Gets whether this entry is a descendant of the traversal root.</summary>
	public bool IsDescendant => Depth > 0;
}
