using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.RecursiveMutation;

/// <summary>Tests preserve-root and lexical containment preflight.</summary>
public sealed class RecursivePathSafetyTests {
	/// <summary>Verifies that the host filesystem root is recognized after normalization.</summary>
	[Fact]
	public void RecognizesFileSystemRoot() {
		var root = System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( "." ) )!;
		Assert.True( new RecursivePathSafety().IsFileSystemRoot( root ) );
	}

	/// <summary>Verifies that a destination below the source is rejected by classification.</summary>
	[Fact]
	public void ClassifiesDestinationInsideSource() {
		var source = System.IO.Path.Combine( System.IO.Path.GetTempPath(), "e5-source" );
		var destination = System.IO.Path.Combine( source, "copy" );
		Assert.Equal(
			RecursivePathRelationship.DestinationInsideSource,
			new RecursivePathSafety().Classify( source, destination )
		);
	}

	/// <summary>Verifies that a textual prefix without a separator is not treated as containment.</summary>
	[Fact]
	public void DoesNotConfuseSiblingPrefixWithDescendant() {
		var parent = System.IO.Path.GetTempPath();
		var source = System.IO.Path.Combine( parent, "tree" );
		var destination = System.IO.Path.Combine( parent, "tree-copy" );
		Assert.Equal(
			RecursivePathRelationship.Disjoint,
			new RecursivePathSafety().Classify( source, destination )
		);
	}

	/// <summary>Verifies E2 physical resolution for an existing source and missing destination suffix.</summary>
	[Fact]
	public async Task EvaluatesPhysicalDestinationInsideSource() {
		var directory = Directory.CreateTempSubdirectory( "e5-path-safety-" );
		try {
			var result = await new RecursivePathSafety().EvaluateAsync(
				directory.FullName,
				System.IO.Path.Combine( directory.FullName, "missing", "destination" )
			);
			Assert.True( result.Succeeded );
			Assert.Equal( RecursivePathRelationship.DestinationInsideSource, result.Relationship );
		} finally {
			directory.Delete( true );
		}
	}
}
