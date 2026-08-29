namespace Icod.CoreUtils.VDir.Tests;

using Xunit;

/// <summary>Verifies the public <c>vdir</c> command boundary over the shared listing engine.</summary>
public sealed class CommandTests {
	/// <summary>Verifies the profile defaults to metadata-backed long output.</summary>
	[Fact]
	public async Task DefaultsToLongListing() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), "icod-vdir-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( root );
		try {
			await File.WriteAllTextAsync( System.IO.Path.Combine( root, "entry.txt" ), "payload" );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await Icod.CoreUtils.VDir.Command.RunAsync(
				new[] { "--color=never", "--quoting-style=literal", root },
				stdout: output,
				stderr: error
			);

			Assert.Equal( 0, exitCode );
			Assert.StartsWith( "total ", output.ToString() );
			Assert.Contains( "entry.txt", output.ToString() );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies version output retains the vdir executable identity.</summary>
	[Fact]
	public async Task ReportsVersion() {
		var output = new StringWriter();
		var exitCode = await Icod.CoreUtils.VDir.Command.RunAsync( new[] { "--version" }, stdout: output );

		Assert.Equal( 0, exitCode );
		Assert.StartsWith( "vdir (Icod.CoreUtils)", output.ToString() );
	}
}
