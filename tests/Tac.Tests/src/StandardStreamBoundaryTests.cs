namespace Icod.CoreUtils.Tac.Tests;

using Xunit;
using TacCommand = Icod.CoreUtils.Tac.Command;

/// <summary>Verifies the convenience command boundary preserves raw standard streams.</summary>
public sealed class StandardStreamBoundaryTests {
	/// <summary>Verifies explicitly supplied binary standard streams are used without text transcoding.</summary>
	[Fact]
	public async Task UsesExplicitBinaryStandardStreams() {
		using var input = new MemoryStream(
			"a\nb\n"u8.ToArray(),
			writable: false
		);
		using var output = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();

		var status = await TacCommand.RunAsync(
			Array.Empty<string>(),
			standardInput: TextReader.Null,
			standardOutput: textOutput,
			standardError: error,
			standardInputStream: input,
			standardOutputStream: output
		);

		Assert.Equal( 0, status );
		Assert.Equal( "b\na\n"u8.ToArray(), output.ToArray() );
		Assert.Equal( string.Empty, textOutput.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}
}
