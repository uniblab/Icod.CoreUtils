namespace Icod.CoreUtils.Comm.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests sorted-stream comparison, formatting, order checks, and control paths.</summary>
public sealed class CommandTests {
	/// <summary>Verifies the conventional three-column merge.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ProducesThreeConventionalColumns() {
		using var first = new TemporaryFile( "a\nc\n"u8.ToArray() );
		using var second = new TemporaryFile( "b\nc\n"u8.ToArray() );
		var result = await RunAsync( [ first.Path, second.Path ] );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a\n\tb\n\t\tc\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies column suppression, custom delimiters, and totals.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsSuppressionDelimitersAndTotals() {
		var common = await RunAsync( [ "-12", first.Path, second.Path ] );
		var detailed = await RunAsync( [ "--output-delimiter=|", "--total", first.Path, second.Path ] );
		Assert.Equal( "c\n"u8.ToArray(), common.Output );
		Assert.Equal( "a\n|b\n||c\n1|1|1|total\n"u8.ToArray(), detailed.Output );
	}

	/// <summary>Verifies NUL-delimited records and empty output delimiters.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsNullRecordsAndNullColumnDelimiter() {
		using var first = new TemporaryFile( new byte[] { (byte)'a', 0, (byte)'c', 0 } );
		using var second = new TemporaryFile( new byte[] { (byte)'b', 0, (byte)'c', 0 } );
		var result = await RunAsync( [ "-z", "--output-delimiter=", first.Path, second.Path ] );
		Assert.Equal(
			new byte[] { (byte)'a', 0, 0, (byte)'b', 0, 0, 0, (byte)'c', 0 },
			result.Output
		);
	}

	/// <summary>Verifies sorted-input diagnostics and explicit check suppression.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ChecksOrIgnoresInputOrder() {
		using var first = new TemporaryFile( "b\na\n"u8.ToArray() );
		using var second = new TemporaryFile( "c\n"u8.ToArray() );
		var checkedResult = await RunAsync( [ "--check-order", first.Path, second.Path ] );
		var uncheckedResult = await RunAsync( [ "--nocheck-order", first.Path, second.Path ] );
		Assert.Equal( CommandExitCodes.Failure, checkedResult.Status );
		Assert.Contains( "not in sorted order", checkedResult.Error );
		Assert.Equal( CommandExitCodes.Success, uncheckedResult.Status );
	}

	/// <summary>Verifies GNU's deferred default order check.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DefaultOrderCheckFailsOnlyAfterUnpairableData() {
		using var pairedFirst = new TemporaryFile( "b\na\n"u8.ToArray() );
		using var pairedSecond = new TemporaryFile( "b\na\n"u8.ToArray() );
		using var unpairedSecond = new TemporaryFile( "c\n"u8.ToArray() );
		var paired = await RunAsync( [ pairedFirst.Path, pairedSecond.Path ] );
		var unpaired = await RunAsync( [ pairedFirst.Path, unpairedSecond.Path ] );
		Assert.Equal( CommandExitCodes.Success, paired.Status );
		Assert.Equal( CommandExitCodes.Failure, unpaired.Status );
		Assert.Contains( "not in sorted order", unpaired.Error );
	}

	/// <summary>Verifies one standard-input operand and rejection of two standard-input operands.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsOneStandardInputOperand() {
		using var second = new TemporaryFile( "b\n"u8.ToArray() );
		var valid = await RunAsync( [ "-", second.Path ], "a\n"u8.ToArray() );
		var invalid = await RunAsync( [ "-", "-" ], "a\n"u8.ToArray() );
		Assert.Equal( "a\n\tb\n"u8.ToArray(), valid.Output );
		Assert.Equal( CommandExitCodes.Failure, invalid.Status );
		Assert.Contains( "both files", invalid.Error );
	}

	/// <summary>Verifies help, version, and operand errors.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ] );
		var version = await RunAsync( [ "--version" ] );
		var missing = await RunAsync( [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: comm", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "comm (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, missing.Status );
		Assert.Contains( "missing operand", missing.Error );
	}

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync(
		string[] args,
		byte[]? input = null
	) {
		using var inputStream = new MemoryStream( input ?? [], writable: false );
		using var outputStream = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext(
			"comm",
			new StringReader( string.Empty ),
			textOutput,
			error,
			inputStream,
			outputStream
		);
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
	}

	private sealed class TemporaryFile : IDisposable {
		public TemporaryFile( byte[] contents ) {
			this.Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-comm-test-", Guid.NewGuid().ToString( "N" ) ) );
			File.WriteAllBytes( this.Path, contents );
		}

		public string Path { get; }

		void IDisposable.Dispose() {
			try {
				File.Delete( this.Path );
			} catch {
				// Test cleanup must not mask assertions.
			}
		}
	}
}
