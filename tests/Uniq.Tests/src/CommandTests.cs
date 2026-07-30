namespace Icod.CoreUtils.Uniq.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests adjacent-record filtering, grouping, comparison slices, and control paths.</summary>
public sealed class CommandTests {
	/// <summary>Verifies default filtering and count prefixes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FiltersAdjacentRecordsAndCountsGroups() {
		var input = "a\na\nb\nc\nc\nc\n"u8.ToArray();
		var plain = await RunAsync( [], input );
		var counted = await RunAsync( [ "-c" ], input );
		Assert.Equal( "a\nb\nc\n"u8.ToArray(), plain.Output );
		Assert.Equal( "      2 a\n      1 b\n      3 c\n"u8.ToArray(), counted.Output );
	}

	/// <summary>Verifies repeated-only and unique-only group selection.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SelectsRepeatedOrUniqueGroups() {
		var input = "a\na\nb\nc\nc\n"u8.ToArray();
		var repeated = await RunAsync( [ "-d" ], input );
		var unique = await RunAsync( [ "-u" ], input );
		Assert.Equal( "a\nc\n"u8.ToArray(), repeated.Output );
		Assert.Equal( "b\n"u8.ToArray(), unique.Output );
	}

	/// <summary>Verifies all-repeated and group delimiter methods without whole-input buffering.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsRepeatedAndAllGroupOutput() {
		var input = "a\na\nb\nc\nc\n"u8.ToArray();
		var repeated = await RunAsync( [ "--all-repeated=separate" ], input );
		var grouped = await RunAsync( [ "--group=separate" ], input );
		Assert.Equal( "a\na\n\nc\nc\n"u8.ToArray(), repeated.Output );
		Assert.Equal( "a\na\n\nb\n\nc\nc\n"u8.ToArray(), grouped.Output );
	}

	/// <summary>Verifies skip-field, skip-character, and comparison-width behavior.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsComparisonSlices() {
		var fields = await RunAsync( [ "-f", "1" ], "1 alpha\n2 alpha\n3 beta\n"u8.ToArray() );
		var chars = await RunAsync( [ "-s", "2", "-w", "1" ], "00apple\n11apricot\n22banana\n"u8.ToArray() );
		Assert.Equal( "1 alpha\n3 beta\n"u8.ToArray(), fields.Output );
		Assert.Equal( "00apple\n22banana\n"u8.ToArray(), chars.Output );
	}

	/// <summary>Verifies case folding and NUL-delimited records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsIgnoreCaseAndNullRecords() {
		var caseFolded = await RunAsync( [ "-i" ], "Alpha\nalpha\nBeta\n"u8.ToArray() );
		var nul = await RunAsync( [ "-z" ], new byte[] { (byte)'a', 0, (byte)'a', 0, (byte)'b' } );
		Assert.Equal( "Alpha\nBeta\n"u8.ToArray(), caseFolded.Output );
		Assert.Equal( new byte[] { (byte)'a', 0, (byte)'b', 0 }, nul.Output );
	}

	/// <summary>Verifies GNU option composition for duplicate-record output.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ComposesRepeatedAndUniqueSelectionFlags() {
		var input = "a\na\na\nb\nc\nc\n"u8.ToArray();
		var none = await RunAsync( [ "-d", "-u" ], input );
		var allButLast = await RunAsync( [ "-D", "-u" ], input );
		var repeatedMethods = await RunAsync(
			[ "--all-repeated=prepend", "--all-repeated=separate" ],
			input
		);
		Assert.Empty( none.Output );
		Assert.Equal( "a\na\nc\n"u8.ToArray(), allButLast.Output );
		Assert.Equal( "a\na\na\n\nc\nc\n"u8.ToArray(), repeatedMethods.Output );
	}

	/// <summary>Verifies repeated group options and single group boundaries.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task LastGroupMethodWinsWithoutDoubleBoundaries() {
		var input = "a\na\nb\nc\nc\n"u8.ToArray();
		var result = await RunAsync( [ "--group=prepend", "--group=both" ], input );
		Assert.Equal( "\na\na\n\nb\n\nc\nc\n\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies multibyte character counts for skip and width options.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CountsMultibyteCharactersRatherThanUtf8Bytes() {
		var originalLcAll = Environment.GetEnvironmentVariable( "LC_ALL" );
		try {
			Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
			var skipped = await RunAsync( [ "-s", "1" ], "éx\nêx\nz\n"u8.ToArray() );
			var width = await RunAsync( [ "-w", "1" ], "éx\néy\nz\n"u8.ToArray() );
			Assert.Equal( "éx\nz\n"u8.ToArray(), skipped.Output );
			Assert.Equal( "éx\nz\n"u8.ToArray(), width.Output );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", originalLcAll );
		}
	}

	/// <summary>Verifies safe replacement when INPUT and OUTPUT name the same file.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OutputMayReplaceInputSafely() {
		using var file = new TemporaryFile( "a\na\nb\n"u8.ToArray() );
		var result = await RunAsync( [ file.Path, file.Path ], [] );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal( "a\nb\n"u8.ToArray(), await File.ReadAllBytesAsync( file.Path ) );
	}

	/// <summary>Verifies option incompatibilities plus help and version paths.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [] );
		var version = await RunAsync( [ "--version" ], [] );
		var conflict = await RunAsync( [ "--group", "-c" ], [] );
		var countAll = await RunAsync( [ "-c", "-D" ], [] );
		var invalidGroup = await RunAsync( [ "--group=none" ], [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: uniq", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "uniq (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, conflict.Status );
		Assert.Contains( "incompatible", conflict.Error );
		Assert.Equal( CommandExitCodes.Failure, countAll.Status );
		Assert.Contains( "meaningless", countAll.Error );
		Assert.Equal( CommandExitCodes.Failure, invalidGroup.Status );
		Assert.Contains( "invalid delimiter method", invalidGroup.Error );
	}

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync(
		string[] args,
		byte[] input
	) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext(
			"uniq",
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
		private TemporaryFile( byte[] contents ) {
			this.Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-uniq-test-", Guid.NewGuid().ToString( "N" ) ) );
			File.WriteAllBytes( this.Path, contents );
		}

		private string Path { get; }

		void IDisposable.Dispose() {
			try {
				File.Delete( this.Path );
			} catch {
				// Test cleanup must not mask assertions.
			}
		}
	}
}
