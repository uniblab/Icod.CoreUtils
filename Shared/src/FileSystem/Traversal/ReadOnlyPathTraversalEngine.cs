using System.Runtime.CompilerServices;

namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Performs iterative, event-based, read-only pathname traversal over an injectable provider.
/// </summary>
public sealed class ReadOnlyPathTraversalEngine {
	private readonly IReadOnlyFileSystemProvider _provider;

	/// <summary>
	/// Initializes a traversal engine.
	/// </summary>
	/// <param name="provider">The one-level filesystem provider.</param>
	public ReadOnlyPathTraversalEngine( IReadOnlyFileSystemProvider provider ) {
		ArgumentNullException.ThrowIfNull( provider );
		_provider = provider;
	}

	/// <summary>
	/// Traverses an asynchronous root stream without materializing all expanded roots.
	/// </summary>
	/// <param name="roots">The asynchronous provenance-preserving roots.</param>
	/// <param name="options">The traversal options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The asynchronous traversal event stream.</returns>
	public async IAsyncEnumerable<PathTraversalEvent> TraverseAsync(
		IAsyncEnumerable<PathTraversalRoot> roots,
		PathTraversalOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( roots );
		options ??= PathTraversalOptions.Default;
		options.Validate();
		await foreach ( var root in roots.WithCancellation( cancellationToken ).ConfigureAwait( false ) ) {
			await foreach ( var item in TraverseAsync(
				new[] { root },
				options,
				cancellationToken
			).ConfigureAwait( false ) ) {
				yield return item;
				if (
					options.ErrorMode == PathTraversalErrorMode.Stop
					&& item.Kind == PathTraversalEventKind.Error
				) {
					yield break;
				}
			}
		}
	}

