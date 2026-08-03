using System.Runtime.CompilerServices;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;

/// <summary>
/// Extends the E1 event stream with preserve-root, containment, stable preconditions, and hard-link identity state.
/// </summary>
public sealed class RecursiveMutationTraversalEngine {
	private readonly ReadOnlyPathTraversalEngine _traversal;
	private readonly RecursivePathSafety _pathSafety;

	/// <summary>Initializes a mutation-aware traversal layer over the E1 provider.</summary>
	/// <param name="provider">The existing E1 one-level filesystem provider.</param>
	/// <param name="pathSafety">Optional preserve-root and containment preflight.</param>
	public RecursiveMutationTraversalEngine(
		IReadOnlyFileSystemProvider provider,
		RecursivePathSafety? pathSafety = null
	) {
		ArgumentNullException.ThrowIfNull( provider );
		_traversal = new ReadOnlyPathTraversalEngine( provider );
		_pathSafety = pathSafety ?? new RecursivePathSafety();
	}

	/// <summary>Traverses an asynchronous root stream while retaining E1 provenance and preparing E4 preconditions.</summary>
	/// <param name="roots">The asynchronous provenance-preserving roots.</param>
	/// <param name="options">The recursive mutation options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The asynchronous mutation-aware event stream.</returns>
	public async IAsyncEnumerable<RecursiveMutationEvent> TraverseAsync(
		IAsyncEnumerable<PathTraversalRoot> roots,
		RecursiveMutationOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( roots );
		options ??= RecursiveMutationOptions.Default;
		options.Validate();
		var hardLinks = new HardLinkIdentityTracker();
		await foreach ( var root in roots.WithCancellation( cancellationToken ).ConfigureAwait( false ) ) {
			ArgumentNullException.ThrowIfNull( root );
			await foreach ( var item in TraverseRootAsync( root, options, hardLinks, cancellationToken ).ConfigureAwait( false ) ) {
				yield return item;
				if ( options.ErrorMode == PathTraversalErrorMode.Stop && item.Kind == RecursiveMutationEventKind.Error ) {
					yield break;
				}
			}
		}
	}

	/// <summary>Traverses roots while retaining E1 provenance and preparing E4 preconditions.</summary>
	/// <param name="roots">The provenance-preserving roots.</param>
	/// <param name="options">The recursive mutation options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The asynchronous mutation-aware event stream.</returns>
	public async IAsyncEnumerable<RecursiveMutationEvent> TraverseAsync(
		IEnumerable<PathTraversalRoot> roots,
		RecursiveMutationOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( roots );
		options ??= RecursiveMutationOptions.Default;
		options.Validate();
		var hardLinks = new HardLinkIdentityTracker();
		foreach ( var root in roots ) {
			ArgumentNullException.ThrowIfNull( root );
			cancellationToken.ThrowIfCancellationRequested();
			await foreach ( var item in TraverseRootAsync( root, options, hardLinks, cancellationToken ).ConfigureAwait( false ) ) {
				yield return item;
				if ( options.ErrorMode == PathTraversalErrorMode.Stop && item.Kind == RecursiveMutationEventKind.Error ) {
					yield break;
				}
			}
		}
	}

	private async IAsyncEnumerable<RecursiveMutationEvent> TraverseRootAsync(
		PathTraversalRoot root,
		RecursiveMutationOptions options,
		HardLinkIdentityTracker hardLinks,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		var preflightError = await ValidateRootAsync( root, options, cancellationToken ).ConfigureAwait( false );
		if ( preflightError is not null ) {
			yield return RecursiveMutationEvent.CreateRoot( root );
			yield return RecursiveMutationEvent.CreateError( preflightError );
			yield break;
		}
		await foreach ( var traversalEvent in _traversal.TraverseAsync(
			new[] { root },
			options.CreateTraversalOptions(),
			cancellationToken
		).ConfigureAwait( false ) ) {
			yield return MapEvent( traversalEvent, options, hardLinks );
		}
	}

