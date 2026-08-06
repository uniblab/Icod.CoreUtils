namespace Icod.CoreUtils.Dir.Tests;

using Xunit;

/// <summary>Verifies the public <c>dir</c> command boundary over the shared listing engine.</summary>
public sealed class CommandTests {
	/// <summary>Verifies the profile uses the shared option and rendering vocabulary.</summary>
	[Fact]
	public async Task ListsDirectoryWithSharedOptions() {
		var root = Path.Combine( Path.GetTempPath(), "icod-dir-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( root );
		try {
			await File.WriteAllTextAsync( Path.Combine( root, "one file.txt" ), "data" );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await Icod.CoreUtils.Dir.Command.RunAsync(
				new[] { "-1", "--color=never", "--quoting-style=escape", root },
				stdout: output,
				stderr: error
			);

			Assert.Equal( 0, exitCode );
			Assert.Contains( "one file.txt", output.ToString() );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies invalid options receive a controlled usage status.</summary>
	[Fact]
	public async Task RejectsUnknownOption() {
		var error = new StringWriter();
		var exitCode = await Icod.CoreUtils.Dir.Command.RunAsync( new[] { "--definitely-invalid" }, stderr: error );

		Assert.Equal( 2, exitCode );
		Assert.StartsWith( "dir: unrecognized option", error.ToString() );
	}
}
