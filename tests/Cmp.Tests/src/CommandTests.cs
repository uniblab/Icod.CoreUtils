namespace Icod.DiffUtils.Cmp.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.DiffUtils.Cmp;
using Icod.DiffUtils.Shared;
using Xunit;

/// <summary>Exercises GNU-compatible <c>cmp</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies equal inputs and the equality status.</summary>
	[Fact]
	public async Task EqualFilesProduceNoOutput() {
		await WithFilesAsync(
			new byte[] { 0, 1, 2, 255 },
			new byte[] { 0, 1, 2, 255 },
			async ( first, second ) => {
				var result = await RunAsync( new[] { first, second } );
				Assert.Equal( (int)ComparisonStatus.Equal, result.Status );
				Assert.Empty( result.Output );
				Assert.Empty( result.Error );
			}
		);
	}

	/// <summary>Verifies the default first-difference report and line number.</summary>
	[Fact]
	public async Task DefaultModeReportsFirstDifference() {
		await WithFilesAsync(
			Encoding.UTF8.GetBytes( "same\nabc\n" ),
			Encoding.UTF8.GetBytes( "same\naxc\n" ),
			async ( first, second ) => {
				var result = await RunAsync( new[] { first, second } );
				Assert.Equal( (int)ComparisonStatus.Different, result.Status );
				Assert.Equal(
					$"{first} {second} differ: char 7, line 2{Environment.NewLine}",
					result.Output
				);
				Assert.Empty( result.Error );
			}
		);
	}

	/// <summary>Verifies visible byte notation in first-difference mode.</summary>
	[Fact]
	public async Task PrintBytesShowsOctalAndVisibleValues() {
		await WithFilesAsync(
			new byte[] { (byte)'a', 0 },
			new byte[] { (byte)'a', 127 },
			async ( first, second ) => {
				var result = await RunAsync( new[] { "-b", first, second } );
				Assert.Equal( (int)ComparisonStatus.Different, result.Status );
				Assert.Contains( "differ: byte 2, line 1 is   0 ^@ 177 ^?", result.Output );
			}
		);
	}

	/// <summary>Verifies all-differences reporting and byte positions.</summary>
	[Fact]
	public async Task VerboseModeReportsEveryDifference() {
		await WithFilesAsync(
			new byte[] { 1, 2, 3, 4 },
			new byte[] { 1, 9, 3, 8 },
			async ( first, second ) => {
				var result = await RunAsync( new[] { "-l", first, second } );
				Assert.Equal( (int)ComparisonStatus.Different, result.Status );
				Assert.Equal(
					string.Concat(
						"2   2  11", Environment.NewLine,
						"4   4  10", Environment.NewLine
					),
					result.Output
				);
				Assert.Empty( result.Error );
			}
		);
	}

	/// <summary>Verifies that quiet mode suppresses difference output.</summary>
	[Fact]
	public async Task QuietModeSuppressesDifferenceOutput() {
		await WithFilesAsync(
			new byte[] { 1 },
			new byte[] { 2 },
			async ( first, second ) => {
				var result = await RunAsync( new[] { "-s", first, second } );
				Assert.Equal( (int)ComparisonStatus.Different, result.Status );
				Assert.Empty( result.Output );
				Assert.Empty( result.Error );
			}
		);
	}

	/// <summary>Verifies that quiet mode suppresses operational diagnostics.</summary>
	[Fact]
	public async Task QuietModeSuppressesOperationalErrors() {
		var missing = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) );
		var result = await RunAsync( new[] { "-s", missing, missing } );
		Assert.Equal( (int)ComparisonStatus.Trouble, result.Status );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies EOF diagnostics and the line containing the final byte.</summary>
	[Fact]
	public async Task ShorterInputReportsEof() {
		await WithFilesAsync(
			Encoding.UTF8.GetBytes( "a\n" ),
			Encoding.UTF8.GetBytes( "a\nb" ),
			async ( first, second ) => {
				var result = await RunAsync( new[] { first, second } );
				Assert.Equal( (int)ComparisonStatus.Different, result.Status );
				Assert.Empty( result.Output );
				Assert.Equal(
					$"cmp: EOF on {first} after byte 2, line 1{Environment.NewLine}",
					result.Error
				);
			}
		);
	}

	/// <summary>Verifies verbose EOF diagnostics omit line information.</summary>
	[Fact]
	public async Task VerboseEofOmitsLineNumber() {
		await WithFilesAsync(
			new byte[] { 1 },
			new byte[] { 1, 2 },
			async ( first, second ) => {
				var result = await RunAsync( new[] { "-l", first, second } );
				Assert.Equal( (int)ComparisonStatus.Different, result.Status );
				Assert.Equal( $"cmp: EOF on {first} after byte 1{Environment.NewLine}", result.Error );
			}
		);
	}

	/// <summary>Verifies option and positional skips are additive.</summary>
	[Fact]
	public async Task OptionAndPositionalSkipsAreAdditive() {
		await WithFilesAsync(
			Encoding.ASCII.GetBytes( "XXabc" ),
			Encoding.ASCII.GetBytes( "YYYabc" ),
			async ( first, second ) => {
				var result = await RunAsync( new[] { "-i", "1:2", first, second, "1", "1" } );
				Assert.Equal( (int)ComparisonStatus.Equal, result.Status );
				Assert.Empty( result.Output );
				Assert.Empty( result.Error );
			}
		);
	}

	/// <summary>Verifies hexadecimal limits and zero-length comparisons.</summary>
	[Fact]
	public async Task LimitAcceptsRadixNotation() {
		await WithFilesAsync(
			new byte[] { 1, 2, 3 },
			new byte[] { 1, 2, 9 },
			async ( first, second ) => {
				var equalPrefix = await RunAsync( new[] { "-n", "0x2", first, second } );
				Assert.Equal( (int)ComparisonStatus.Equal, equalPrefix.Status );
				var zero = await RunAsync( new[] { "-n", "00", first, second } );
				Assert.Equal( (int)ComparisonStatus.Equal, zero.Status );
			}
		);
	}

	/// <summary>Verifies raw binary standard input when FILE2 is omitted.</summary>
	[Fact]
	public async Task OmittedSecondFileUsesBinaryStandardInput() {
		var bytes = new byte[] { 0, 255, 10, 128 };
		await WithFileAsync(
			bytes,
			async first => {
				var result = await RunAsync( new[] { first }, bytes );
				Assert.Equal( (int)ComparisonStatus.Equal, result.Status );
			}
		);
	}

	/// <summary>Verifies GNU's repeated-standard-input identity behavior.</summary>
	[Fact]
	public async Task RepeatedStandardInputIsTheSameOperand() {
		var result = await RunAsync( new[] { "-i", "1:2", "-", "-" }, new byte[] { 1, 2, 3 } );
		Assert.Equal( (int)ComparisonStatus.Equal, result.Status );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies pre-cancellation precedes the repeated-input fast path.</summary>
	[Fact]
	public async Task RepeatedStandardInputHonorsCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync(
			new[] { "-", "-" },
			new byte[] { 1, 2, 3 },
			cancellation.Token
		);
		Assert.Equal( CommandExitCodes.Canceled, result.Status );
	}

	/// <summary>Verifies invalid skips are diagnosed before excess operands.</summary>
	[Fact]
	public async Task InvalidSkipPrecedesExtraOperandDiagnostic() {
		var result = await RunAsync( new[] { "a", "b", "invalid", "2", "extra" } );
		Assert.Equal( (int)ComparisonStatus.Trouble, result.Status );
		Assert.Contains( "invalid --ignore-initial value 'invalid'", result.Error );
		Assert.DoesNotContain( "extra operand", result.Error );
	}

	/// <summary>Verifies incompatible reporting modes are rejected.</summary>
	[Fact]
	public async Task VerboseAndQuietAreIncompatible() {
		var result = await RunAsync( new[] { "-ls", "a", "b" } );
		Assert.Equal( (int)ComparisonStatus.Trouble, result.Status );
		Assert.Contains( "options -l and -s are incompatible", result.Error );
		Assert.Contains( "Try 'cmp --help'", result.Error );
	}

	/// <summary>Verifies help and version return success without opening operands.</summary>
	[Theory]
	[InlineData( "--help", "Usage: cmp" )]
	[InlineData( "--version", "cmp (Icod.DiffUtils)" )]
	public async Task HelpAndVersionSucceed( string option, string expected ) {
		var result = await RunAsync( new[] { option } );
		Assert.Equal( (int)ComparisonStatus.Equal, result.Status );
		Assert.Contains( expected, result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies cancellation uses the repository-wide cancellation status.</summary>
	[Fact]
	public async Task CancellationReturnsCanceledStatus() {
		await WithFilesAsync(
			new byte[] { 1 },
			new byte[] { 1 },
			async ( first, second ) => {
				using var cancellation = new CancellationTokenSource();
				cancellation.Cancel();
				var result = await RunAsync( new[] { first, second }, cancellationToken: cancellation.Token );
				Assert.Equal( CommandExitCodes.Canceled, result.Status );
			}
		);
	}

	private static async Task<(int Status, string Output, string Error)> RunAsync(
		IReadOnlyList<string> arguments,
		byte[]? stdinBytes = null,
		CancellationToken cancellationToken = default
	) {
		using var stdin = new MemoryStream( stdinBytes ?? Array.Empty<byte>() );
		using var output = new StringWriter();
		using var error = new StringWriter();
		var status = await Command.RunAsync(
			arguments,
			TextReader.Null,
			output,
			error,
			cancellationToken,
			stdin
		);
		return ( status, output.ToString(), error.ToString() );
	}

	private static async Task WithFileAsync( byte[] bytes, Func<string, Task> action ) {
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "cmp-", Guid.NewGuid().ToString( "N" ) ) );
		try {
			await File.WriteAllBytesAsync( path, bytes );
			await action( path );
		} finally {
			File.Delete( path );
		}
	}

	private static async Task WithFilesAsync(
		byte[] firstBytes,
		byte[] secondBytes,
		Func<string, string, Task> action
	) {
		await WithFileAsync(
			firstBytes,
			first => WithFileAsync( secondBytes, second => action( first, second ) )
		);
	}
}
