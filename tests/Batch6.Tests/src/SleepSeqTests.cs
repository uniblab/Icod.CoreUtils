namespace Icod.CoreUtils.Batch6.Tests;

using SeqCommand = Icod.CoreUtils.Seq.Command;
using SleepCommand = Icod.CoreUtils.Sleep.Command;
using Xunit;

public sealed class SleepSeqTests {
	[Fact]
	public async Task SleepAcceptsAllSuffixesAndMultipleOperands() {
		var result = await TestSupport.RunAsync(
			SleepCommand.RunAsync,
			new string[] { "0s", "0m", "0h", "0d" }
		);
		Assert.Equal( 0, result.ExitCode );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task SleepRejectsMissingAndInvalidDurations() {
		var missing = await TestSupport.RunAsync(
			SleepCommand.RunAsync,
			Array.Empty<string>()
		);
		var invalid = await TestSupport.RunAsync(
			SleepCommand.RunAsync,
			new string[] { "1fortnight" }
		);
		Assert.Equal( 1, missing.ExitCode );
		Assert.Contains( "missing operand", missing.Error );
		Assert.Equal( 1, invalid.ExitCode );
		Assert.Contains( "invalid time interval", invalid.Error );
	}

	[Fact]
	public async Task SleepIsCancellableWithoutBlockingAWorkerThread() {
		using var cancellation = new CancellationTokenSource();
		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 25 ) );
		var result = await TestSupport.RunAsync(
			SleepCommand.RunAsync,
			new string[] { "inf" },
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 130, result.ExitCode );
	}

	[Fact]
	public async Task SleepAndSeqUseInvariantNumericSyntax() {
		using var culture = new CultureScope( "fr-FR" );
		var sleep = await TestSupport.RunAsync(
			SleepCommand.RunAsync,
			new string[] { "0.0" }
		);
		var sequence = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "1", "0.1", "1.2" }
		);
		Assert.Equal( 0, sleep.ExitCode );
		Assert.Equal( "1.0\n1.1\n1.2\n", sequence.Output );
	}

	[Fact]
	public async Task SeqSupportsAllSynopsisFormsAndDescendingRanges() {
		var lastOnly = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "3" }
		);
		var firstLast = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "2", "4" }
		);
		var descending = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "3", "-1", "1" }
		);
		Assert.Equal( "1\n2\n3\n", lastOnly.Output );
		Assert.Equal( "2\n3\n4\n", firstLast.Output );
		Assert.Equal( "3\n2\n1\n", descending.Output );
	}

	[Fact]
	public async Task SeqSupportsSeparatorEqualWidthAndPrintfFormat() {
		var separated = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "-s,", "3" }
		);
		var padded = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "-w", "-2", "2" }
		);
		var formatted = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "-f", "x%+06.2fy", "1", "2" }
		);
		Assert.Equal( "1,2,3\n", separated.Output );
		Assert.Equal( "-2\n-1\n00\n01\n02\n", padded.Output );
		Assert.Equal( "x+01.00y\nx+02.00y\n", formatted.Output );
	}

	[Fact]
	public async Task SeqSupportsInfinityAndHexadecimalPrintf() {
		var hexadecimal = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "-f", "%a", "1", "2" }
		);
		Assert.Equal( "0x8p-3\n0x8p-2\n", hexadecimal.Output );

		using var cancellation = new CancellationTokenSource();
		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 25 ) );
		var infinity = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "inf" },
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 130, infinity.ExitCode );
	}

	[Fact]
	public async Task SeqPreservesNegativeZeroAndAcceptsLongDoubleFormat() {
		var negativeZero = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "--", "-0.0", "1", "2" }
		);
		var longDouble = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "-f", "%Lg", "1", "2" }
		);
		Assert.Equal( "-0.0\n1.0\n2.0\n", negativeZero.Output );
		Assert.Equal( "1\n2\n", longDouble.Output );
	}

	[Fact]
	public async Task SeqRepeatsInfiniteEndpointsUntilCancelled() {
		using var cancellation = new CancellationTokenSource();
		var output = new CancellingTextWriter(
			cancellation,
			128
		);
		using var error = new StringWriter();
		var exitCode = await SeqCommand.RunAsync(
			new string[] { "inf", "inf" },
			new StringReader( string.Empty ),
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, exitCode );
		Assert.StartsWith( "inf\ninf\n", output.Output );
	}

	[Fact]
	public async Task SeqRejectsConflictingAndInvalidArguments() {
		var conflict = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "-w", "-f", "%g", "1", "2" }
		);
		var zero = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "1", "0", "2" }
		);
		Assert.Equal( 1, conflict.ExitCode );
		Assert.Contains( "may not be specified", conflict.Error );
		Assert.Equal( 1, zero.ExitCode );
		Assert.Contains( "zero increment", zero.Error );
	}

	[Fact]
	public async Task SeqStreamsLargeRangesAndHonorsCancellation() {
		var large = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "1", "10000" }
		);
		Assert.Equal( 10_000, large.Output.Count( value => '\n' == value ) );
		Assert.StartsWith( "1\n2\n", large.Output );
		Assert.EndsWith( "9999\n10000\n", large.Output );

		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var cancelled = await TestSupport.RunAsync(
			SeqCommand.RunAsync,
			new string[] { "1", "1000000000" },
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 130, cancelled.ExitCode );
	}
}
