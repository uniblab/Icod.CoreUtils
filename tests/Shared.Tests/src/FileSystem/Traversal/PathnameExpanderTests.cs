using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>
/// Tests provenance-preserving pathname operand expansion.
/// </summary>
public sealed class PathnameExpanderTests {
	/// <summary>
	/// Verifies ordered expansion and preservation of repeated operands.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExpandsInOperandAndOrdinalNameOrder() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddFile( Path.Combine( rootPath, "z.txt" ) )
			.AddFile( Path.Combine( rootPath, "a.txt" ) )
			.AddFile( Path.Combine( rootPath, "skip.bin" ) );
		var expander = new PathnameExpander( provider );
		var operand = Path.Combine( "root", "*.txt" );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { operand, operand },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				MatchOrder = PathnameExpansionMatchOrder.Ordinal,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var roots = events.Where( static item => item.Root is not null ).Select( static item => item.Root! ).ToArray();
		Assert.Equal( 4, roots.Length );
		Assert.Equal( new[] { 0, 0, 1, 1 }, roots.Select( static root => root.OperandIndex ) );
		Assert.Equal( new long[] { 0, 1, 2, 3 }, roots.Select( static root => root.RootOrdinal ) );
		Assert.Equal(
			new[] { "a.txt", "z.txt", "a.txt", "z.txt" },
			roots.Select( static root => Path.GetFileName( root.AccessPath ) )
		);
	}

	/// <summary>
	/// Verifies recursive <c>**</c> expansion, including its zero-segment case.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExpandsDoubleStarAcrossZeroOrMoreSegments() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var nestedPath = Path.Combine( rootPath, "nested" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddFile( Path.Combine( rootPath, "target.txt" ) )
			.AddDirectory( nestedPath )
			.AddFile( Path.Combine( nestedPath, "target.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**", "target.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				MatchOrder = PathnameExpansionMatchOrder.Ordinal,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var relative = events
			.Where( static item => item.Root is not null )
			.Select( item => Path.GetRelativePath( basePath, item.Root!.AccessPath ) )
			.ToArray();
		Assert.Equal(
			new[] {
				Path.Combine( "root", "target.txt" ),
				Path.Combine( "root", "nested", "target.txt" )
			},
			relative
		);
	}


	/// <summary>
	/// Verifies that adjacent recursive segments do not duplicate one logical pathname match.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task AdjacentDoubleStarsDoNotDuplicateSameExpansionState() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var nestedPath = Path.Combine( rootPath, "nested" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddDirectory( nestedPath )
			.AddFile( Path.Combine( nestedPath, "target.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**", "**", "target.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		Assert.Equal( Path.Combine( nestedPath, "target.txt" ), Assert.Single( events ).Root!.AccessPath );
	}


	/// <summary>
	/// Verifies lexical current-directory segments in an expandable operand.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ExpandsPatternWithCurrentDirectorySegment() {
		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddFile( Path.Combine( basePath, "found.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( ".", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var root = Assert.Single( events ).Root!;
		Assert.Equal( Path.Combine( basePath, "found.txt" ), root.AccessPath );
		Assert.Equal( Path.Combine( ".", "found.txt" ), root.DisplayPath );
	}


	/// <summary>
	/// Verifies lexical parent-directory navigation without reporting an active-ancestry cycle.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ParentDirectorySegmentRewindsExpansionAncestry() {
		var basePath = CreateBasePath();
		var childPath = Path.Combine( basePath, "child" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( childPath )
			.AddFile( Path.Combine( basePath, "found.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "child", "..", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		Assert.Equal( Path.Combine( basePath, "found.txt" ), Assert.Single( events ).Root!.AccessPath );
	}

	/// <summary>
	/// Verifies the three unmatched-pattern policies.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task AppliesConfiguredUnmatchedPatternPolicy() {
		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( basePath );
		var expander = new PathnameExpander( provider );
		var operand = "*.missing";

		var preserved = await CollectAsync( expander.ExpandAsync(
			new[] { operand },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.PreserveAsLiteral
			}
		) );
		Assert.Equal( PathTraversalRootKind.Literal, Assert.Single( preserved ).Root!.Kind );

		var noMatches = await CollectAsync( expander.ExpandAsync(
			new[] { operand },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );
		Assert.Equal( PathnameExpansionEventKind.NoMatch, Assert.Single( noMatches ).Kind );

		var errors = await CollectAsync( expander.ExpandAsync(
			new[] { operand },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReportError
			}
		) );
		Assert.Equal( PathTraversalErrorCode.NoPatternMatch, Assert.Single( errors ).Error!.Code );
	}

	/// <summary>
	/// Verifies that root-only link following applies to explicitly named intermediate segments, not wildcard discoveries.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RootsOnlyFollowsExplicitButNotWildcardDiscoveredLink() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var targetPath = Path.Combine( basePath, "target" );
		var linkPath = Path.Combine( rootPath, "link" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddDirectory( targetPath )
			.AddFile( Path.Combine( targetPath, "found.txt" ) )
			.AddLink( linkPath, targetPath );
		var expander = new PathnameExpander( provider );
		var options = new PathnameExpansionOptions {
			BaseDirectory = basePath,
			SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly,
			UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
		};

		var explicitEvents = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "link", "*.txt" ) },
			options
		) );
		Assert.Single( explicitEvents.Where( static item => item.Root is not null ) );

		var wildcardEvents = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "*", "*.txt" ) },
			options
		) );
		Assert.Equal( PathnameExpansionEventKind.NoMatch, Assert.Single( wildcardEvents ).Kind );
	}


	/// <summary>
	/// Verifies that a final recursive wildcard returns the starting directory and every visible descendant.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FinalDoubleStarIncludesZeroSegmentAndDescendantMatches() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var nestedPath = Path.Combine( rootPath, "nested" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddFile( Path.Combine( rootPath, "file.txt" ) )
			.AddDirectory( nestedPath )
			.AddFile( Path.Combine( nestedPath, "inside.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				MatchOrder = PathnameExpansionMatchOrder.Ordinal,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		Assert.Equal(
			new[] {
				"root",
				Path.Combine( "root", "file.txt" ),
				Path.Combine( "root", "nested" ),
				Path.Combine( "root", "nested", "inside.txt" )
			},
			events.Where( static item => item.Root is not null )
				.Select( item => Path.GetRelativePath( basePath, item.Root!.AccessPath ) )
		);
	}

	/// <summary>
	/// Verifies that recursive wildcards honor the configured leading-period policy.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DoubleStarDoesNotConsumeHiddenSegmentByDefault() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var hiddenPath = Path.Combine( rootPath, ".hidden" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddDirectory( hiddenPath )
			.AddFile( Path.Combine( hiddenPath, "inside.txt" ) )
			.AddFile( Path.Combine( rootPath, "visible.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				MatchOrder = PathnameExpansionMatchOrder.Ordinal,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var root = Assert.Single( events.Where( static item => item.Root is not null ) ).Root!;
		Assert.Equal( "visible.txt", Path.GetFileName( root.AccessPath ) );
	}

	/// <summary>
	/// Verifies that quoting a metacharacter creates a literal operand rather than a wildcard expansion.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task QuotedMetacharacterProducesUnquotedLiteralRootOnUnixLikeHosts() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( basePath );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { @"a\*b.txt" },
			new PathnameExpansionOptions { BaseDirectory = basePath }
		) );

		var root = Assert.Single( events ).Root!;
		Assert.Equal( Path.Combine( basePath, "a*b.txt" ), root.AccessPath );
		Assert.Equal( @"a\*b.txt", root.OriginalOperand );
		Assert.Equal( "a*b.txt", root.DisplayPath );
	}

	/// <summary>
	/// Verifies deterministic structured errors for disappearing intermediate entries.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsDisappearingIntermediateEntryWithoutConvertingItToNoMatch() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddPhantomChild( rootPath, "gone" );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "*", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var error = Assert.Single( events );
		Assert.Equal( PathnameExpansionEventKind.Error, error.Kind );
		Assert.Equal( PathTraversalErrorCode.ObservationFailed, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Entry, error.Error.Scope );
	}

	/// <summary>
	/// Verifies bounded per-directory retention during expansion.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsConfiguredDirectoryEntryLimit() {
		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddFile( Path.Combine( basePath, "one.txt" ) )
			.AddFile( Path.Combine( basePath, "two.txt" ) );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { "*.txt" },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				MaximumEntriesPerDirectory = 1,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var error = Assert.Single( events );
		Assert.Equal( PathTraversalErrorCode.DirectoryEntryLimitExceeded, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Subtree, error.Error.Scope );
	}


	/// <summary>
	/// Verifies that recursive expansion requires stable identities even when links are not followed.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsUnavailableIdentityBeforeRecursiveExpansion() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath );
		provider.RemoveEntryIdentity( basePath );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var error = Assert.Single( events );
		Assert.Equal( PathTraversalErrorCode.IdentityUnavailable, error.Error!.Code );
		Assert.Equal( PathTraversalErrorScope.Root, error.Error.Scope );
	}


	/// <summary>
	/// Verifies that finite link-following expansion remains usable without stable entry identities.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FiniteLinkExpansionDoesNotRequireEntryIdentity() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var targetPath = Path.Combine( basePath, "target" );
		var linkPath = Path.Combine( rootPath, "link" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddDirectory( targetPath )
			.AddFile( Path.Combine( targetPath, "found.txt" ) )
			.AddLink( linkPath, targetPath );
		provider.RemoveEntryIdentity( basePath );
		provider.RemoveEntryIdentity( rootPath );
		provider.RemoveEntryIdentity( targetPath );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "link", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		Assert.Equal( Path.Combine( linkPath, "found.txt" ), Assert.Single( events ).Root!.AccessPath );
	}

	/// <summary>
	/// Verifies that a finite explicitly named link path may revisit an ancestor safely.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FiniteExpansionMayRevisitAncestorThroughExplicitLink() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var childPath = Path.Combine( rootPath, "child" );
		var upPath = Path.Combine( childPath, "up" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath, "root-id" )
			.AddDirectory( childPath, "child-id" )
			.AddFile( Path.Combine( rootPath, "found.txt" ) )
			.AddLink( upPath, rootPath );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "child", "up", "found.*" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		Assert.Equal(
			Path.Combine( upPath, "found.txt" ),
			Assert.Single( events ).Root!.AccessPath
		);
	}


	/// <summary>
	/// Verifies active-ancestry cycle events while recursive expansion continues safely.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsCycleDuringRecursiveExpansion() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var childPath = Path.Combine( rootPath, "child" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath, "root-id" )
			.AddDirectory( childPath, "child-id" )
			.AddLink( Path.Combine( childPath, "up" ), rootPath );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				SymbolicLinkMode = SymbolicLinkTraversalMode.Always,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var cycle = Assert.Single( events );
		Assert.Equal( PathnameExpansionEventKind.Cycle, cycle.Kind );
		Assert.Equal( rootPath, cycle.RelatedPath );
	}

	/// <summary>
	/// Verifies filesystem-boundary events during recursive expansion.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsFileSystemBoundaryDuringRecursiveExpansion() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var mountedPath = Path.Combine( rootPath, "mounted" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath, fileSystemIdentity: "fs-root" )
			.AddDirectory( rootPath, fileSystemIdentity: "fs-root" )
			.AddDirectory( mountedPath, fileSystemIdentity: "fs-other" )
			.AddFile( Path.Combine( mountedPath, "inside.txt" ), fileSystemIdentity: "fs-other" );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { Path.Combine( "root", "**", "*.txt" ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				FileSystemBoundaryMode = FileSystemBoundaryMode.StayOnRootFileSystem,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var boundary = Assert.Single( events );
		Assert.Equal( PathnameExpansionEventKind.FileSystemBoundary, boundary.Kind );
		Assert.Equal( mountedPath, boundary.Path );
	}

	/// <summary>
	/// Verifies that fail-fast mode stops after the first expansion error.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FailFastStopsAfterFirstNoMatchError() {
		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( basePath );
		var expander = new PathnameExpander( provider );

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { "*.missing", "*.also-missing" },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReportError,
				ErrorMode = PathTraversalErrorMode.Stop
			}
		) );

		var error = Assert.Single( events );
		Assert.Equal( 0, error.OperandIndex );
		Assert.Equal( PathTraversalErrorCode.NoPatternMatch, error.Error!.Code );
	}

	/// <summary>
	/// Verifies that a failed terminal directory observation remains a structured error.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsTerminalDirectoryObservationFailure() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var childPath = Path.Combine( rootPath, "child" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddDirectory( childPath );
		provider.SetObservationException( childPath, new IOException( "synthetic" ) );
		var expander = new PathnameExpander( provider );
		var separator = Path.DirectorySeparatorChar.ToString();

		var events = await CollectAsync( expander.ExpandAsync(
			new[] { string.Concat( "r*", separator, "*", separator ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );

		var error = Assert.Single( events );
		Assert.Equal( PathnameExpansionEventKind.Error, error.Kind );
		Assert.Equal( PathTraversalErrorCode.ObservationFailed, error.Error!.Code );
		Assert.Equal( childPath, error.Error.Path );
	}

	/// <summary>
	/// Verifies that a trailing separator treats a wildcard-produced terminal link as an expanded root.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TrailingSeparatorRespectsTerminalLinkPolicy() {
		var basePath = CreateBasePath();
		var rootPath = Path.Combine( basePath, "root" );
		var targetPath = Path.Combine( basePath, "target" );
		var linkPath = Path.Combine( rootPath, "link" );
		var provider = new SyntheticReadOnlyFileSystemProvider()
			.AddDirectory( basePath )
			.AddDirectory( rootPath )
			.AddDirectory( targetPath )
			.AddLink( linkPath, targetPath );
		var expander = new PathnameExpander( provider );
		var separator = Path.DirectorySeparatorChar.ToString();

		var explicitEvents = await CollectAsync( expander.ExpandAsync(
			new[] { string.Concat( "r*", separator, "link", separator ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );
		Assert.Equal( linkPath, Assert.Single( explicitEvents ).Root!.AccessPath );

		var wildcardEvents = await CollectAsync( expander.ExpandAsync(
			new[] { string.Concat( "r*", separator, "*", separator ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );
		Assert.Equal( linkPath, Assert.Single( wildcardEvents ).Root!.AccessPath );

		var followedEvents = await CollectAsync( expander.ExpandAsync(
			new[] { string.Concat( "r*", separator, "*", separator ) },
			new PathnameExpansionOptions {
				BaseDirectory = basePath,
				SymbolicLinkMode = SymbolicLinkTraversalMode.Always,
				UnmatchedPatternBehavior = UnmatchedPathnamePatternBehavior.ReturnNoMatches
			}
		) );
		Assert.Equal( linkPath, Assert.Single( followedEvents ).Root!.AccessPath );
	}


	/// <summary>
	/// Verifies deterministic validation before expansion begins.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsInvalidExpansionOptions() {
		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( basePath );
		var expander = new PathnameExpander( provider );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async () => {
			_ = await CollectAsync( expander.ExpandAsync(
				new[] { "*" },
				new PathnameExpansionOptions { MaximumEntriesPerDirectory = 0 }
			) );
		} );
	}

	/// <summary>
	/// Verifies that cancellation is observed before enumeration continues.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ObservesCancellation() {
		var basePath = CreateBasePath();
		var provider = new SyntheticReadOnlyFileSystemProvider().AddDirectory( basePath );
		var expander = new PathnameExpander( provider );
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			await foreach ( var unused in expander.ExpandAsync(
				new[] { "*" },
				new PathnameExpansionOptions { BaseDirectory = basePath },
				cancellation.Token
			) ) {
				_ = unused;
			}
		} );
	}

	private static string CreateBasePath() => Path.Combine(
		Path.GetTempPath(),
		string.Concat( "icod-e1-synthetic-", Guid.NewGuid().ToString( "N" ) )
	);

	private static async Task<IReadOnlyList<PathnameExpansionEvent>> CollectAsync(
		IAsyncEnumerable<PathnameExpansionEvent> source
	) {
		var results = new List<PathnameExpansionEvent>();
		await foreach ( var item in source ) {
			results.Add( item );
		}
		return results;
	}
}
