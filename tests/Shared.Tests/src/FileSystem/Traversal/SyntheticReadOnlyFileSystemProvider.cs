using System.Runtime.CompilerServices;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>
/// Supplies a deterministic in-memory filesystem graph for traversal tests.
/// </summary>
internal sealed class SyntheticReadOnlyFileSystemProvider : IReadOnlyFileSystemProvider {
	private readonly Dictionary<string, SyntheticNode> _nodes = new( GetPathComparer() );

	/// <summary>
	/// Adds a directory.
	/// </summary>
	/// <param name="path">The directory path.</param>
	/// <param name="entryIdentity">The optional entry identity value.</param>
	/// <param name="fileSystemIdentity">The optional filesystem identity value.</param>
	/// <returns>This provider.</returns>
	internal SyntheticReadOnlyFileSystemProvider AddDirectory(
		string path,
		string? entryIdentity = null,
		string fileSystemIdentity = "fs-1"
	) => AddNode(
		path,
		FileSystemEntryKind.Directory,
		entryIdentity,
		fileSystemIdentity,
		null
	);

	/// <summary>
	/// Adds a regular file.
	/// </summary>
	/// <param name="path">The file path.</param>
	/// <param name="entryIdentity">The optional entry identity value.</param>
	/// <param name="fileSystemIdentity">The optional filesystem identity value.</param>
	/// <returns>This provider.</returns>
	internal SyntheticReadOnlyFileSystemProvider AddFile(
		string path,
		string? entryIdentity = null,
		string fileSystemIdentity = "fs-1"
	) => AddNode(
		path,
		FileSystemEntryKind.File,
		entryIdentity,
		fileSystemIdentity,
		null
	);

	/// <summary>
	/// Adds a symbolic link.
	/// </summary>
	/// <param name="path">The link path.</param>
	/// <param name="targetPath">The target path.</param>
	/// <param name="entryIdentity">The optional link identity value.</param>
	/// <param name="fileSystemIdentity">The optional link filesystem identity value.</param>
	/// <returns>This provider.</returns>
	internal SyntheticReadOnlyFileSystemProvider AddLink(
		string path,
		string targetPath,
		string? entryIdentity = null,
		string fileSystemIdentity = "fs-1"
	) => AddNode(
		path,
		FileSystemEntryKind.SymbolicLink,
		entryIdentity,
		fileSystemIdentity,
		targetPath
	);


	/// <summary>
	/// Adds a directory-enumeration result that disappears before it can be observed.
	/// </summary>
	/// <param name="directoryPath">The containing directory.</param>
	/// <param name="childName">The disappearing child basename.</param>
	/// <returns>This provider.</returns>
	internal SyntheticReadOnlyFileSystemProvider AddPhantomChild(
		string directoryPath,
		string childName
	) {
		ArgumentException.ThrowIfNullOrEmpty( childName );
		var directory = GetNode( directoryPath );
		if ( directory.Kind != FileSystemEntryKind.Directory ) {
			throw new IOException( "The synthetic pathname is not a directory." );
		}
		directory.Children.Add( Normalize( Path.Combine( directory.Path, childName ) ) );
		return this;
	}

	/// <summary>
	/// Configures an exception for one pathname observation.
	/// </summary>
	/// <param name="path">The pathname.</param>
	/// <param name="exception">The exception.</param>
	internal void SetObservationException( string path, Exception exception ) {
		ArgumentNullException.ThrowIfNull( exception );
		GetNode( path ).ObservationException = exception;
	}

	/// <summary>
	/// Removes the stable entry identity from one synthetic object.
	/// </summary>
	/// <param name="path">The pathname.</param>
	internal void RemoveEntryIdentity( string path ) =>
		GetNode( path ).EntryIdentity = FileSystemEntryIdentity.Unavailable;

	/// <summary>
	/// Removes the filesystem identity from one synthetic object.
	/// </summary>
	/// <param name="path">The pathname.</param>
	internal void RemoveFileSystemIdentity( string path ) =>
		GetNode( path ).FileSystemIdentity = FileSystemIdentity.Unavailable;

	/// <summary>
	/// Configures an exception for one directory enumeration.
	/// </summary>
	/// <param name="path">The directory path.</param>
	/// <param name="exception">The exception.</param>
	internal void SetEnumerationException( string path, Exception exception ) {
		ArgumentNullException.ThrowIfNull( exception );
		GetNode( path ).EnumerationException = exception;
	}

	/// <inheritdoc/>
	public ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
		string path,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		var normalized = Normalize( path );
		SyntheticNode source;
		bool exact;
		if ( _nodes.TryGetValue( normalized, out var exactSource ) && exactSource is not null ) {
			source = exactSource;
			exact = true;
		} else {
			source = ResolveAliasedNode( normalized, new HashSet<string>( GetPathComparer() ) );
			exact = false;
		}
		if ( source.ObservationException is not null ) {
			throw source.ObservationException;
		}
		if ( !exact || source.Kind != FileSystemEntryKind.SymbolicLink || !followSymbolicLink ) {
			return ValueTask.FromResult( CreateObservation(
				normalized,
				source,
				source,
				false
			) );
		}

