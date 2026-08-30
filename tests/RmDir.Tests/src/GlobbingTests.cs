namespace Icod.CoreUtils.RmDir.Tests;

using Icod.CommandFramework.Diagnostics;
using RmDirCommand = Icod.CoreUtils.RmDir.Command;
using Xunit;

/// <summary>Exercises pathname expansion at the <c>rmdir</c> command boundary.</summary>
public sealed class GlobbingTests {
	/// <summary>Verifies a wildcard selects every matching empty directory.</summary>
	[Fact]
	public async Task ExpandsPathnameOperandsBeforeRemoval() {
		var root = CreateTemporaryDirectory();
		var first = Directory.CreateDirectory( System.IO.Path.Combine( root, "first.tmp" ) ).FullName;
		var second = Directory.CreateDirectory( System.IO.Path.Combine( root, "second.tmp" ) ).FullName;
		var retained = Directory.CreateDirectory( System.IO.Path.Combine( root, "retained.bin" ) ).FullName;
		try {
			var status = await RmDirCommand.RunAsync(
				new[] { System.IO.Path.Combine( root, "*.tmp" ) },
				new CommandContext(
					"rmdir",
					TextReader.Null,
					new StringWriter(),
					new StringWriter()
				)
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( Directory.Exists( first ) );
			Assert.False( Directory.Exists( second ) );
			Assert.True( Directory.Exists( retained ) );
		} finally {
			DeleteTree( root );
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod-RmDir-Glob-", Guid.NewGuid().ToString( "N" ) )
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