	/// <summary>
	/// Traverses roots in their supplied order.
	/// </summary>
	/// <param name="roots">The provenance-preserving roots.</param>
	/// <param name="options">The traversal options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The asynchronous traversal event stream.</returns>
	public async IAsyncEnumerable<PathTraversalEvent> TraverseAsync(
		IEnumerable<PathTraversalRoot> roots,
		PathTraversalOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( roots );
		options ??= PathTraversalOptions.Default;
		options.Validate();

		foreach ( var root in roots ) {
			ArgumentNullException.ThrowIfNull( root );
			cancellationToken.ThrowIfCancellationRequested();
			yield return PathTraversalEvent.CreateRoot( root );

			ReadOnlyFileSystemEntry? rootObservation = null;
			PathTraversalError? rootObservationError = null;
			try {
				var rootDereferenceMode = options.SymbolicLinkMode is SymbolicLinkTraversalMode.RootsOnly
					or SymbolicLinkTraversalMode.Always
					? PathDereferenceMode.FollowEligiblePathIndirection
					: PathDereferenceMode.NoFollow;
				rootObservation = await _provider.ObserveAsync(
					root.AccessPath,
					rootDereferenceMode,
					cancellationToken
				).ConfigureAwait( false );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				throw;
			} catch ( Exception exception ) {
				rootObservationError = CreateError(
					PathTraversalErrorCode.ObservationFailed,
					root,
					root.AccessPath,
					PathTraversalOperationStage.ObserveRoot,
					PathTraversalErrorScope.Root,
					"The traversal root could not be observed.",
					exception
				);
			}
			if ( rootObservationError is not null ) {
				cancellationToken.ThrowIfCancellationRequested();
				yield return PathTraversalEvent.CreateError( rootObservationError );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				continue;
			}

			var rootEntry = CreateRootEntry( root, rootObservation! );
			var rootSelection = options.Selector.Select( rootEntry );
			if ( rootEntry.Kind != FileSystemEntryKind.Directory ) {
				if ( rootSelection.Yield ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.Entry, rootEntry );
				}
				continue;
			}

			if ( rootSelection.Yield ) {
				cancellationToken.ThrowIfCancellationRequested();
				yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.EnterDirectory, rootEntry );
			}
			if ( !rootSelection.Descend || options.MaximumDepth == 0 ) {
				if ( rootSelection.Yield ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.LeaveDirectory, rootEntry );
				}
				continue;
			}

			if (
				options.FileSystemBoundaryMode == FileSystemBoundaryMode.StayOnRootFileSystem
				&& !rootEntry.FileSystemIdentity.IsAvailable
			) {
				var error = CreateError(
					PathTraversalErrorCode.IdentityUnavailable,
					root,
					rootEntry.AccessPath,
					PathTraversalOperationStage.ReadIdentity,
					PathTraversalErrorScope.Root,
					"A filesystem identity is required to enforce the root boundary."
				);
				cancellationToken.ThrowIfCancellationRequested();
				yield return PathTraversalEvent.CreateError( error );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				if ( rootSelection.Yield ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.LeaveDirectory, rootEntry );
				}
				continue;
			}

			if ( !rootEntry.EntryIdentity.IsAvailable ) {
				var error = CreateError(
					PathTraversalErrorCode.IdentityUnavailable,
					root,
					rootEntry.AccessPath,
					PathTraversalOperationStage.ReadIdentity,
					PathTraversalErrorScope.Root,
					"A stable directory identity is required for cycle-safe descent."
				);
				cancellationToken.ThrowIfCancellationRequested();
				yield return PathTraversalEvent.CreateError( error );
				if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
					yield break;
				}
				if ( rootSelection.Yield ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.LeaveDirectory, rootEntry );
				}
				continue;
			}

			var ancestry = new Dictionary<FileSystemEntryIdentity, string>();
			if ( rootEntry.EntryIdentity.IsAvailable ) {
				ancestry.Add( rootEntry.EntryIdentity, rootEntry.AccessPath );
			}
			var frames = new Stack<DirectoryTraversalFrame>();
			frames.Push( new DirectoryTraversalFrame( rootEntry, rootSelection.Yield ) );

			while ( frames.Count > 0 ) {
				cancellationToken.ThrowIfCancellationRequested();
				var frame = frames.Peek();
				if ( !frame.IsLoaded ) {
					PathTraversalError? loadError = null;
					try {
						await frame.LoadChildrenAsync(
							_provider,
							options,
							cancellationToken
						).ConfigureAwait( false );
					} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
						throw;
					} catch ( DirectoryEntryLimitException exception ) {
						loadError = CreateError(
							PathTraversalErrorCode.DirectoryEntryLimitExceeded,
							root,
							frame.Entry.AccessPath,
							PathTraversalOperationStage.EnumerateDirectory,
							PathTraversalErrorScope.Subtree,
							"The configured directory-entry limit was exceeded.",
							exception
						);
					} catch ( Exception exception ) {
						loadError = CreateError(
							PathTraversalErrorCode.EnumerationFailed,
							root,
							frame.Entry.AccessPath,
							PathTraversalOperationStage.EnumerateDirectory,
							PathTraversalErrorScope.Subtree,
							"The directory could not be enumerated.",
							exception
						);
					}
					if ( loadError is not null ) {
						cancellationToken.ThrowIfCancellationRequested();
						yield return PathTraversalEvent.CreateError( loadError );
						if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
							yield break;
						}
						PopFrame( frames, ancestry );
						if ( frame.WasYielded ) {
							cancellationToken.ThrowIfCancellationRequested();
							yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.LeaveDirectory, frame.Entry );
						}
						continue;
					}
				}

				if ( !frame.TryTakeNext( out var child ) ) {
					PopFrame( frames, ancestry );
					if ( frame.WasYielded ) {
						cancellationToken.ThrowIfCancellationRequested();
						yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.LeaveDirectory, frame.Entry );
					}
					continue;
				}

				ReadOnlyFileSystemEntry? observation = null;
				PathTraversalError? observationError = null;
				try {
					var childDereferenceMode = options.SymbolicLinkMode == SymbolicLinkTraversalMode.Always
						? PathDereferenceMode.FollowEligiblePathIndirection
						: PathDereferenceMode.NoFollow;
					observation = await _provider.ObserveAsync(
						child.AccessPath,
						childDereferenceMode,
						cancellationToken
					).ConfigureAwait( false );
				} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
					throw;
				} catch ( Exception exception ) {
					observationError = CreateError(
						PathTraversalErrorCode.ObservationFailed,
						root,
						child.AccessPath,
						PathTraversalOperationStage.ObserveEntry,
						PathTraversalErrorScope.Entry,
						"The directory entry could not be observed.",
						exception
					);
				}
				if ( observationError is not null ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateError( observationError );
					if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
						yield break;
					}
					continue;
				}

				PathTraversalEntry? childEntry = null;
				PathTraversalError? childEntryError = null;
				try {
					childEntry = CreateChildEntry( frame.Entry, child, observation! );
				} catch ( Exception exception ) when (
					exception is ArgumentException
					or NotSupportedException
					or PathTooLongException
				) {
					childEntryError = CreateError(
						PathTraversalErrorCode.InvalidPath,
						root,
						child.AccessPath,
						PathTraversalOperationStage.ObserveEntry,
						PathTraversalErrorScope.Entry,
						"The entry pathname could not be represented.",
						exception
					);
				}
				if ( childEntryError is not null ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateError( childEntryError );
					if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
						yield break;
					}
					continue;
				}

				var selectedEntry = childEntry!;
				var selection = options.Selector.Select( selectedEntry );
				if ( selectedEntry.Kind != FileSystemEntryKind.Directory ) {
					if ( selection.Yield ) {
						cancellationToken.ThrowIfCancellationRequested();
						yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.Entry, selectedEntry );
					}
					continue;
				}

				var depthLimitReached = options.MaximumDepth is int maximumDepth
					&& selectedEntry.Depth >= maximumDepth;
				if ( !selection.Descend || depthLimitReached ) {
					if ( selection.Yield ) {
						cancellationToken.ThrowIfCancellationRequested();
						yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.EnterDirectory, selectedEntry );
						yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.LeaveDirectory, selectedEntry );
					}
					continue;
				}

				if (
					options.FileSystemBoundaryMode == FileSystemBoundaryMode.StayOnRootFileSystem
					&& !CanRemainOnRootFileSystem( rootEntry.FileSystemIdentity, selectedEntry.FileSystemIdentity )
				) {
					if ( !selectedEntry.FileSystemIdentity.IsAvailable ) {
						var error = CreateError(
							PathTraversalErrorCode.IdentityUnavailable,
							root,
							selectedEntry.AccessPath,
							PathTraversalOperationStage.ReadIdentity,
							PathTraversalErrorScope.Subtree,
							"A filesystem identity is required to enforce the root boundary."
						);
						cancellationToken.ThrowIfCancellationRequested();
						yield return PathTraversalEvent.CreateError( error );
						if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
							yield break;
						}
					} else {
						cancellationToken.ThrowIfCancellationRequested();
						yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.FileSystemBoundary, selectedEntry );
					}
					continue;
				}

				if (
					selectedEntry.EntryIdentity.IsAvailable
					&& ancestry.TryGetValue( selectedEntry.EntryIdentity, out var ancestorPath )
				) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateCycle( selectedEntry, ancestorPath );
					continue;
				}

				if ( !selectedEntry.EntryIdentity.IsAvailable ) {
					var error = CreateError(
						PathTraversalErrorCode.IdentityUnavailable,
						root,
						selectedEntry.AccessPath,
						PathTraversalOperationStage.ReadIdentity,
						PathTraversalErrorScope.Subtree,
						"A stable directory identity is required for cycle-safe descent."
					);
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateError( error );
					if ( options.ErrorMode == PathTraversalErrorMode.Stop ) {
						yield break;
					}
					continue;
				}

				if ( selection.Yield ) {
					cancellationToken.ThrowIfCancellationRequested();
					yield return PathTraversalEvent.CreateEntry( PathTraversalEventKind.EnterDirectory, selectedEntry );
				}
				if ( selectedEntry.EntryIdentity.IsAvailable ) {
					ancestry.Add( selectedEntry.EntryIdentity, selectedEntry.AccessPath );
				}
				frames.Push( new DirectoryTraversalFrame( selectedEntry, selection.Yield ) );
			}
		}
	}

	private static PathTraversalEntry CreateRootEntry(
		PathTraversalRoot root,
		ReadOnlyFileSystemEntry observation
	) => new(
		root,
		root.AccessPath,
		root.DisplayPath,
		string.Empty,
		observation.Name,
		0,
		observation.Kind,
		observation.IsSymbolicLink,
		observation.WasDereferenced,
		observation.LinkTarget,
		observation.EntryIdentity,
		observation.FileSystemIdentity,
		observation.Indirection
	);

	private static PathTraversalEntry CreateChildEntry(
		PathTraversalEntry parent,
		ReadOnlyDirectoryEntry child,
		ReadOnlyFileSystemEntry observation
	) {
		var relativePath = parent.RelativePath.Length == 0
			? child.Name
			: Path.Combine( parent.RelativePath, child.Name );
		var displayPath = Path.Combine( parent.DisplayPath, child.Name );
		return new PathTraversalEntry(
			parent.Root,
			child.AccessPath,
			displayPath,
			relativePath,
			child.Name,
			checked(parent.Depth + 1),
			observation.Kind,
			observation.IsSymbolicLink,
			observation.WasDereferenced,
			observation.LinkTarget,
			observation.EntryIdentity,
			observation.FileSystemIdentity,
			observation.Indirection
		);
	}

	private static bool CanRemainOnRootFileSystem(
		FileSystemIdentity rootIdentity,
		FileSystemIdentity entryIdentity
	) => rootIdentity.IsAvailable
		&& entryIdentity.IsAvailable
		&& rootIdentity == entryIdentity;

	private static PathTraversalError CreateError(
		PathTraversalErrorCode code,
		PathTraversalRoot root,
		string path,
		PathTraversalOperationStage stage,
		PathTraversalErrorScope scope,
		string message,
		Exception? exception = null
	) => new( code, root, path, stage, scope, message, exception );

	private static void PopFrame(
		Stack<DirectoryTraversalFrame> frames,
		IDictionary<FileSystemEntryIdentity, string> ancestry
	) {
		var frame = frames.Pop();
		if ( frame.Entry.EntryIdentity.IsAvailable ) {
			ancestry.Remove( frame.Entry.EntryIdentity );
		}
	}
}

