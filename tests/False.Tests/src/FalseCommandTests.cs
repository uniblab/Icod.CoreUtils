namespace Icod.CoreUtils.False.Tests;

using FalseCommand = Icod.CoreUtils.False.Command;
using Xunit;

public sealed class FalseCommandTests {
	[Fact]
	public async Task DefaultStatusIsFailureWithoutOutput() {
		var result = await RunAsync(
			Array.Empty<string>()
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task HelpAndVersionRetainFailureStatus() {
		var help = await RunAsync(
			new string[] { "--help" }
		);
		var version = await RunAsync(
			new string[] { "--version" }
		);
		Assert.Equal( 1, help.ExitCode );
		Assert.Contains( "Usage: false", help.Output );
		Assert.Equal( 1, version.ExitCode );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	[Fact]
	public async Task OptionsAreIgnoredWhenAdditionalOperandsArePresent() {
		var result = await RunAsync(
			new string[] { "--help", "ignored" }
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task CancellationUsesConventionalStatus() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync(
			Array.Empty<string>(),
			cancellation.Token
		);
		Assert.Equal( 130, result.ExitCode );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		CancellationToken cancellationToken = default
	) {
		using var output = new StringWriter { NewLine = "\n" };
		using var error = new StringWriter { NewLine = "\n" };
		var exitCode = await FalseCommand.RunAsync(
			args,
			TextReader.Null,
			output,
			error,
			cancellationToken
		);
		return new CommandResult(
			exitCode,
			output.ToString(),
			error.ToString()
		);
	}

	private sealed record CommandResult(
		int ExitCode,
		string Output,
		string Error
	);
}