	private async ValueTask<RecursiveMutationError?> ValidateRootAsync(
		PathTraversalRoot root,
		RecursiveMutationOptions options,
		CancellationToken cancellationToken
	) {
		try {
			var safety = await _pathSafety.EvaluateAsync(
				root.AccessPath,
				options.DestinationPath,
				cancellationToken
			).ConfigureAwait( false );
			if ( !safety.Succeeded ) {
				return new RecursiveMutationError(
					RecursiveMutationErrorCode.TraversalFailed,
					RecursiveMutationStage.Preflight,
					PathTraversalErrorScope.Root,
					root,
					root.AccessPath,
					safety.Message ?? "The source or destination pathname could not be resolved.",
					safety.Exception
				);
			}
			if ( options.PreserveRoot && safety.IsSourceRoot ) {
				return new RecursiveMutationError(
					RecursiveMutationErrorCode.PreservedRoot,
					RecursiveMutationStage.Preflight,
					PathTraversalErrorScope.Root,
					root,
					root.AccessPath,
					"The recursive operation was refused because the operand resolves to a filesystem root."
				);
			}
			if ( safety.Relationship is RecursivePathRelationship.Same or RecursivePathRelationship.DestinationInsideSource ) {
				return new RecursiveMutationError(
					RecursiveMutationErrorCode.DestinationInsideSource,
					RecursiveMutationStage.Preflight,
					PathTraversalErrorScope.Root,
					root,
					options.DestinationPath!,
					"The destination is the source or resolves inside the source."
				);
			}
			return null;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) when (
			exception is ArgumentException
			or NotSupportedException
			or IOException
			or UnauthorizedAccessException
		) {
			return new RecursiveMutationError(
				RecursiveMutationErrorCode.TraversalFailed,
				RecursiveMutationStage.Preflight,
				PathTraversalErrorScope.Root,
				root,
				root.AccessPath,
				"The source or destination pathname could not be normalized.",
				exception
			);
		}
	}

	private static RecursiveMutationEvent MapEvent(
		PathTraversalEvent traversalEvent,
		RecursiveMutationOptions options,
		HardLinkIdentityTracker hardLinks
	) {
		ArgumentNullException.ThrowIfNull( traversalEvent );
		if ( traversalEvent.Kind == PathTraversalEventKind.Root ) {
			return RecursiveMutationEvent.CreateRoot( traversalEvent.Root );
		}
		if ( traversalEvent.Kind == PathTraversalEventKind.Error ) {
			var error = traversalEvent.Error!;
			return RecursiveMutationEvent.CreateError( new RecursiveMutationError(
				RecursiveMutationErrorCode.TraversalFailed,
				RecursiveMutationStage.Traversal,
				error.Scope,
				traversalEvent.Root,
				error.Path,
				error.Message,
				error.Exception,
				error
			) );
		}
		var entry = traversalEvent.Entry!;
		if ( options.RequireStableEntryIdentity && !entry.EntryIdentity.IsAvailable ) {
			return RecursiveMutationEvent.CreateError( new RecursiveMutationError(
				RecursiveMutationErrorCode.IdentityUnavailable,
				RecursiveMutationStage.Traversal,
				PathTraversalErrorScope.Entry,
				entry.Root,
				entry.AccessPath,
				"A stable entry identity is required for race-aware recursive mutation."
			) );
		}
		var dereferenceMode = entry.WasDereferenced
			? PathDereferenceMode.FollowEligiblePathIndirection
			: PathDereferenceMode.NoFollow;
		var precondition = FileSystemMutationPrecondition.FromObservation(
			entry.Kind,
			entry.EntryIdentity,
			dereferenceMode
		);
		var destinationPath = options.DestinationPath is null
			? null
			: entry.RelativePath.Length == 0
				? options.DestinationPath
				: Path.Combine( options.DestinationPath, entry.RelativePath );
		HardLinkIdentityAnchor? firstHardLink = null;
		if ( entry.Kind != FileSystemEntryKind.Directory ) {
			_ = hardLinks.Track(
				entry.EntryIdentity,
				entry.AccessPath,
				destinationPath,
				out firstHardLink
			);
		}
		var recursiveEntry = new RecursiveMutationEntry(
			entry,
			precondition,
			destinationPath,
			firstHardLink
		);
		return traversalEvent.Kind switch {
			PathTraversalEventKind.EnterDirectory => RecursiveMutationEvent.CreateEntry(
				RecursiveMutationEventKind.EnterDirectory,
				recursiveEntry
			),
			PathTraversalEventKind.Entry => RecursiveMutationEvent.CreateEntry(
				RecursiveMutationEventKind.Entry,
				recursiveEntry
			),
			PathTraversalEventKind.LeaveDirectory => RecursiveMutationEvent.CreateEntry(
				RecursiveMutationEventKind.LeaveDirectory,
				recursiveEntry
			),
			PathTraversalEventKind.Cycle => RecursiveMutationEvent.CreateEntry(
				RecursiveMutationEventKind.Cycle,
				recursiveEntry,
				traversalEvent.RelatedPath
			),
			PathTraversalEventKind.FileSystemBoundary => RecursiveMutationEvent.CreateEntry(
				RecursiveMutationEventKind.FileSystemBoundary,
				recursiveEntry,
				traversalEvent.RelatedPath
			),
			_ => throw new InvalidOperationException( "The E1 traversal event kind is not supported." )
		};
	}
}
