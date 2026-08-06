namespace Icod.CoreUtils.Ls.Tests;

using Xunit;

/// <summary>Verifies the public <c>ls</c> command boundary over the shared listing engine.</summary>
public sealed class CommandTests {
	/// <summary>Verifies deterministic single-column directory listing and classification.</summary>
	[Fact]
	public async Task ListsDirectoryThroughSharedEngine() {
		var root = CreateTemporaryDirectory();
		try {
			await File.WriteAllTextAsync( Path.Combine( root, "alpha.txt" ), "a" );
			await File.WriteAllTextAsync( Path.Combine( root, "beta.txt" ), "b" );
			Directory.CreateDirectory( Path.Combine( root, "nested" ) );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await Icod.CoreUtils.Ls.Command.RunAsync(
				new[] { "-1F", "--color=never", "--quoting-style=literal", root },
				stdout: output,
				stderr: error
			);

			Assert.Equal( 0, exitCode );
			var lines = ReadLines( output.ToString() );
			Assert.Contains( "alpha.txt", lines );
			Assert.Contains( "beta.txt", lines );
			Assert.Contains( "nested/", lines );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies the asynchronous boundary exposes help.</summary>
	[Fact]
	public async Task ReportsHelp() {
		var output = new StringWriter();
		var exitCode = await Icod.CoreUtils.Ls.Command.RunAsync( new[] { "--help" }, stdout: output );

		Assert.Equal( 0, exitCode );
		Assert.StartsWith( "Usage: ls ", output.ToString() );
	}

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine( Path.GetTempPath(), "icod-ls-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static string[] ReadLines( string value ) {
		return value.Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries );
	}
}
