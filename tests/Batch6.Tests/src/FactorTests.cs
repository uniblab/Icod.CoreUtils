namespace Icod.CoreUtils.Batch6.Tests;

using FactorCommand = Icod.CoreUtils.Factor.Command;
using Xunit;

public sealed class FactorTests {
	[Fact]
	public async Task FactorsArgumentsIncludingZeroAndOne() {
		var result = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			new string[] { "0", "1", "12", "+13" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			"0:\n1:\n12: 2 2 3\n13: 13\n",
			result.Output
		);
	}

	[Fact]
	public async Task ExponentModeGroupsRepeatedFactors() {
		var result = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			new string[] { "--exponents", "12", "72" }
		);
		Assert.Equal( "12: 2^2 3\n72: 2^3 3^2\n", result.Output );
	}

	[Fact]
	public async Task ReadsWhitespaceSeparatedStandardInputAsynchronously() {
		var result = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			Array.Empty<string>(),
			"12 13\n14\t15"
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			"12: 2 2 3\n13: 13\n14: 2 7\n15: 3 5\n",
			result.Output
		);
	}

	[Fact]
	public async Task UsesArbitraryPrecisionAndPollardRhoForLargeSemiprime() {
		var result = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			new string[] { "1000000016000000063" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			"1000000016000000063: 1000000007 1000000009\n",
			result.Output
		);
	}

	[Fact]
	public async Task InvalidTokensDoNotPreventLaterInputs() {
		var result = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			new string[] { "12", "invalid", "13" }
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Equal( "12: 2 2 3\n13: 13\n", result.Output );
		Assert.Contains( "not a valid positive integer", result.Error );
	}

	[Fact]
	public async Task HandlesLargeStandardInputAndCancellation() {
		var input = string.Join(
			" ",
			Enumerable.Repeat( "2", 5000 )
		);
		var large = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			Array.Empty<string>(),
			input
		);
		Assert.Equal( 5000, large.Output.Count( value => '\n' == value ) );

		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var cancelled = await TestSupport.RunAsync(
			FactorCommand.RunAsync,
			new string[] { "1000000016000000063" },
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 130, cancelled.ExitCode );
	}
}
