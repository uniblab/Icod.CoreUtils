namespace Icod.CoreUtils.Seq.Tests;

using System.Globalization;
using System.Text;
using SeqCommand = Icod.CoreUtils.Seq.Command;
using Xunit;

public sealed class SeqCommandTests {
	[Fact]
	public async Task SupportsAllSynopsisFormsAndDescendingRanges() {
		var lastOnly = await RunAsync(
			new string[] { "3" }
		);
		var firstLast = await RunAsync(
			new string[] { "2", "4" }
		);
		var descending = await RunAsync(
			new string[] { "3", "-1", "1" }
		);
		Assert.Equal( Lines( "1", "2", "3" ), lastOnly.Output );
		Assert.Equal( Lines( "2", "3", "4" ), firstLast.Output );
		Assert.Equal( Lines( "3", "2", "1" ), descending.Output );
	}

	[Fact]
	public async Task SupportsSeparatorEqualWidthAndPrintfFormat() {
		var separated = await RunAsync(
			new string[] { "-s,", "3" }
		);
		var padded = await RunAsync(
			new string[] { "-w", "-2", "2" }
		);
		var formatted = await RunAsync(
			new string[] { "-f", "x%+06.2fy", "1", "2" }
		);
		Assert.Equal( Lines( "1,2,3" ), separated.Output );
		Assert.Equal( Lines( "-2", "-1", "00", "01", "02" ), padded.Output );
		Assert.Equal( Lines( "x+01.00y", "x+02.00y" ), formatted.Output );
	}

	[Fact]
	public async Task UsesInvariantNumericSyntax() {
		using var culture = new CultureScope( "fr-FR" );
		var result = await RunAsync(
			new string[] { "1", "0.1", "1.2" }
		);
		Assert.Equal( Lines( "1.0", "1.1", "1.2" ), result.Output );
	}

	[Fact]
	public async Task SupportsInfinityAndHexadecimalPrintf() {
		var hexadecimal = await RunAsync(
			new string[] { "-f", "%a", "1", "2" }
		);
		Assert.Equal( Lines( "0x8p-3", "0x8p-2" ), hexadecimal.Output );

		using var cancellation = new CancellationTokenSource();
		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 25 ) );
		var infinity = await RunAsync(
			new string[] { "inf" },
			cancellation.Token
		);
		Assert.Equal( 130, infinity.ExitCode );
	}

	[Fact]
	public async Task PreservesNegativeZeroAndAcceptsLongDoubleFormat() {
		var negativeZero = await RunAsync(
			new string[] { "--", "-0.0", "1", "2" }
		);
		var longDouble = await RunAsync(
			new string[] { "-f", "%Lg", "1", "2" }
		);
		Assert.Equal( Lines( "-0.0", "1.0", "2.0" ), negativeZero.Output );
		Assert.Equal( Lines( "1", "2" ), longDouble.Output );
	}

	[Fact]
	public async Task RepeatsInfiniteEndpointsUntilCancelled() {
		using var cancellation = new CancellationTokenSource();
		var output = new CancellingTextWriter(
			cancellation,
			128
		);
		using var error = new StringWriter { NewLine = Environment.NewLine };
		var exitCode = await SeqCommand.RunAsync(
			new string[] { "inf", "inf" },
			TextReader.Null,
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, exitCode );
		Assert.StartsWith( Lines( "inf", "inf" ), output.Output );
	}

	[Fact]
	public async Task RejectsConflictingAndInvalidArguments() {
		var conflict = await RunAsync(
			new string[] { "-w", "-f", "%g", "1", "2" }
		);
		var zero = await RunAsync(
			new string[] { "1", "0", "2" }
		);
		Assert.Equal( 1, conflict.ExitCode );
		Assert.Contains( "may not be specified", conflict.Error );
		Assert.Equal( 1, zero.ExitCode );
		Assert.Contains( "zero increment", zero.Error );
	}

	[Fact]
	public async Task StreamsLargeRangesAndHonorsCancellation() {
		var large = await RunAsync(
			new string[] { "1", "10000" }
		);
		Assert.Equal( 10_000, CountOccurrences( large.Output, Environment.NewLine ) );
		Assert.StartsWith( Lines( "1", "2" ), large.Output );
		Assert.EndsWith( Lines( "9999", "10000" ), large.Output );

		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var cancelled = await RunAsync(
			new string[] { "1", "1000000000" },
			cancellation.Token
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
		Assert.Contains( "Usage: seq", help.Output );
		Assert.Equal( 0, version.ExitCode );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		CancellationToken cancellationToken = default
	) {
		using var output = new StringWriter(
			CultureInfo.InvariantCulture
		) { NewLine = Environment.NewLine };
		using var error = new StringWriter(
			CultureInfo.InvariantCulture
		) { NewLine = Environment.NewLine };
		var exitCode = await SeqCommand.RunAsync(
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

	private sealed class CancellingTextWriter : TextWriter {
		private readonly CancellationTokenSource myCancellation;
		private readonly int myLimit;
		private readonly StringBuilder myOutput = new();

		public override Encoding Encoding => Encoding.UTF8;
		public string Output => this.myOutput.ToString();

		public CancellingTextWriter(
			CancellationTokenSource cancellation,
			int limit
		) {
			this.myCancellation = cancellation;
			this.myLimit = limit;
		}

		public override Task WriteAsync(
			char[] buffer,
			int index,
			int count
		) {
			this.AppendAndMaybeCancel(
				buffer.AsSpan( index, count )
			);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.AppendAndMaybeCancel( buffer.Span );
			return Task.CompletedTask;
		}

		private void AppendAndMaybeCancel(
			ReadOnlySpan<char> value
		) {
			var remaining = this.myLimit - this.myOutput.Length;
			if ( 0 < remaining ) {
				this.myOutput.Append(
					value[ ..Math.Min( remaining, value.Length ) ]
				);
			}
			if ( this.myOutput.Length >= this.myLimit ) {
				this.myCancellation.Cancel();
			}
		}
	}

	private sealed class CultureScope : IDisposable {
		private readonly CultureInfo myCulture;
		private readonly CultureInfo myUiCulture;

		public CultureScope(
			string cultureName
		) {
			this.myCulture = CultureInfo.CurrentCulture;
			this.myUiCulture = CultureInfo.CurrentUICulture;
			CultureInfo.CurrentCulture = new CultureInfo( cultureName );
			CultureInfo.CurrentUICulture = new CultureInfo( cultureName );
		}

		public void Dispose() {
			CultureInfo.CurrentCulture = this.myCulture;
			CultureInfo.CurrentUICulture = this.myUiCulture;
		}
	}
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
