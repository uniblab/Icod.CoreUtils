namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Identifies a traversal event phase.
/// </summary>
public enum PathTraversalEventKind {
	/// <summary>A traversal root is beginning.</summary>
	Root = 0,
	/// <summary>A selected directory is entered in preorder.</summary>
	EnterDirectory = 1,
	/// <summary>A selected nondirectory entry is encountered.</summary>
	Entry = 2,
	/// <summary>A selected directory is left in postorder.</summary>
	LeaveDirectory = 3,
	/// <summary>A structured provider or policy error occurred.</summary>
	Error = 4,
	/// <summary>Following a directory would revisit an identity in the active ancestry.</summary>
	Cycle = 5,
	/// <summary>Descending would cross the configured root filesystem boundary.</summary>
	FileSystemBoundary = 6
}

/// <summary>
/// Represents one root, entry, postorder, error, cycle, or boundary traversal event.
/// </summary>
public sealed class PathTraversalEvent {
	private PathTraversalEvent(
		PathTraversalEventKind kind,
		PathTraversalRoot root,
		PathTraversalEntry? entry,
		PathTraversalError? error,
		string? relatedPath
	) {
		Kind = kind;
		Root = root;
		Entry = entry;
		Error = error;
		RelatedPath = relatedPath;
	}

	/// <summary>Gets the event kind.</summary>
	public PathTraversalEventKind Kind { get; }

	/// <summary>Gets the associated traversal root.</summary>
	public PathTraversalRoot Root { get; }

	/// <summary>Gets the associated entry, when applicable.</summary>
	public PathTraversalEntry? Entry { get; }

	/// <summary>Gets the structured error, when applicable.</summary>
	public PathTraversalError? Error { get; }

	/// <summary>Gets a related pathname, such as the active ancestor that forms a cycle.</summary>
	public string? RelatedPath { get; }

	/// <summary>Creates a root event.</summary>
	/// <param name="root">The traversal root.</param>
	/// <returns>The event.</returns>
	public static PathTraversalEvent CreateRoot( PathTraversalRoot root ) {
		ArgumentNullException.ThrowIfNull( root );
		return new PathTraversalEvent( PathTraversalEventKind.Root, root, null, null, null );
	}

	/// <summary>Creates an entry-phase event.</summary>
	/// <param name="kind">The entry-phase kind.</param>
	/// <param name="entry">The traversal entry.</param>
	/// <returns>The event.</returns>
	public static PathTraversalEvent CreateEntry(
		PathTraversalEventKind kind,
		PathTraversalEntry entry
	) {
		ArgumentNullException.ThrowIfNull( entry );
		if (
			kind != PathTraversalEventKind.EnterDirectory
			&& kind != PathTraversalEventKind.Entry
			&& kind != PathTraversalEventKind.LeaveDirectory
			&& kind != PathTraversalEventKind.FileSystemBoundary
		) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		return new PathTraversalEvent( kind, entry.Root, entry, null, null );
	}

	/// <summary>Creates a cycle event.</summary>
	/// <param name="entry">The entry whose descent would cycle.</param>
	/// <param name="ancestorPath">The active ancestor path with the same identity.</param>
	/// <returns>The event.</returns>
	public static PathTraversalEvent CreateCycle(
		PathTraversalEntry entry,
		string ancestorPath
	) {
		ArgumentNullException.ThrowIfNull( entry );
		ArgumentException.ThrowIfNullOrEmpty( ancestorPath );
		return new PathTraversalEvent(
			PathTraversalEventKind.Cycle,
			entry.Root,
			entry,
			null,
			ancestorPath
		);
	}

	/// <summary>Creates an error event.</summary>
	/// <param name="error">The structured error.</param>
	/// <returns>The event.</returns>
	public static PathTraversalEvent CreateError( PathTraversalError error ) {
		ArgumentNullException.ThrowIfNull( error );
		if ( error.Root is null ) {
			throw new ArgumentException( "A traversal error event requires an established root.", nameof( error ) );
		}
		return new PathTraversalEvent(
			PathTraversalEventKind.Error,
			error.Root,
			null,
			error,
			null
		);
	}
}
