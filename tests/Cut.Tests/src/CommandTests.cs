namespace Icod.CoreUtils.Cut.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests byte, character, range, and control-path behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordinary and complemented byte lists.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SelectsAndComplementsByteRanges() {
		var selected = await RunAsync( [ "-b", "1,3-4" ], "abcdef\n"u8.ToArray() );
		var complement = await RunAsync( [ "-b", "2-4", "--complement" ], "abcdef\n"u8.ToArray() );
		Assert.Equal( "acd\n"u8.ToArray(), selected.Output );
		Assert.Equal( "aef\n"u8.ToArray(), complement.Output );
	}

	/// <summary>Verifies that adjacent requested ranges retain an observable output boundary.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OutputDelimiterObservesAdjacentRanges() {
		var result = await RunAsync( [ "-b", "1-2,3-4", "-O", ":" ], "abcdef\n"u8.ToArray() );
		Assert.Equal( "ab:cd\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies decoded-character positions and exact UTF-8 bytes.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SelectsMultibyteCharacters() {
		var result = await RunAsync( [ "-c", "2" ], Encoding.UTF8.GetBytes( "é界x\n" ) );
		Assert.Equal( Encoding.UTF8.GetBytes( "界\n" ), result.Output );
	}

	/// <summary>Verifies GNU's suffix-based no-partial-byte rule.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task NoPartialOutputsOnlySelectedCharacterSuffixes() {
		var trailing = await RunAsync( [ "-b", "2", "-n" ], Encoding.UTF8.GetBytes( "é\n" ) );
		var leading = await RunAsync( [ "-b", "1", "-n" ], Encoding.UTF8.GetBytes( "é\n" ) );
		Assert.Equal( Encoding.UTF8.GetBytes( "é\n" ), trailing.Output );
		Assert.Equal( "\n"u8.ToArray(), leading.Output );
	}

	/// <summary>Verifies generated termination for an unterminated final record.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TerminatesUnterminatedSelectedRecords() {
		var result = await RunAsync( [ "-b", "1-" ], "abc"u8.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( "abc", Environment.NewLine ) ), result.Output );
	}

	/// <summary>Verifies NUL-delimited records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsNullTerminatedRecords() {
		var result = await RunAsync( [ "-z", "-b", "2" ], [ (byte)'a', (byte)'b', 0, (byte)'c', (byte)'d', 0 ] );
		Assert.Equal( new byte[] { (byte)'b', 0, (byte)'d', 0 }, result.Output );
	}

	/// <summary>Verifies help, version, and semantic-error statuses.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [] );
		var version = await RunAsync( [ "--version" ], [] );
		var missing = await RunAsync( [], [] );
		var duplicate = await RunAsync( [ "-b", "1", "-c", "1" ], [] );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: cut", help.TextOutput );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "cut (Icod.CoreUtils)", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, missing.Status );
		Assert.Contains( "must specify", missing.Error );
		Assert.Equal( CommandExitCodes.Failure, duplicate.Status );
		Assert.Contains( "only one list", duplicate.Error );
	}


	/// <summary>Verifies that short <c>-w</c> remains a no-argument option inside a GNU-style option cluster.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ShortWhitespaceOptionDoesNotConsumeFollowingCluster() {
		var result = await RunAsync( [ "-wf2" ], "a b c\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "b\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that the <c>trimmed</c> value belongs only to the long whitespace option.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ShortWhitespaceSpellingDoesNotAcceptTrimmedValue() {
		var result = await RunAsync( [ "-wtrimmed", "-f", "1" ], "a b\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "Try 'cut --help'", result.Error );
	}

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var textOutput = new StringWriter();
		var error = new StringWriter();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), textOutput, error, inputStream, outputStream );
		var status = await Command.RunAsync( args, context );
		return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
	}
}
