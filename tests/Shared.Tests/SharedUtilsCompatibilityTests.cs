namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared;
using Xunit;

public sealed class SharedUtilsCompatibilityTests {

	[Fact]
	public void BasenamePreservesLegacyBehavior() {
		Assert.Equal( ".", SharedUtils.Basename( string.Empty ) );
		Assert.Equal(
			System.IO.Path.DirectorySeparatorChar.ToString(),
			SharedUtils.Basename(
				System.IO.Path.DirectorySeparatorChar.ToString()
			)
		);
		Assert.Equal(
			"file.txt",
			SharedUtils.Basename(
				System.IO.Path.Combine( "directory", "file.txt" )
			)
		);
	}

	[Fact]
	public void LegacyParseOptionsRetainsExistingContract() {
		var result = SharedUtils.ParseOptions(
			new string[ 4 ] { "-ab", "-n10", "file", "other" },
			"abn:"
		);

		Assert.Contains( 'a', result.flags );
		Assert.Contains( 'b', result.flags );
		Assert.Equal( "10", result.optionValues[ 'n' ] );
		Assert.Equal( new string[ 2 ] { "file", "other" }, result.rest );
	}

	[Fact]
	public void ParseAssignmentsKeepsLastValueIgnoringCase() {
		var result = SharedUtils.ParseAssignments(
			new string[ 3 ] { "Name=one", "NAME=two", "invalid" }
		);

		Assert.Single( result );
		Assert.Equal( "two", result[ "name" ] );
	}

	[Fact]
	public void SplitByNumericLineRetainsLegacySegments() {
		var result = SharedUtils.SplitByPatternOrLines(
			new string[ 4 ] { "a", "b", "c", "d" },
			"3"
		).ToArray();

		Assert.Equal( new string[ 2 ] { "a", "b" }, result[ 0 ] );
		Assert.Equal( new string[ 2 ] { "c", "d" }, result[ 1 ] );
	}

}
