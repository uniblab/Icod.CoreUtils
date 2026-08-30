namespace Icod.CoreUtils.Rm.Tests;

using Icod.CommandFramework.Diagnostics;
using RmCommand = Icod.CoreUtils.Rm.Command;
using Xunit;

/// <summary>Exercises pathname expansion at the <c>rm</c> command boundary.</summary>
public sealed class GlobbingTests {
	/// <summary>Verifies a wildcard selects every matching removal target.</summary>
	[Fact]
	public async Task ExpandsPathnameOperandsBeforeRemoval() {
		var root = CreateTemporaryDirectory();
		var first = System.IO.Path.Combine( root, "first.txt" );
		var second = System.IO.Path.Combine( root, "second.txt" );
		var retained = System.IO.Path.Combine( root, "retained.bin" );
		await File.WriteAllTextAsync( first, "one" );
		await File.WriteAllTextAsync( second, "two" );
		await File.WriteAllTextAsync( retained, "keep" );
		try {
			var status = await RmCommand.RunAsync(
				new[] { System.IO.Path.Combine( root, "*.txt" ) },
				new CommandContext(
					"rm",
					TextReader.Null,
					new StringWriter(),
					new StringWriter()
				)
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( File.Exists( first ) );
			Assert.False( File.Exists( second ) );
			Assert.True( File.Exists( retained ) );
		} finally {
			DeleteTree( root );
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod-Rm-Glob-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}

	private static void DeleteTree( string path ) {
		try {
			if ( Directory.Exists( path ) ) {
				Directory.Delete( path, recursive: true );
			}
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}
}
