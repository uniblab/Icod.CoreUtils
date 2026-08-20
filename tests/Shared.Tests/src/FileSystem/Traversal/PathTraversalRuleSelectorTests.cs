using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>
/// Tests ordered traversal filtering and matching scopes.
/// </summary>
public sealed class PathTraversalRuleSelectorTests {
	/// <summary>
	/// Verifies that the last matching rule controls each targeted decision independently.
	/// </summary>
	[Fact]
	public void AppliesLastMatchingRuleIndependentlyToYieldAndDescend() {
		var selector = new PathTraversalRuleSelector(
			new[] {
				new PathTraversalFilterRule(
					PathnamePattern.Parse( "build" ),
					PathMatchScope.BaseName,
					PathTraversalRuleAction.Exclude,
					PathTraversalRuleTarget.YieldAndDescend,
					PathTraversalRuleEntryKind.Directories
				),
				new PathTraversalFilterRule(
					PathnamePattern.Parse( "build" ),
					PathMatchScope.BaseName,
					PathTraversalRuleAction.Include,
					PathTraversalRuleTarget.Yield,
					PathTraversalRuleEntryKind.Directories
				)
			},
			PathTraversalSelection.IncludeAll
		);

		var selection = selector.Select( CreateEntry(
			"build",
			"build",
			FileSystemEntryKind.Directory
		) );

		Assert.True( selection.Yield );
		Assert.False( selection.Descend );
	}

	/// <summary>
	/// Verifies basename, root-relative, whole-path, and matching-name-suffix scopes.
	/// </summary>
	[Fact]
	public void SupportsAllDocumentedMatchingScopes() {
		var entry = CreateEntry(
			System.IO.Path.Combine( "src", "generated", "file.g.cs" ),
			System.IO.Path.Combine( "operand", "src", "generated", "file.g.cs" ),
			FileSystemEntryKind.File
		);
		var accessPath = entry.AccessPath;

		Assert.False( SelectsYield( entry, "*.g.cs", PathMatchScope.BaseName, PathTraversalRuleAction.Exclude ) );
		Assert.False( SelectsYield(
			entry,
			System.IO.Path.Combine( "src", "**", "*.g.cs" ),
			PathMatchScope.RootRelativePath,
			PathTraversalRuleAction.Exclude
		) );
		Assert.False( SelectsYield(
			entry,
			accessPath,
			PathMatchScope.WholePath,
			PathTraversalRuleAction.Exclude
		) );
		Assert.False( SelectsYield(
			entry,
			System.IO.Path.Combine( "generated", "*.g.cs" ),
			PathMatchScope.MatchingNameSuffix,
			PathTraversalRuleAction.Exclude
		) );
	}

	/// <summary>
	/// Verifies that a trailing separator restricts a rule to directory entries.
	/// </summary>
	[Fact]
	public void TrailingSeparatorRuleMatchesDirectoriesOnly() {
		var pattern = string.Concat( "build", System.IO.Path.DirectorySeparatorChar );
		var selector = new PathTraversalRuleSelector(
			new[] {
				new PathTraversalFilterRule(
					PathnamePattern.Parse( pattern ),
					PathMatchScope.BaseName,
					PathTraversalRuleAction.Exclude,
					PathTraversalRuleTarget.Yield
				)
			},
			PathTraversalSelection.IncludeAll
		);

		Assert.False( selector.Select( CreateEntry( "build", "build", FileSystemEntryKind.Directory ) ).Yield );
		Assert.True( selector.Select( CreateEntry( "build", "build", FileSystemEntryKind.File ) ).Yield );
	}


	/// <summary>
	/// Verifies that a rule collection cannot contain a null entry.
	/// </summary>
	[Fact]
	public void RejectsNullRuleEntry() {
		Assert.Throws<ArgumentException>( () => new PathTraversalRuleSelector(
			new PathTraversalFilterRule[] { null! },
			PathTraversalSelection.IncludeAll
		) );
	}

	private static bool SelectsYield(
		PathTraversalEntry entry,
		string pattern,
		PathMatchScope scope,
		PathTraversalRuleAction action
	) {
		var selector = new PathTraversalRuleSelector(
			new[] {
				new PathTraversalFilterRule(
					PathnamePattern.Parse( pattern ),
					scope,
					action,
					PathTraversalRuleTarget.Yield
				)
			},
			PathTraversalSelection.IncludeAll
		);
		return selector.Select( entry ).Yield;
	}

	private static PathTraversalEntry CreateEntry(
		string relativePath,
		string displayPath,
		FileSystemEntryKind kind
	) {
		var rootPath = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "icod-e1-selector-", Guid.NewGuid().ToString( "N" ) )
		);
		var root = new PathTraversalRoot(
			"operand",
			0,
			0,
			rootPath,
			"operand",
			PathTraversalRootKind.Literal
		);
		var name = System.IO.Path.GetFileName( relativePath );
		return new PathTraversalEntry(
			root,
			System.IO.Path.Combine( rootPath, relativePath ),
			displayPath,
			relativePath,
			name,
			1,
			kind,
			false,
			false,
			null,
			new FileSystemEntryIdentity( "synthetic", relativePath ),
			new FileSystemIdentity( "synthetic", "fs" )
		);
	}
}
