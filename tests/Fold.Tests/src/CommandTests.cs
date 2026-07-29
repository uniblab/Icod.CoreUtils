namespace Icod.CoreUtils.Fold.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests documented <c>fold</c> option, counting, and control-character behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies the default width and both modern and obsolete width syntax.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task WidthFormsAndDefaultAreSupported() {
		var eighty = Encoding.UTF8.GetBytes( new string( 'x', 80 ) );
		var defaultResult = await RunAsync( [ ], eighty );
		var modern = await RunAsync( [ "--width=4" ], "abcdef\n"u8.ToArray() );
		var obsolete = await RunAsync( [ "-4" ], "abcdef"u8.ToArray() );
		Assert.Equal( eighty, defaultResult.Output );
		Assert.Equal( Combine( "abcd"u8.ToArray(), Newline, "ef\n"u8.ToArray() ), modern.Output );
		Assert.Equal( Combine( "abcd"u8.ToArray(), Newline, "ef"u8.ToArray() ), obsolete.Output );
	}

	/// <summary>Verifies that byte, character, and display-column modes remain distinct.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ByteCharacterAndDisplayModesAreDistinct() {
		var input = Encoding.UTF8.GetBytes( "界界界" );
		var display = await RunAsync( [ "--width=4" ], input );
		var characters = await RunAsync( [ "--characters", "--width=4" ], input );
		var bytes = await RunAsync( [ "--bytes", "--width=4" ], input );
		Assert.Equal( Combine( Encoding.UTF8.GetBytes( "界界" ), Newline, Encoding.UTF8.GetBytes( "界" ) ), display.Output );
		Assert.Equal( input, characters.Output );
		Assert.Equal(
			Combine(
				Encoding.UTF8.GetBytes( "界" ),
				Newline,
				Encoding.UTF8.GetBytes( "界" ),
				Newline,
				Encoding.UTF8.GetBytes( "界" )
			),
			bytes.Output
		);
	}

	/// <summary>Verifies that the final counting-mode option controls the invocation.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task LastCountingOptionWins() {
		var input = Encoding.UTF8.GetBytes( "ééé" );
		var characters = await RunAsync( [ "--bytes", "--characters", "--width=4" ], input );
		var bytes = await RunAsync( [ "--characters", "--bytes", "--width=4" ], input );
		Assert.Equal( input, characters.Output );
		Assert.Equal( Combine( Encoding.UTF8.GetBytes( "éé" ), Newline, Encoding.UTF8.GetBytes( "é" ) ), bytes.Output );
	}

	/// <summary>Verifies that spaces mode breaks after the final eligible locale blank.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SpacesModeUsesTheLastBlankBoundary() {
		var result = await RunAsync( [ "--spaces", "--width=7" ], "one two three"u8.ToArray() );
		Assert.Equal(
			Combine( "one "u8.ToArray(), Newline, "two "u8.ToArray(), Newline, "three"u8.ToArray() ),
			result.Output
		);
	}

	/// <summary>Verifies that a word longer than the limit is folded without discarding source bytes.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SpacesModeStillFoldsLongWords() {
		var result = await RunAsync( [ "--spaces", "--width=4" ], "abcdef gh"u8.ToArray() );
		Assert.Equal( Combine( "abcd"u8.ToArray(), Newline, "ef "u8.ToArray(), Newline, "gh"u8.ToArray() ), result.Output );
	}

	/// <summary>Verifies tab, carriage-return, backspace, and zero-width column movement.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ControlAndZeroWidthUnitsAdjustColumns() {
		var tab = await RunAsync( [ "--width=8" ], "\tX"u8.ToArray() );
		var carriage = await RunAsync( [ "--width=3" ], "abc\rde"u8.ToArray() );
		var backspace = await RunAsync( [ "--width=3" ], "ab\bcd"u8.ToArray() );
		var combining = await RunAsync( [ "--width=2" ], Encoding.UTF8.GetBytes( "a\u0301b" ) );
		Assert.Equal( Combine( "\t"u8.ToArray(), Newline, "X"u8.ToArray() ), tab.Output );
		Assert.Equal( "abc\rde"u8.ToArray(), carriage.Output );
		Assert.Equal( "ab\bcd"u8.ToArray(), backspace.Output );
		Assert.Equal( Encoding.UTF8.GetBytes( "a\u0301b" ), combining.Output );
	}

	/// <summary>Verifies help, version, malformed width, overflow, and cancellation paths.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CommandControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [ ] );
		var version = await RunAsync( [ "--version" ], [ ] );
		var zero = await RunAsync( [ "--width=0" ], [ ] );
		var signed = await RunAsync( [ "--width=+4" ], [ ] );
		var spaced = await RunAsync( [ "--width= 4" ], [ ] );
		var overflow = await RunAsync( [ string.Concat( "--width=", ulong.MaxValue ) ], [ ] );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var canceled = await RunAsync( [ ], "x"u8.ToArray(), source.Token );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: fold", help.TextOutput );
		Assert.Contains( "GNU Coreutils 9.11", version.TextOutput );
		Assert.All( new[] { zero, signed, spaced, overflow }, result => Assert.Equal( CommandExitCodes.Failure, result.Status ) );
		Assert.Contains( "invalid number of columns", zero.Error );
		Assert.Equal( CommandExitCodes.Canceled, canceled.Status );
	}

	/// <summary>Verifies that the synchronous wrapper delegates to the asynchronous engine.</summary>
	[Fact]
	public void SynchronousWrapperUsesTheSameEngine() {
		var output = new StringWriter();
		var status = Command.Run( [ "-4" ], new StringReader( "abcdef" ), output, new StringWriter() );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( string.Concat( "abcd", Environment.NewLine, "ef" ), output.ToString() );
	}

	private static byte[] Newline => Encoding.UTF8.GetBytes( Environment.NewLine );

	private static byte[] Combine( params byte[][] values ) {
		return values.SelectMany( value => value ).ToArray();
	}

	private static async Task<(int Status, byte[] Output, string TextOutput, string Error)> RunAsync(
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default
	) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var textOutput = new StringWriter();
			var error = new StringWriter();
			var context = new CommandContext(
				"fold",
				new StringReader( string.Empty ),
				textOutput,
				error,
				inputStream,
				outputStream,
				null,
				cancellationToken
			);
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
