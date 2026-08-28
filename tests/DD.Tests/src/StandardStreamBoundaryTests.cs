namespace Icod.CoreUtils.DD.Tests;

using Xunit;
using Tool = Icod.CoreUtils.DD.Command;

/// <summary>Verifies the convenience command boundary preserves raw standard streams.</summary>
public sealed class StandardStreamBoundaryTests {
	/// <summary>Verifies explicitly supplied binary standard streams are used without text transcoding.</summary>
	[Fact]
	public async Task CopiesExplicitBinaryStandardStreams() {
		using var input = new MemoryStream(
			new byte[] { 0, 1, 2, 255 },
			writable: false
		);
		using var output = new MemoryStream();
		var error = new StringWriter();

		var exitCode = await Tool.RunAsync(
			[ "status=none" ],
			stdin: TextReader.Null,
			stdout: new StringWriter(),
			stderr: error,
			stdinStream: input,
			stdoutStream: output
		);

		Assert.Equal( 0, exitCode );
		Assert.Equal(
			new byte[] { 0, 1, 2, 255 },
			output.ToArray()
		);
		Assert.Equal(
			string.Empty,
			error.ToString()
		);
	}
}
