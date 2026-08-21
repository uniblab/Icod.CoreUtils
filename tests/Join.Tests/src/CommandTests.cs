namespace Icod.CoreUtils.Join.Tests;

using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests sorted relational joining, duplicate groups, fields, and control paths.</summary>
public sealed class CommandTests {
	/// <summary>Verifies Cartesian output for duplicate join keys.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DuplicateKeysProduceCartesianProducts() {
		using var first = new TemporaryFile( "a 1\na 2\nb 3\n"u8.ToArray() );
		using var second = new TemporaryFile( "a x\na y\nc z\n"u8.ToArray() );
		var result = await RunAsync( [ first.Path, second.Path ] );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a 1 x\na 1 y\na 2 x\na 2 y\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies paired and unpaired selection modes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsAlsoUnpairedAndUnpairedOnlyModes() {
		using var first = new TemporaryFile( "a 1\nb 2\n"u8.ToArray() );
		using var second = new TemporaryFile( "a x\nc y\n"u8.ToArray() );
		var all = await RunAsync( [ "-a1", "-a2", first.Path, second.Path ] );
		var firstOnly = await RunAsync( [ "-v1", first.Path, second.Path ] );
		Assert.Equal( "a 1 x\nb 2\nc y\n"u8.ToArray(), all.Output );
		Assert.Equal( "b 2\n"u8.ToArray(), firstOnly.Output );
	}

	/// <summary>Verifies independent join fields and explicit output formats.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsFieldSelectionAndOutputFormats() {
		using var first = new TemporaryFile( "left:k:one\nmissing:q:\n"u8.ToArray() );
		using var second = new TemporaryFile( "k:right\nq:\n"u8.ToArray() );
		var result = await RunAsync(
			[ "-t:", "-1", "2", "-2", "1", "-e", "EMPTY", "-o", "0,1.1,1.3,2.2", first.Path, second.Path ]
		);
		Assert.Equal( "k:left:one:right\nq:missing:EMPTY:EMPTY\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies headers and case-insensitive comparison.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsHeadersAndIgnoreCase() {
		using var first = new TemporaryFile( "id left\nAlpha 1\n"u8.ToArray() );
		using var second = new TemporaryFile( "key right\nalpha x\n"u8.ToArray() );
		var result = await RunAsync( [ "--header", "--ignore-case", first.Path, second.Path ] );
		Assert.Equal( "id left right\nAlpha 1 x\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies NUL-delimited records with an exact separator.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsNullRecords() {
		using var first = new TemporaryFile( new byte[] { (byte)'a', (byte)':', (byte)'1', 0 } );
		using var second = new TemporaryFile( new byte[] { (byte)'a', (byte)':', (byte)'x', 0 } );
		var result = await RunAsync( [ "-z", "-t:", first.Path, second.Path ] );
		Assert.Equal( new byte[] { (byte)'a', (byte)':', (byte)'1', (byte)':', (byte)'x', 0 }, result.Output );
	}

	/// <summary>Verifies separated output fields, NUL field separators, and option validation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsExtendedFieldSyntaxAndValidatesSeparators() {
		using var first = new TemporaryFile( new byte[] { (byte)'a', 0, (byte)'1', (byte)'\n' } );
		using var second = new TemporaryFile( new byte[] { (byte)'a', 0, (byte)'x', (byte)'\n' } );
		var nul = await RunAsync( [ "-t", "\\0", "-o", "0", "1.2", "2.2", first.Path, second.Path ] );
		var invalidSeparator = await RunAsync( [ "-t", "xy", first.Path, second.Path ] );
		var incompatibleFields = await RunAsync( [ "-j", "1", "-1", "2", first.Path, second.Path ] );
		Assert.Equal( new byte[] { (byte)'a', 0, (byte)'1', 0, (byte)'x', (byte)'\n' }, nul.Output );
		Assert.Equal( CommandExitCodes.Failure, invalidSeparator.Status );
		Assert.Contains( "multi-character", invalidSeparator.Error );
		Assert.Equal( CommandExitCodes.Failure, incompatibleFields.Status );
		Assert.Contains( "incompatible join fields", incompatibleFields.Error );
	}

	/// <summary>Verifies GNU's deferred default order check.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DefaultOrderCheckFailsOnlyAfterUnpairableData() {
		using var pairedFirst = new TemporaryFile( "b 1\na 2\n"u8.ToArray() );
		using var pairedSecond = new TemporaryFile( "b x\na y\n"u8.ToArray() );
		using var unpairedSecond = new TemporaryFile( "c x\n"u8.ToArray() );
		var paired = await RunAsync( [ pairedFirst.Path, pairedSecond.Path ] );
		var unpaired = await RunAsync( [ pairedFirst.Path, unpairedSecond.Path ] );
		Assert.Equal( CommandExitCodes.Success, paired.Status );
		Assert.Equal( CommandExitCodes.Failure, unpaired.Status );
		Assert.Contains( "not in sorted order", unpaired.Error );
	}

	/// <summary>Verifies order diagnostics and explicit order-check suppression.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ChecksOrIgnoresInputOrder() {
		using var first = new TemporaryFile( "b 1\na 2\n"u8.ToArray() );
		using var second = new TemporaryFile( "c x\n"u8.ToArray() );
		var checkedResult = await RunAsync( [ "--check-order", first.Path, second.Path ] );
		var uncheckedResult = await RunAsync( [ "--nocheck-order", first.Path, second.Path ] );
		Assert.Equal( CommandExitCodes.Failure, checkedResult.Status );
		Assert.Contains( "not in sorted order", checkedResult.Error );
		Assert.Equal( CommandExitCodes.Success, uncheckedResult.Status );
	}

	/// <summary>Verifies help, version, and standard-input operand validation.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ] );
		var version = await RunAsync( [ "--version" ] );
		var invalid = await RunAsync( [ "-", "-" ], "a 1\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: join", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "join (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, invalid.Status );
		Assert.Contains( "both files", invalid.Error );
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
			"join",
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
			this.Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-join-test-", Guid.NewGuid().ToString( "N" ) ) );
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
