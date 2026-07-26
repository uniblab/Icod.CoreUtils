namespace Icod.CoreUtils.Batch6.Tests;

using EchoCommand = Icod.CoreUtils.Echo.Command;
using YesCommand = Icod.CoreUtils.Yes.Command;
using Xunit;

public sealed class EchoYesTests {
	[Fact]
	public async Task EchoSupportsShortOptionClusters() {
		var result = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "-nEne", "alpha\\nbeta" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( "alpha\nbeta", result.Output );
	}

	[Fact]
	public async Task EchoImplementsDocumentedEscapes() {
		var result = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] {
				"-e",
				"\\a\\b\\e\\f\\n\\r\\t\\v\\0101\\x42\\\\"
			}
		);
		Assert.Equal(
			"\a\b\u001B\f\n\r\t\vAB\\\n",
			result.Output
		);
	}

	[Fact]
	public async Task EchoCStopsAllFurtherOutputAndSuppressesNewline() {
		var result = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "-e", "before\\cafter", "ignored" }
		);
		Assert.Equal( "before", result.Output );
	}

	[Fact]
	public async Task EchoPreservesUnknownEscapesAndLiteralDoubleDash() {
		var unknown = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "-e", "a\\qb" }
		);
		var doubleDash = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "--", "-n" }
		);
		Assert.Equal( "a\\qb\n", unknown.Output );
		Assert.Equal( "-- -n\n", doubleDash.Output );
	}

	[Fact]
	public async Task EchoHonorsPosixlyCorrectOptionRules() {
		using var environment = new EnvironmentVariableScope(
			"POSIXLY_CORRECT",
			"1"
		);
		var literalOption = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "-e", "alpha\\nbeta" }
		);
		var leadingN = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "-n", "-E", "alpha\\nbeta" }
		);
		var literalHelp = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "--help" }
		);
		Assert.Equal( "-e alpha\nbeta\n", literalOption.Output );
		Assert.Equal( "alpha\nbeta", leadingN.Output );
		Assert.Equal( "--help\n", literalHelp.Output );
	}

	[Fact]
	public async Task EchoHandlesLargeOutputWithoutChangingContents() {
		var payload = new string( 'x', 2 * 1024 * 1024 );
		var result = await TestSupport.RunAsync(
			EchoCommand.RunAsync,
			new string[] { "-n", payload }
		);
		Assert.Equal( payload, result.Output );
	}

	[Fact]
	public async Task YesDefaultsToYAndIsCancellable() {
		using var cancellation = new CancellationTokenSource();
		var output = new CancellingTextWriter(
			cancellation,
			70_000
		);
		using var error = new StringWriter();
		var exitCode = await YesCommand.RunAsync(
			Array.Empty<string>(),
			new StringReader( string.Empty ),
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, exitCode );
		Assert.StartsWith( "y\ny\ny\n", output.Output );
		Assert.True( output.Output.Length >= 70_000 );
	}

	[Fact]
	public async Task YesJoinsOperandsAndHandlesBrokenPipe() {
		using var cancellation = new CancellationTokenSource();
		var output = new CancellingTextWriter(
			cancellation,
			80
		);
		using var error = new StringWriter();
		var exitCode = await YesCommand.RunAsync(
			new string[] { "alpha", "beta" },
			new StringReader( string.Empty ),
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, exitCode );
		Assert.StartsWith( "alpha beta\nalpha beta\n", output.Output );

		var broken = await TestSupport.RunAsync(
			YesCommand.RunAsync,
			Array.Empty<string>(),
			output: new ThrowingTextWriter()
		);
		Assert.Equal( 1, broken.ExitCode );
		Assert.Contains( "simulated broken pipe", broken.Error );
	}
}
