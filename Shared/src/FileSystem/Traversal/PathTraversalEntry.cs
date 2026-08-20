using Path = global::System.IO.Path;
using PathIndirectionInfo = Icod.Path.PathIndirectionInfo;

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
	/// <param name="isSymbolicLink">Whether the source path specifically names a symbolic link.</param>
	/// <param name="isFollowedSymbolicLink">Whether the link was followed.</param>
	/// <param name="linkTarget">The immediate provider-reported link target.</param>
	/// <param name="entryIdentity">The effective entry identity.</param>
	/// <param name="fileSystemIdentity">The effective filesystem identity.</param>
	/// <param name="indirection">The physical indirection and reparse-point characterization.</param>
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
		FileSystemIdentity fileSystemIdentity,
		PathIndirectionInfo? indirection = null
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
		Indirection = indirection ?? (isSymbolicLink
			? PathIndirectionInfo.PosixSymbolicLink( linkTarget )
			: PathIndirectionInfo.None);
		IsSymbolicLink = indirection is null ? isSymbolicLink : Indirection.IsSymbolicLink;
		WasDereferenced = isFollowedSymbolicLink;
		LinkTarget = Indirection.Target ?? linkTarget;
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

	/// <summary>Gets the complete physical indirection and reparse-point characterization.</summary>
	public PathIndirectionInfo Indirection { get; }

	/// <summary>Gets whether the source path specifically names a symbolic link.</summary>
	public bool IsSymbolicLink { get; }

	/// <summary>Gets whether the source path participates in pathname indirection.</summary>
	public bool IsPathIndirection => Indirection.IsPathIndirection || IsSymbolicLink;

	/// <summary>Gets whether the source path is a Windows directory junction.</summary>
	public bool IsJunction => Indirection.IsJunction;

	/// <summary>Gets whether the source path is a mounted Windows volume.</summary>
	public bool IsVolumeMountPoint => Indirection.IsVolumeMountPoint;

	/// <summary>Gets whether the source path is a recognized Cloud Files placeholder.</summary>
	public bool IsCloudPlaceholder => Indirection.IsCloudPlaceholder;

	/// <summary>Gets whether the source path carries the Windows reparse-point attribute.</summary>
	public bool IsReparsePoint => Indirection.IsReparsePoint;

	/// <summary>Gets whether a supported pathname indirection was followed.</summary>
	public bool WasDereferenced { get; }

	/// <summary>Gets whether the historical link-following flag is set.</summary>
	public bool IsFollowedSymbolicLink => WasDereferenced;

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
