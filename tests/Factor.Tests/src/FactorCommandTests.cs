namespace Icod.CoreUtils.Factor.Tests;

using FactorCommand = Icod.CoreUtils.Factor.Command;
using Xunit;

public sealed class FactorCommandTests {
	[Fact]
	public async Task FactorsArgumentsIncludingZeroAndOne() {
		var result = await RunAsync(
			new string[] { "0", "1", "12", "+13" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			Lines( "0:", "1:", "12: 2 2 3", "13: 13" ),
			result.Output
		);
	}

	[Fact]
	public async Task ExponentModeGroupsRepeatedFactors() {
		var result = await RunAsync(
			new string[] { "--exponents", "12", "72" }
		);
		Assert.Equal(
			Lines( "12: 2^2 3", "72: 2^3 3^2" ),
			result.Output
		);
	}

	[Fact]
	public async Task ReadsWhitespaceSeparatedStandardInputAsynchronously() {
		var result = await RunAsync(
			Array.Empty<string>(),
			System.String.Concat( "12 13", Environment.NewLine, "14\t15" )
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			Lines( "12: 2 2 3", "13: 13", "14: 2 7", "15: 3 5" ),
			result.Output
		);
	}

	[Fact]
	public async Task UsesArbitraryPrecisionAndPollardRhoForLargeSemiprime() {
		var result = await RunAsync(
			new string[] { "1000000016000000063" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			Lines( "1000000016000000063: 1000000007 1000000009" ),
			result.Output
		);
	}

	[Fact]
	public async Task InvalidTokensDoNotPreventLaterInputs() {
		var result = await RunAsync(
			new string[] { "12", "invalid", "13" }
		);
		Assert.Equal( 1, result.ExitCode );
		Assert.Equal( Lines( "12: 2 2 3", "13: 13" ), result.Output );
		Assert.Contains( "not a valid positive integer", result.Error );
	}

	[Fact]
	public async Task HandlesLargeStandardInputAndCancellation() {
		var input = string.Join(
			" ",
			Enumerable.Repeat( "2", 5000 )
		);
		var large = await RunAsync(
			Array.Empty<string>(),
			input
		);
		Assert.Equal( 5000, CountOccurrences( large.Output, Environment.NewLine ) );

		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var cancelled = await RunAsync(
			new string[] { "1000000016000000063" },
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 130, cancelled.ExitCode );
	}

	[Fact]
	public async Task HelpAndVersionAreHandled() {
		var help = await RunAsync(
			new string[] { "--help" }
		);
		var version = await RunAsync(
			new string[] { "--version" }
		);
		Assert.Equal( 0, help.ExitCode );
		Assert.Contains( "Usage: factor", help.Output );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		string input = "",
		CancellationToken cancellationToken = default
	) {
		using var inputReader = new StringReader( input );
		using var output = new StringWriter { NewLine = Environment.NewLine };
		using var error = new StringWriter { NewLine = Environment.NewLine };
		var exitCode = await FactorCommand.RunAsync(
			args,
			inputReader,
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
	private static string Lines( params string[] values ) => System.String.Concat(
		string.Join( Environment.NewLine, values ),
		Environment.NewLine
	);

	private static int CountOccurrences( string value, string separator ) {
		var count = 0;
		var offset = 0;
		while ( true ) {
			var index = value.IndexOf( separator, offset, StringComparison.Ordinal );
			if ( 0 > index ) {
				return count;
			}
			count++;
			offset = index + separator.Length;
		}
	}

}
