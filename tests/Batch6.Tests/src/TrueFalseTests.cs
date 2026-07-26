namespace Icod.CoreUtils.Batch6.Tests;

using FalseCommand = Icod.CoreUtils.False.Command;
using TrueCommand = Icod.CoreUtils.True.Command;
using Xunit;

public sealed class TrueFalseTests {
	[Fact]
	public async Task DefaultStatusesMatchUtilities() {
		var trueResult = await TestSupport.RunAsync(
			TrueCommand.RunAsync,
			Array.Empty<string>()
		);
		var falseResult = await TestSupport.RunAsync(
			FalseCommand.RunAsync,
			Array.Empty<string>()
		);
		Assert.Equal( 0, trueResult.ExitCode );
		Assert.Equal( 1, falseResult.ExitCode );
		Assert.Empty( trueResult.Output );
		Assert.Empty( falseResult.Output );
	}

	[Fact]
	public async Task HelpIsRecognizedOnlyAsSoleOperand() {
		var help = await TestSupport.RunAsync(
			TrueCommand.RunAsync,
			new string[] { "--help" }
		);
		var ignored = await TestSupport.RunAsync(
			TrueCommand.RunAsync,
			new string[] { "--help", "ignored" }
		);
		Assert.Contains( "Usage: true", help.Output );
		Assert.Empty( ignored.Output );
	}

	[Fact]
	public async Task FalseHelpAndVersionRetainFailureStatus() {
		var help = await TestSupport.RunAsync(
			FalseCommand.RunAsync,
			new string[] { "--help" }
		);
		var version = await TestSupport.RunAsync(
			FalseCommand.RunAsync,
			new string[] { "--version" }
		);
		Assert.Equal( 1, help.ExitCode );
		Assert.Equal( 1, version.ExitCode );
		Assert.Contains( "Usage: false", help.Output );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	[Fact]
	public async Task CancellationUsesConventionalStatus() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await TestSupport.RunAsync(
			TrueCommand.RunAsync,
			Array.Empty<string>(),
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 130, result.ExitCode );
	}
}
