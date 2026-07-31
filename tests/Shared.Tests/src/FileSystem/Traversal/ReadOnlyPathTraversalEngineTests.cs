using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>
/// Tests iterative read-only traversal policy and event behavior.
/// </summary>
public sealed class ReadOnlyPathTraversalEngineTests {
	/// <summary>
	/// Verifies deterministic preorder and postorder phases with preserved provenance.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EmitsPreorderEntriesAndPostorderDirectoryExit() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddFile( Path.Combine( paths.Root, "z.txt" ) )
			.AddDirectory( Path.Combine( paths.Root, "a" ) )
			.AddFile( Path.Combine( paths.Root, "a", "inside.txt" ) );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root, "operand-root" ) },
			new PathTraversalOptions { ChildOrder = PathTraversalChildOrder.Ordinal }
		) );

		Assert.Equal(
			new[] {
				PathTraversalEventKind.Root,
				PathTraversalEventKind.EnterDirectory,
				PathTraversalEventKind.EnterDirectory,
				PathTraversalEventKind.Entry,
				PathTraversalEventKind.LeaveDirectory,
				PathTraversalEventKind.Entry,
				PathTraversalEventKind.LeaveDirectory
			},
			events.Select( static item => item.Kind )
		);
		Assert.All(
			events,
			item => Assert.Equal( "operand-root", item.Root.OriginalOperand )
		);
		Assert.Equal( Path.Combine( "a", "inside.txt" ), events[3].Entry!.RelativePath );
	}

	/// <summary>
	/// Verifies that yielding and directory pruning are independent decisions.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SelectorCanYieldDirectoryWhilePruningItsChildren() {
		var paths = CreatePaths();
		var skipped = Path.Combine( paths.Root, "skip" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddDirectory( skipped )
			.AddFile( Path.Combine( skipped, "hidden.txt" ) )
			.AddFile( Path.Combine( paths.Root, "visible.txt" ) );
		var selector = new PathTraversalRuleSelector(
			new[] {
				new PathTraversalFilterRule(
					PathnamePattern.Parse( "skip" ),
					PathMatchScope.BaseName,
					PathTraversalRuleAction.Exclude,
					PathTraversalRuleTarget.Descend,
					PathTraversalRuleEntryKind.Directories
				)
			},
			PathTraversalSelection.IncludeAll
		);
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions {
				ChildOrder = PathTraversalChildOrder.Ordinal,
				Selector = selector
			}
		) );

		Assert.Contains( events, item => item.Kind == PathTraversalEventKind.EnterDirectory && item.Entry!.Name == "skip" );
		Assert.Contains( events, item => item.Kind == PathTraversalEventKind.LeaveDirectory && item.Entry!.Name == "skip" );
		Assert.DoesNotContain( events, item => item.Entry?.Name == "hidden.txt" );
		Assert.Contains( events, item => item.Entry?.Name == "visible.txt" );
	}

	/// <summary>
	/// Verifies root-only and all-link traversal semantics.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DistinguishesRootOnlyAndDescendantLinkFollowing() {
		var paths = CreatePaths();
		var target = Path.Combine( paths.Base, "target" );
		var link = Path.Combine( paths.Root, "link" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Base )
			.AddDirectory( paths.Root )
			.AddDirectory( target )
			.AddFile( Path.Combine( target, "through-link.txt" ) )
			.AddLink( link, target );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var rootsOnly = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions { SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly }
		) );
		Assert.DoesNotContain( rootsOnly, item => item.Entry?.Name == "through-link.txt" );
		Assert.Contains( rootsOnly, item => item.Entry?.Name == "link" && item.Entry.Kind == FileSystemEntryKind.SymbolicLink );

		var always = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions { SymbolicLinkMode = SymbolicLinkTraversalMode.Always }
		) );
		Assert.Contains( always, item => item.Entry?.Name == "through-link.txt" );
		Assert.Contains( always, item => item.Entry?.Name == "link" && item.Entry.IsFollowedSymbolicLink );
	}

	/// <summary>
	/// Verifies active-ancestry cycle detection without global identity deduplication.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DetectsAncestorCycleButAllowsIndependentRepeatedIdentity() {
		var paths = CreatePaths();
		var first = Path.Combine( paths.Root, "first" );
		var second = Path.Combine( paths.Root, "second" );
		var up = Path.Combine( first, "up" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root, "root-id" )
			.AddDirectory( first, "shared-id" )
			.AddLink( up, paths.Root )
			.AddDirectory( second, "shared-id" );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions {
				ChildOrder = PathTraversalChildOrder.Ordinal,
				SymbolicLinkMode = SymbolicLinkTraversalMode.Always
			}
		) );

		var cycle = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.Cycle );
		Assert.Equal( paths.Root, cycle.RelatedPath );
		Assert.Contains( events, item => item.Kind == PathTraversalEventKind.EnterDirectory && item.Entry?.Name == "first" );
		Assert.Contains( events, item => item.Kind == PathTraversalEventKind.EnterDirectory && item.Entry?.Name == "second" );
	}

	/// <summary>
	/// Verifies filesystem-boundary events and skipped descent.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task StopsAtRootFileSystemBoundary() {
		var paths = CreatePaths();
		var mounted = Path.Combine( paths.Root, "mounted" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root, fileSystemIdentity: "fs-root" )
			.AddDirectory( mounted, fileSystemIdentity: "fs-other" )
			.AddFile( Path.Combine( mounted, "inside.txt" ), fileSystemIdentity: "fs-other" );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions {
				FileSystemBoundaryMode = FileSystemBoundaryMode.StayOnRootFileSystem
			}
		) );

		var boundary = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.FileSystemBoundary );
		Assert.Equal( "mounted", boundary.Entry!.Name );
		Assert.DoesNotContain( events, item => item.Entry?.Name == "inside.txt" );
	}

	/// <summary>
	/// Verifies structured continuation after a subtree enumeration failure.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsEnumerationErrorAndContinuesWithSibling() {
		var paths = CreatePaths();
		var bad = Path.Combine( paths.Root, "bad" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddDirectory( bad )
			.AddFile( Path.Combine( paths.Root, "good.txt" ) );
		provider.SetEnumerationException( bad, new UnauthorizedAccessException( "synthetic" ) );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions { ChildOrder = PathTraversalChildOrder.Ordinal }
		) );

		var error = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.Error );
		Assert.Equal( PathTraversalErrorCode.EnumerationFailed, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Subtree, error.Error.Scope );
		Assert.Contains( events, item => item.Entry?.Name == "good.txt" );
	}


	/// <summary>
	/// Verifies that root-only mode follows a link supplied as a traversal root.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RootsOnlyFollowsLinkedRoot() {
		var paths = CreatePaths();
		var target = Path.Combine( paths.Base, "target" );
		var linkedRoot = Path.Combine( paths.Base, "linked-root" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Base )
			.AddDirectory( target )
			.AddFile( Path.Combine( target, "inside.txt" ) )
			.AddLink( linkedRoot, target );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( linkedRoot, "linked-root" ) },
			new PathTraversalOptions { SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly }
		) );

		Assert.Contains( events, item => item.Kind == PathTraversalEventKind.EnterDirectory && item.Entry!.IsRoot && item.Entry.IsFollowedSymbolicLink );
		Assert.Contains( events, item => item.Entry?.Name == "inside.txt" );
	}


	/// <summary>
	/// Verifies that identity tracking is limited to active ancestry rather than global deduplication.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task IndependentlyReachedDirectoryIdentityRemainsObservable() {
		var paths = CreatePaths();
		var target = Path.Combine( paths.Base, "target" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddDirectory( target, "shared-target" )
			.AddFile( Path.Combine( target, "inside.txt" ) )
			.AddLink( Path.Combine( paths.Root, "first" ), target )
			.AddLink( Path.Combine( paths.Root, "second" ), target );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions {
				SymbolicLinkMode = SymbolicLinkTraversalMode.Always,
				ChildOrder = PathTraversalChildOrder.Ordinal
			}
		) );

		Assert.Equal( 2, events.Count( item => item.Entry?.Name == "inside.txt" ) );
		Assert.DoesNotContain( events, static item => item.Kind == PathTraversalEventKind.Cycle );
	}

	/// <summary>
	/// Verifies structured continuation after an entry disappears between enumeration and observation.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsDisappearingEntryAndContinuesWithSibling() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddPhantomChild( paths.Root, "gone.txt" )
			.AddFile( Path.Combine( paths.Root, "present.txt" ) );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions { ChildOrder = PathTraversalChildOrder.Ordinal }
		) );

		var error = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.Error );
		Assert.Equal( PathTraversalErrorCode.ObservationFailed, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Entry, error.Error.Scope );
		Assert.Contains( events, item => item.Entry?.Name == "present.txt" );
	}

	/// <summary>
	/// Verifies bounded one-directory retention during traversal.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsConfiguredDirectoryEntryLimit() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddFile( Path.Combine( paths.Root, "one.txt" ) )
			.AddFile( Path.Combine( paths.Root, "two.txt" ) );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions { MaximumEntriesPerDirectory = 1 }
		) );

		var error = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.Error );
		Assert.Equal( PathTraversalErrorCode.DirectoryEntryLimitExceeded, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Subtree, error.Error.Scope );
		Assert.Contains( events, item => item.Kind == PathTraversalEventKind.LeaveDirectory && item.Entry!.IsRoot );
	}

	/// <summary>
	/// Verifies that an unavailable filesystem identity is reported when a boundary must be enforced.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsUnavailableRootFileSystemIdentity() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( paths.Root );
		provider.RemoveFileSystemIdentity( paths.Root );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions {
				FileSystemBoundaryMode = FileSystemBoundaryMode.StayOnRootFileSystem
			}
		) );

		var error = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.Error );
		Assert.Equal( PathTraversalErrorCode.IdentityUnavailable, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Root, error.Error.Scope );
	}


	/// <summary>
	/// Verifies that recursive descent requires a stable identity for every active directory.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsUnavailableDirectoryIdentityBeforeRecursiveDescent() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( paths.Root );
		provider.RemoveEntryIdentity( paths.Root );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions()
		) );

		var error = Assert.Single( events, static item => item.Kind == PathTraversalEventKind.Error );
		Assert.Equal( PathTraversalErrorCode.IdentityUnavailable, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Root, error.Error.Scope );
	}

	/// <summary>
	/// Verifies that asynchronous roots are consumed incrementally and in order.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TraversesAsynchronousRootStreamInOrder() {
		var paths = CreatePaths();
		var second = Path.Combine( paths.Base, "second" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( paths.Root )
			.AddDirectory( second );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync( CreateRootsAsync(
			CreateRoot( paths.Root, "first" ),
			CreateRoot( second, "second" )
		) ) );

		Assert.Equal(
			new[] { "first", "second" },
			events.Where( static item => item.Kind == PathTraversalEventKind.Root )
				.Select( static item => item.Root.OriginalOperand )
		);
	}


	/// <summary>
	/// Verifies that fail-fast mode emits no directory-exit event after its first error.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FailFastStopsBeforeDirectoryExitAfterEnumerationError() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( paths.Root );
		provider.SetEnumerationException( paths.Root, new IOException( "synthetic" ) );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( paths.Root ) },
			new PathTraversalOptions { ErrorMode = PathTraversalErrorMode.Stop }
		) );

		Assert.Equal(
			new[] {
				PathTraversalEventKind.Root,
				PathTraversalEventKind.EnterDirectory,
				PathTraversalEventKind.Error
			},
			events.Select( static item => item.Kind )
		);
	}

	/// <summary>
	/// Verifies that fail-fast mode also stops an asynchronous root stream after the first error.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FailFastStopsAsynchronousRootStream() {
		var paths = CreatePaths();
		var second = Path.Combine( paths.Base, "second" );
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( second );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		var events = await CollectAsync( engine.TraverseAsync(
			CreateRootsAsync(
				CreateRoot( paths.Root, "missing" ),
				CreateRoot( second, "second" )
			),
			new PathTraversalOptions { ErrorMode = PathTraversalErrorMode.Stop }
		) );

		Assert.Equal(
			new[] { PathTraversalEventKind.Root, PathTraversalEventKind.Error },
			events.Select( static item => item.Kind )
		);
		Assert.DoesNotContain( events, item => item.Root.OriginalOperand == "second" );
	}


	/// <summary>
	/// Verifies deterministic validation before traversal begins.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsInvalidTraversalOptions() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( paths.Root );
		var engine = new ReadOnlyPathTraversalEngine( provider );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async () => {
			_ = await CollectAsync( engine.TraverseAsync(
				new[] { CreateRoot( paths.Root ) },
				new PathTraversalOptions { MaximumDepth = -1 }
			) );
		} );
	}

	/// <summary>
	/// Verifies cancellation before a traversal begins.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ObservesCancellation() {
		var paths = CreatePaths();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( paths.Root );
		var engine = new ReadOnlyPathTraversalEngine( provider );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			await foreach ( var unused in engine.TraverseAsync(
				new[] { CreateRoot( paths.Root ) },
				cancellationToken: cancellation.Token
			) ) {
				_ = unused;
			}
		} );
	}


	private static async IAsyncEnumerable<PathTraversalRoot> CreateRootsAsync(
		params PathTraversalRoot[] roots
	) {
		foreach ( var root in roots ) {
			await Task.Yield();
			yield return root;
		}
	}

	private static (string Base, string Root) CreatePaths() {
		var basePath = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "icod-e1-traversal-", Guid.NewGuid().ToString( "N" ) )
		);
		return (basePath, Path.Combine( basePath, "root" ));
	}

	private static PathTraversalRoot CreateRoot(
		string path,
		string originalOperand = "root"
	) => new(
		originalOperand,
		0,
		0,
		path,
		originalOperand,
		PathTraversalRootKind.Literal
	);

	private static async Task<IReadOnlyList<PathTraversalEvent>> CollectAsync(
		IAsyncEnumerable<PathTraversalEvent> source
	) {
		var results = new List<PathTraversalEvent>();
		await foreach ( var item in source ) {
			results.Add( item );
		}
		return results;
	}
}
