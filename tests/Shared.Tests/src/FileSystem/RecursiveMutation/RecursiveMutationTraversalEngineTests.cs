using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.RecursiveMutation;

/// <summary>Tests the E5 extension of the E1 event stream.</summary>
public sealed class RecursiveMutationTraversalEngineTests {
	/// <summary>Verifies root-relative destination mapping, no-follow preconditions, and hard-link tracking.</summary>
	[Fact]
	public async Task MapsEntriesAndTracksRepeatedIdentity() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var destination = Path.Combine( Path.GetTempPath(), "e5-destination" );
		var sharedIdentity = new FileSystemEntryIdentity( "test", "file-1" );
		var provider = new SyntheticProvider()
			.AddDirectory( source, "directory-1", "filesystem-1" )
			.AddFile( Path.Combine( source, "a" ), sharedIdentity, "filesystem-1" )
			.AddFile( Path.Combine( source, "b" ), sharedIdentity, "filesystem-1" );
		var engine = new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		);
		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( source ) },
			new RecursiveMutationOptions {
				DestinationPath = destination,
				ChildOrder = PathTraversalChildOrder.Ordinal
			}
		) );
		var entries = events.Where( item => item.Kind == RecursiveMutationEventKind.Entry ).ToArray();
		Assert.Equal( 2, entries.Length );
		Assert.Equal( Path.Combine( destination, "a" ), entries[0].Entry!.DestinationPath );
		Assert.Equal( PathDereferenceMode.NoFollow, entries[0].Entry!.Precondition.DereferenceMode );
		Assert.False( entries[0].Entry!.IsRepeatedHardLink );
		Assert.True( entries[1].Entry!.IsRepeatedHardLink );
		Assert.Equal( Path.Combine( source, "a" ), entries[1].Entry!.FirstHardLinkSourcePath );
		Assert.Equal( Path.Combine( destination, "a" ), entries[1].Entry!.FirstHardLinkDestinationPath );
	}

	/// <summary>Verifies that E1 filesystem identities enforce one-filesystem descent for E5.</summary>
	[Fact]
	public async Task PreservesFileSystemBoundaryEvents() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var provider = new SyntheticProvider()
			.AddDirectory( source, "directory-1", "filesystem-1" )
			.AddDirectory( Path.Combine( source, "mounted" ), "directory-2", "filesystem-2" );
		var engine = new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		);
		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( source ) },
			new RecursiveMutationOptions {
				FileSystemBoundaryMode = FileSystemBoundaryMode.StayOnRootFileSystem
			}
		) );
		var boundary = Assert.Single( events, item => item.Kind == RecursiveMutationEventKind.FileSystemBoundary );
		Assert.Equal( Path.Combine( source, "mounted" ), boundary.Entry!.TraversalEntry.AccessPath );
	}

	/// <summary>Verifies that repeated identities remain visible across separate source roots.</summary>
	[Fact]
	public async Task TracksHardLinksAcrossRoots() {
		var firstPath = Path.Combine( Path.GetTempPath(), "e5-first" );
		var secondPath = Path.Combine( Path.GetTempPath(), "e5-second" );
		var identity = new FileSystemEntryIdentity( "test", "shared-file" );
		var provider = new SyntheticProvider()
			.AddFile( firstPath, identity, "filesystem-1" )
			.AddFile( secondPath, identity, "filesystem-1" );
		var engine = new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		);
		var events = await CollectAsync( engine.TraverseAsync( new[] {
			CreateRoot( firstPath ),
			CreateRoot( secondPath )
		} ) );
		var entries = events.Where( item => item.Kind == RecursiveMutationEventKind.Entry ).ToArray();
		Assert.Equal( 2, entries.Length );
		Assert.False( entries[0].Entry!.IsRepeatedHardLink );
		Assert.True( entries[1].Entry!.IsRepeatedHardLink );
		Assert.Equal( firstPath, entries[1].Entry!.FirstHardLinkSourcePath );
	}

	/// <summary>Verifies that E1 selector pruning is preserved by the mutation-aware layer.</summary>
	[Fact]
	public async Task PreservesSelectorPruning() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var skipped = Path.Combine( source, "skip" );
		var nested = Path.Combine( skipped, "nested" );
		var provider = new SyntheticProvider()
			.AddDirectory( source, "directory-1", "filesystem-1" )
			.AddDirectory( skipped, "directory-2", "filesystem-1" )
			.AddFile( nested, new FileSystemEntryIdentity( "test", "file-1" ), "filesystem-1" );
		var engine = new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		);
		var events = await CollectAsync( engine.TraverseAsync(
			new[] { CreateRoot( source ) },
			new RecursiveMutationOptions { Selector = new PruneNamedDirectorySelector( "skip" ) }
		) );
		Assert.Contains( events, item =>
			item.Kind == RecursiveMutationEventKind.EnterDirectory
			&& item.Entry?.TraversalEntry.AccessPath == skipped
		);
		Assert.DoesNotContain( events, item => item.Entry?.TraversalEntry.AccessPath == nested );
		Assert.Equal( 1, provider.EnumerationCount );
	}

	/// <summary>Verifies that E1 cycle events retain their related ancestor path.</summary>
	[Fact]
	public async Task PreservesCycleEvents() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var child = Path.Combine( source, "cycle" );
		var provider = new SyntheticProvider()
			.AddDirectory( source, "directory-1", "filesystem-1" )
			.AddDirectory( child, "directory-1", "filesystem-1" );
		var engine = new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		);
		var events = await CollectAsync( engine.TraverseAsync( new[] { CreateRoot( source ) } ) );
		var cycle = Assert.Single( events, item => item.Kind == RecursiveMutationEventKind.Cycle );
		Assert.Equal( source, cycle.RelatedPath );
		Assert.Equal( child, cycle.Entry!.TraversalEntry.AccessPath );
	}

	/// <summary>Verifies that a filesystem root is refused before provider observation.</summary>
	[Fact]
	public async Task RejectsFileSystemRootBeforeTraversal() {
		var rootPath = Path.GetPathRoot( Path.GetFullPath( "." ) )!;
		var provider = new SyntheticProvider();
		var events = await CollectAsync( new RecursiveMutationTraversalEngine( provider ).TraverseAsync(
			new[] { CreateRoot( rootPath ) }
		) );
		Assert.Equal( RecursiveMutationEventKind.Root, events[0].Kind );
		Assert.Equal( RecursiveMutationErrorCode.PreservedRoot, events[1].Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Root, events[1].Error!.Scope );
		Assert.Equal( 0, provider.ObservationCount );
	}

	/// <summary>Verifies that destination containment fails before the provider is observed.</summary>
	[Fact]
	public async Task RejectsDestinationInsideSourceBeforeTraversal() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var provider = new SyntheticProvider();
		var events = await CollectAsync( new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		).TraverseAsync(
			new[] { CreateRoot( source ) },
			new RecursiveMutationOptions { DestinationPath = Path.Combine( source, "copy" ) }
		) );
		Assert.Equal( RecursiveMutationEventKind.Root, events[0].Kind );
		Assert.Equal( RecursiveMutationEventKind.Error, events[1].Kind );
		Assert.Equal( RecursiveMutationErrorCode.DestinationInsideSource, events[1].Error!.Code );
		Assert.Equal( 0, provider.ObservationCount );
	}

	/// <summary>Verifies that E1 continuation scope and the underlying structured error are retained.</summary>
	[Fact]
	public async Task PreservesStructuredTraversalErrorScope() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var provider = new SyntheticProvider()
			.AddDirectory( source, "directory-1", "filesystem-1" )
			.AddMissingChild( source, "missing" );
		var events = await CollectAsync( new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		).TraverseAsync( new[] { CreateRoot( source ) } ) );
		var error = Assert.Single( events, item => item.Kind == RecursiveMutationEventKind.Error );
		Assert.Equal( RecursiveMutationErrorCode.TraversalFailed, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Entry, error.Error.Scope );
		Assert.NotNull( error.Error.TraversalError );
	}

	/// <summary>Verifies that a mutable entry without a stable identity is rejected.</summary>
	[Fact]
	public async Task RejectsEntryWithoutStableIdentity() {
		var source = Path.Combine( Path.GetTempPath(), "e5-source" );
		var provider = new SyntheticProvider()
			.AddDirectory( source, "directory-1", "filesystem-1" )
			.AddFile( Path.Combine( source, "entry" ), FileSystemEntryIdentity.Unavailable, "filesystem-1" );
		var events = await CollectAsync( new RecursiveMutationTraversalEngine(
			provider,
			new RecursivePathSafety( StringComparison.Ordinal )
		).TraverseAsync( new[] { CreateRoot( source ) } ) );
		var error = Assert.Single( events, item => item.Error?.Code == RecursiveMutationErrorCode.IdentityUnavailable );
		Assert.Equal( RecursiveMutationStage.Traversal, error.Error!.Stage );
	}

	private static PathTraversalRoot CreateRoot( string path ) => new(
		path,
		0,
		0,
		path,
		path,
		PathTraversalRootKind.Literal
	);

	private static async Task<List<RecursiveMutationEvent>> CollectAsync(
		IAsyncEnumerable<RecursiveMutationEvent> source
	) {
		var items = new List<RecursiveMutationEvent>();
		await foreach ( var item in source ) {
			items.Add( item );
		}
		return items;
	}

	private sealed class PruneNamedDirectorySelector : IPathTraversalSelector {
		private readonly string _name;

		/// <summary>Initializes a selector that yields but prunes one directory basename.</summary>
		public PruneNamedDirectorySelector( string name ) {
			_name = name;
		}

		/// <summary>Selects every entry while pruning the configured directory.</summary>
		public PathTraversalSelection Select( PathTraversalEntry entry ) =>
			entry.Kind == FileSystemEntryKind.Directory && entry.Name == _name
				? new PathTraversalSelection( true, false )
				: PathTraversalSelection.IncludeAll;
	}

	private sealed class SyntheticProvider : IReadOnlyFileSystemProvider {
		private readonly Dictionary<string, ReadOnlyFileSystemEntry> _entries = new( StringComparer.Ordinal );
		private readonly Dictionary<string, List<ReadOnlyDirectoryEntry>> _children = new( StringComparer.Ordinal );

		/// <summary>Initializes an empty deterministic provider.</summary>
		public SyntheticProvider() {
		}

		/// <summary>Gets the number of pathname observations.</summary>
		public int ObservationCount { get; private set; }

		/// <summary>Gets the number of one-directory enumerations.</summary>
		public int EnumerationCount { get; private set; }

		/// <summary>Adds one synthetic directory and returns this provider.</summary>
		public SyntheticProvider AddDirectory( string path, string identity, string fileSystem ) {
			AddEntry( path, FileSystemEntryKind.Directory, new FileSystemEntryIdentity( "test", identity ), fileSystem );
			_children[path] = new List<ReadOnlyDirectoryEntry>();
			AddToParent( path );
			return this;
		}

		/// <summary>Adds one synthetic ordinary file and returns this provider.</summary>
		public SyntheticProvider AddFile(
			string path,
			FileSystemEntryIdentity identity,
			string fileSystem
		) {
			AddEntry( path, FileSystemEntryKind.File, identity, fileSystem );
			AddToParent( path );
			return this;
		}

		/// <summary>Adds an enumerated child whose later observation fails.</summary>
		public SyntheticProvider AddMissingChild( string parentPath, string name ) {
			_children[parentPath].Add( new ReadOnlyDirectoryEntry( name, Path.Combine( parentPath, name ) ) );
			return this;
		}

		/// <summary>Observes one synthetic pathname.</summary>
		public ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
			string path,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			ObservationCount++;
			return ValueTask.FromResult( _entries[path] );
		}

		/// <summary>Enumerates one synthetic directory level.</summary>
		public async IAsyncEnumerable<ReadOnlyDirectoryEntry> EnumerateDirectoryAsync(
			string directoryPath,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
		) {
			EnumerationCount++;
			foreach ( var child in _children[directoryPath] ) {
				cancellationToken.ThrowIfCancellationRequested();
				yield return child;
				await Task.Yield();
			}
		}

		private void AddEntry(
			string path,
			FileSystemEntryKind kind,
			FileSystemEntryIdentity identity,
			string fileSystem
		) {
			_entries[path] = new ReadOnlyFileSystemEntry(
				path,
				Path.GetFileName( path ),
				kind,
				false,
				false,
				null,
				identity,
				new FileSystemIdentity( "test", fileSystem )
			);
		}

		private void AddToParent( string path ) {
			var parent = Path.GetDirectoryName( path );
			if ( parent is not null && _children.TryGetValue( parent, out var children ) ) {
				children.Add( new ReadOnlyDirectoryEntry( Path.GetFileName( path ), path ) );
			}
		}
	}
}
