namespace Icod.CoreUtils.DirColors.Tests;

using Xunit;

/// <summary>Verifies the public <c>dircolors</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies database input produces Bourne-compatible shell output.</summary>
	[Fact]
	public async Task EmitsBourneShellAssignment() {
		var output = new StringWriter();
		var error = new StringWriter();

		var exitCode = await Icod.CoreUtils.DirColors.Command.RunAsync(
			new[] { "-b", "-" },
			new StringReader( "DIR 01;34" + Environment.NewLine ),
			output,
			error
		);

		Assert.Equal( 0, exitCode );
		Assert.StartsWith( "LS_COLORS='di=01;34';", output.ToString() );
		Assert.Contains( "export LS_COLORS", output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies the built-in database can be printed without shell inference.</summary>
	[Fact]
	public async Task PrintsBuiltInDatabase() {
		var output = new StringWriter();
		var error = new StringWriter();

		var exitCode = await Icod.CoreUtils.DirColors.Command.RunAsync( new[] { "--print-database" }, stdout: output, stderr: error );

		Assert.Equal( 0, exitCode );
		Assert.Contains( "DIR 01;34", output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}
}
