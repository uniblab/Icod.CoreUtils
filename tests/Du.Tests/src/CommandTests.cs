namespace Icod.CoreUtils.Du.Tests;

using Icod.CoreUtils.DU;
using Xunit;

/// <summary>Verifies the public <c>du</c> command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies byte-mode recursive output and summarize depth policy.</summary>
	[Fact]
	public async Task ReportsApparentBytesAndSummarizesRoot() {
		var root = CreateTemporaryDirectory();
		try {
			await File.WriteAllBytesAsync( System.IO.Path.Combine( root, "payload.bin" ), new byte[ 37 ] );
			var output = new StringWriter();
			var error = new StringWriter();

			var exitCode = await Icod.CoreUtils.DU.Command.RunAsync(
				new[] { "--bytes", "--summarize", root },
				stdout: output,
				stderr: error
			);

			Assert.Equal( 0, exitCode );
			var lines = output.ToString().Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries );
			Assert.Single( lines );
			Assert.EndsWith( string.Concat( "\t", root ), lines[ 0 ] );
			Assert.Equal( string.Empty, error.ToString() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies NUL-delimited operand input and output.</summary>
	[Fact]
	public async Task ReadsFilesZeroFromStandardInput() {
		var root = CreateTemporaryDirectory();
		try {
			var first = System.IO.Path.Combine( root, "first.bin" );
			var second = System.IO.Path.Combine( root, "second.bin" );
			await File.WriteAllBytesAsync( first, new byte[ 3 ] );
			await File.WriteAllBytesAsync( second, new byte[ 5 ] );
			var input = new StringReader( string.Concat( first, "\0", second, "\0" ) );
			var output = new StringWriter();

			var exitCode = await Icod.CoreUtils.DU.Command.RunAsync(
				new[] { "--bytes", "--null", "--files0-from=-" },
				stdin: input,
				stdout: output
			);

			Assert.Equal( 0, exitCode );
			var records = output.ToString().Split( '\0', StringSplitOptions.RemoveEmptyEntries );
			Assert.Equal( 2, records.Length );
			Assert.Contains( first, records[ 0 ] );
			Assert.Contains( second, records[ 1 ] );
		} finally {
			Directory.Delete( root, true );
		}
	}



	/// <summary>Verifies zero-length NUL-delimited operands receive a controlled diagnostic.</summary>
	[Fact]
	public async Task RejectsEmptyFilesZeroFromOperand() {
		var error = new StringWriter();

		var exitCode = await Icod.CoreUtils.DU.Command.RunAsync(
			new[] { "--files0-from=-" },
			stdin: new StringReader( "\0" ),
			stderr: error
		);

		Assert.Equal( 1, exitCode );
		Assert.Contains( "invalid zero-length file name", error.ToString() );
	}

	/// <summary>Verifies unsupported timestamp words are rejected instead of accepted as extensions.</summary>
	[Theory]
	[InlineData( "--time=mtime" )]
	[InlineData( "--time=birth" )]
	[InlineData( "--time-style=locale" )]
	public void RejectsNonGnuTimestampWords( string argument ) {
		Assert.Throws<DuUsageException>( () => DuOptionParser.Parse( new[] { argument } ) );
	}

	/// <summary>Verifies summarize and all-entry output are mutually exclusive.</summary>
	[Fact]
	public void RejectsSummarizeWithAllEntries() {
		Assert.Throws<DuUsageException>( () => DuOptionParser.Parse( new[] { "--summarize", "--all" } ) );
	}

	/// <summary>Verifies summarize conflicts with a positive explicit maximum depth regardless of option order.</summary>
	[Theory]
	[InlineData( "--summarize", "--max-depth=1" )]
	[InlineData( "--max-depth=1", "--summarize" )]
	[InlineData( "-s", "--max-depth=1" )]
	[InlineData( "--max-depth=1", "-s" )]
	[InlineData( "--summarize", "-d1" )]
	[InlineData( "-d1", "--summarize" )]
	public void RejectsConflictingSummarizeAndDepth( string first, string second ) {
		Assert.Throws<DuUsageException>( () => DuOptionParser.Parse( new[] { first, second } ) );
	}

	/// <summary>Verifies summarize remains compatible with its equivalent explicit depth of zero.</summary>
	[Theory]
	[InlineData( "--summarize", "--max-depth=0" )]
	[InlineData( "--max-depth=0", "--summarize" )]
	[InlineData( "-s", "-d0" )]
	[InlineData( "-d0", "-s" )]
	public void AcceptsSummarizeWithZeroDepth( string first, string second ) {
		var options = DuOptionParser.Parse( new[] { first, second } );

		Assert.True( options.Summarize );
		Assert.True( options.MaximumDepthSpecified );
		Assert.Equal( 0, options.MaximumDepth );
	}

	/// <summary>Verifies the asynchronous command boundary exposes help.</summary>
	[Fact]
	public async Task ReportsHelp() {
		var output = new StringWriter();
		var exitCode = await Icod.CoreUtils.DU.Command.RunAsync( new[] { "--help" }, stdout: output );

		Assert.Equal( 0, exitCode );
		Assert.StartsWith( "Usage: du ", output.ToString() );
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-du-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}
}
