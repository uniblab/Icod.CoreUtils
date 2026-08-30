namespace Icod.CoreUtils.Shred.Tests;

using Icod.CoreUtils.Shred;
using Xunit;

/// <summary>Exercises pathname expansion at the <c>shred</c> command boundary.</summary>
public sealed class GlobbingTests {
	/// <summary>Verifies a wildcard selects every matching shred target.</summary>
	[Fact]
	public async Task ExpandsPathnameOperandsBeforeShredding() {
		var root = CreateTemporaryDirectory();
		var first = System.IO.Path.Combine( root, "first.tmp" );
		var second = System.IO.Path.Combine( root, "second.tmp" );
		var retained = System.IO.Path.Combine( root, "retained.bin" );
		await File.WriteAllTextAsync( first, "one" );
		await File.WriteAllTextAsync( second, "two" );
		await File.WriteAllTextAsync( retained, "keep" );
		try {
			var status = await Command.RunAsync(
				new[] { "-n0", "--remove=unlink", System.IO.Path.Combine( root, "*.tmp" ) },
				stdin: TextReader.Null,
				stdout: new StringWriter(),
				stderr: new StringWriter()
			);
			Assert.Equal( 0, status );
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
			string.Concat( "Icod-Shred-Glob-", Guid.NewGuid().ToString( "N" ) )
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