		var target = ResolveLinkTarget( source, new HashSet<string>( GetPathComparer() ) );
		return ValueTask.FromResult( CreateObservation( normalized, source, target, true ) );
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<ReadOnlyDirectoryEntry> EnumerateDirectoryAsync(
		string directoryPath,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();
		var normalized = Normalize( directoryPath );
		var directory = ResolveAliasedNode( normalized, new HashSet<string>( GetPathComparer() ) );
		if ( directory.Kind == FileSystemEntryKind.SymbolicLink ) {
			directory = ResolveLinkTarget( directory, new HashSet<string>( GetPathComparer() ) );
		}
		if ( directory.Kind != FileSystemEntryKind.Directory ) {
			throw new IOException( "The synthetic pathname is not a directory." );
		}
		if ( directory.EnumerationException is not null ) {
			throw directory.EnumerationException;
		}
		foreach ( var childPath in directory.Children ) {
			cancellationToken.ThrowIfCancellationRequested();
			var name = Path.GetFileName( Path.TrimEndingDirectorySeparator( childPath ) );
			yield return new ReadOnlyDirectoryEntry(
				name,
				Path.Combine( normalized, name )
			);
		}
	}

	private SyntheticReadOnlyFileSystemProvider AddNode(
		string path,
		FileSystemEntryKind kind,
		string? entryIdentity,
		string fileSystemIdentity,
		string? targetPath
	) {
		var normalized = Normalize( path );
		var node = new SyntheticNode(
			normalized,
			kind,
			new FileSystemEntryIdentity( "synthetic-entry", entryIdentity ?? normalized ),
			new FileSystemIdentity( "synthetic-filesystem", fileSystemIdentity ),
			targetPath is null ? null : Normalize( targetPath )
		);
		_nodes.Add( normalized, node );
		var parent = Path.GetDirectoryName( normalized );
		if ( !string.IsNullOrEmpty( parent ) && _nodes.TryGetValue( Normalize( parent ), out var parentNode ) ) {
			parentNode.Children.Add( normalized );
		}
		return this;
	}

	private ReadOnlyFileSystemEntry CreateObservation(
		string accessPath,
		SyntheticNode source,
		SyntheticNode effective,
		bool wasDereferenced
	) => new(
		accessPath,
		Path.GetFileName( Path.TrimEndingDirectorySeparator( accessPath ) ),
		wasDereferenced ? effective.Kind : source.Kind,
		source.Kind == FileSystemEntryKind.SymbolicLink,
		wasDereferenced,
		source.TargetPath,
		wasDereferenced ? effective.EntryIdentity : source.EntryIdentity,
		wasDereferenced ? effective.FileSystemIdentity : source.FileSystemIdentity
	);

	private SyntheticNode ResolveAliasedNode( string path, ISet<string> activeLinks ) {
		if ( _nodes.TryGetValue( path, out var exact ) ) {
			return exact;
		}

		var ancestor = Path.GetDirectoryName( path );
		while ( !string.IsNullOrEmpty( ancestor ) ) {
			var normalizedAncestor = Normalize( ancestor );
			if (
				_nodes.TryGetValue( normalizedAncestor, out var link )
				&& link.Kind == FileSystemEntryKind.SymbolicLink
			) {
				if ( !activeLinks.Add( link.Path ) ) {
					throw new IOException( "The synthetic symbolic-link chain contains a loop." );
				}
				if ( link.TargetPath is null ) {
					throw new FileNotFoundException( "The synthetic symbolic link has no target.", link.Path );
				}
				var remainder = Path.GetRelativePath( normalizedAncestor, path );
				var targetPath = Normalize( Path.Combine( link.TargetPath, remainder ) );
				return ResolveAliasedNode( targetPath, activeLinks );
			}
			ancestor = Path.GetDirectoryName( normalizedAncestor );
		}
		throw new FileNotFoundException( "The synthetic pathname does not exist.", path );
	}

	private SyntheticNode ResolveLinkTarget(
		SyntheticNode source,
		ISet<string> activeLinks
	) {
		if ( source.TargetPath is null ) {
			throw new FileNotFoundException( "The synthetic symbolic link has no target.", source.Path );
		}
		if ( !activeLinks.Add( source.Path ) ) {
			throw new IOException( "The synthetic symbolic-link chain contains a loop." );
		}
		var target = GetNode( source.TargetPath );
		return target.Kind == FileSystemEntryKind.SymbolicLink
			? ResolveLinkTarget( target, activeLinks )
			: target;
	}

	private SyntheticNode GetNode( string path ) {
		var normalized = Normalize( path );
		return _nodes.TryGetValue( normalized, out var node )
			? node
			: throw new FileNotFoundException( "The synthetic pathname does not exist.", normalized );
	}

	private static string Normalize( string path ) => Path.TrimEndingDirectorySeparator( Path.GetFullPath( path ) );

	private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	/// <summary>
	/// Represents one synthetic filesystem object.
	/// </summary>
	private sealed class SyntheticNode {
		/// <summary>
		/// Initializes a node.
		/// </summary>
		internal SyntheticNode(
			string path,
			FileSystemEntryKind kind,
			FileSystemEntryIdentity entryIdentity,
			FileSystemIdentity fileSystemIdentity,
			string? targetPath
		) {
			Path = path;
			Kind = kind;
			EntryIdentity = entryIdentity;
			FileSystemIdentity = fileSystemIdentity;
			TargetPath = targetPath;
		}

		/// <summary>Gets the operational path.</summary>
		internal string Path { get; }

		/// <summary>Gets the object kind.</summary>
		internal FileSystemEntryKind Kind { get; }

		/// <summary>Gets the entry identity.</summary>
		internal FileSystemEntryIdentity EntryIdentity { get; set; }

		/// <summary>Gets the filesystem identity.</summary>
		internal FileSystemIdentity FileSystemIdentity { get; set; }

		/// <summary>Gets the link target.</summary>
		internal string? TargetPath { get; }

		/// <summary>Gets the child paths in provider order.</summary>
		internal IList<string> Children { get; } = new List<string>();

		/// <summary>Gets or sets an observation failure.</summary>
		internal Exception? ObservationException { get; set; }

		/// <summary>Gets or sets an enumeration failure.</summary>
		internal Exception? EnumerationException { get; set; }
	}
}