/// <summary>
/// Retains the bounded one-directory child set and cursor for iterative depth-first traversal.
/// </summary>
internal sealed class DirectoryTraversalFrame {
	private IReadOnlyList<ReadOnlyDirectoryEntry> _children = Array.Empty<ReadOnlyDirectoryEntry>();
	private int _nextIndex;

	/// <summary>
	/// Initializes a directory frame.
	/// </summary>
	/// <param name="entry">The directory entry.</param>
	/// <param name="wasYielded">Whether preorder was exposed.</param>
	internal DirectoryTraversalFrame( PathTraversalEntry entry, bool wasYielded ) {
		Entry = entry;
		WasYielded = wasYielded;
	}

	/// <summary>Gets the directory entry.</summary>
	internal PathTraversalEntry Entry { get; }

	/// <summary>Gets whether preorder was exposed.</summary>
	internal bool WasYielded { get; }

	/// <summary>Gets whether children have been loaded.</summary>
	internal bool IsLoaded { get; private set; }

	/// <summary>
	/// Loads one directory level and applies the configured child ordering.
	/// </summary>
	/// <param name="provider">The provider.</param>
	/// <param name="options">The traversal options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing completion.</returns>
	internal async ValueTask LoadChildrenAsync(
		IReadOnlyFileSystemProvider provider,
		PathTraversalOptions options,
		CancellationToken cancellationToken
	) {
		var children = new List<ReadOnlyDirectoryEntry>();
		await foreach ( var child in provider.EnumerateDirectoryAsync( Entry.AccessPath, cancellationToken )
			.WithCancellation( cancellationToken )
			.ConfigureAwait( false ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( children.Count >= options.MaximumEntriesPerDirectory ) {
				throw new DirectoryEntryLimitException( Entry.AccessPath, options.MaximumEntriesPerDirectory );
			}
			children.Add( child );
		}

		Comparison<ReadOnlyDirectoryEntry>? comparison = options.ChildOrder switch {
			PathTraversalChildOrder.Ordinal => static ( left, right ) => string.CompareOrdinal( left.Name, right.Name ),
			PathTraversalChildOrder.OrdinalIgnoreCase => static ( left, right ) => {
				var result = StringComparer.OrdinalIgnoreCase.Compare( left.Name, right.Name );
				return result != 0 ? result : StringComparer.Ordinal.Compare( left.Name, right.Name );
			},
			_ => null
		};
		if ( comparison is not null ) {
			children.Sort( comparison );
		}
		_children = children;
		IsLoaded = true;
	}

	/// <summary>
	/// Takes the next child.
	/// </summary>
	/// <param name="child">The child when available.</param>
	/// <returns><see langword="true"/> when a child was returned.</returns>
	internal bool TryTakeNext( out ReadOnlyDirectoryEntry child ) {
		if ( _nextIndex >= _children.Count ) {
			child = null!;
			return false;
		}
		child = _children[_nextIndex++];
		return true;
	}
}

/// <summary>
/// Indicates that a one-directory enumeration exceeded the configured retained-entry limit.
/// </summary>
internal sealed class DirectoryEntryLimitException : Exception {
	/// <summary>
	/// Initializes the exception.
	/// </summary>
	/// <param name="path">The directory pathname.</param>
	/// <param name="limit">The configured limit.</param>
	internal DirectoryEntryLimitException( string path, int limit )
		: base( string.Concat( "Directory '", path, "' exceeded the entry limit ", limit, "." ) ) {
		Path = path;
		Limit = limit;
	}

	/// <summary>Gets the directory pathname.</summary>
	internal string Path { get; }

	/// <summary>Gets the configured limit.</summary>
	internal int Limit { get; }
}
