namespace Icod.CoreUtils.Unexpand.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests documented <c>unexpand</c> option and blank-conversion behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies default conversion of an initial eight-space run.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InitialEightSpacesBecomeOneTab() {
		var result = await RunAsync( [ ], "        x"u8.ToArray() );
		Assert.Equal( "\tx"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that one blank immediately before a stop remains a blank.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SingleBlankBeforeAStopIsNotExpanded() {
		var result = await RunAsync( [ ], "1234567 x"u8.ToArray() );
		Assert.Equal( "1234567 x"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies default initial-only mode and explicit all-line mode.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InitialAndAllModesAreDistinct() {
		var initial = await RunAsync( [ ], "        x       y"u8.ToArray() );
		var all = await RunAsync( [ "--all" ], "        x       y"u8.ToArray() );
		Assert.Equal( "\tx       y"u8.ToArray(), initial.Output );
		Assert.Equal( "\tx\ty"u8.ToArray(), all.Output );
	}

	/// <summary>Verifies modern tab options, obsolete tab syntax, and first-only precedence.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task TabOptionPrecedenceMatchesGnu() {
		var modern = await RunAsync( [ "--tabs=4" ], "    x   y"u8.ToArray() );
		var obsolete = await RunAsync( [ "-4" ], "    x   y"u8.ToArray() );
		var firstBeforeAll = await RunAsync( [ "--first-only", "--all" ], "        x       y"u8.ToArray() );
		var firstAfterAll = await RunAsync( [ "--all", "--first-only" ], "        x       y"u8.ToArray() );
		Assert.Equal( "\tx\ty"u8.ToArray(), modern.Output );
		Assert.Equal( "\tx   y"u8.ToArray(), obsolete.Output );
		Assert.Equal( "\tx       y"u8.ToArray(), firstBeforeAll.Output );
		Assert.Equal( firstBeforeAll.Output, firstAfterAll.Output );
	}

	/// <summary>Verifies explicit, relative, absolute, and repeated tab-stop forms.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task TabStopFormsAreSupported() {
		var repeated = await RunAsync( [ "-t4", "-t10" ], "          x"u8.ToArray() );
		var relative = await RunAsync( [ "--tabs=4,8,+4" ], "            x"u8.ToArray() );
		var absolute = await RunAsync( [ "--tabs=4,10,/8" ], "                x"u8.ToArray() );
		Assert.Equal( "\t\tx"u8.ToArray(), repeated.Output );
		Assert.Equal( "\t\t\tx"u8.ToArray(), relative.Output );
		Assert.Equal( "\t\t\tx"u8.ToArray(), absolute.Output );
	}

	/// <summary>Verifies that exhausting a finite list disables later conversion for the line.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task FiniteListExhaustionDoesNotResumeAfterNonblankText() {
		var result = await RunAsync( [ "--all", "--tabs=4,8" ], "        x       y"u8.ToArray() );
		Assert.Equal( "\t\tx       y"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies preservation of an existing tab.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ExistingTabsArePreserved() {
		var result = await RunAsync( [ ], "\tx"u8.ToArray() );
		Assert.Equal( "\tx"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies the GNU pending-blank rule after a tab and backspace.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BackspaceCanMakeOneBlankBeforeAStopPending() {
		var result = await RunAsync( [ "--all" ], "\t\b  "u8.ToArray() );
		Assert.Equal( "\t\b\t "u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that nonbreaking spaces are not treated as ordinary locale blanks.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NonbreakingSpacesRemainUnchanged() {
		var input = Encoding.UTF8.GetBytes( new string( '\u00A0', 8 ) );
		var result = await RunAsync( [ "--all" ], input );
		Assert.Equal( input, result.Output );
	}

	/// <summary>Verifies that a malformed byte ends the default initial blank region without being normalized.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InvalidByteEndsInitialRegion() {
		var input = Enumerable.Repeat( (byte)' ', 8 )
			.Concat( new byte[] { 0xFF } )
			.Concat( Enumerable.Repeat( (byte)' ', 7 ) )
			.ToArray();
		var result = await RunAsync( [ ], input );
		var expected = new byte[] { (byte)'\t', 0xFF }
			.Concat( Enumerable.Repeat( (byte)' ', 7 ) )
			.ToArray();
		Assert.Equal( expected, result.Output );
	}

	/// <summary>Verifies conversion of a multibyte locale blank run at a tab stop.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task WideLocaleBlanksCanBecomeATab() {
		var result = await RunAsync( [ ], Encoding.UTF8.GetBytes( new string( '\u3000', 4 ) + "x" ) );
		Assert.Equal( "\tx"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies help, version, invalid-list, and cancellation paths.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CommandControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [ ] );
		var version = await RunAsync( [ "--version" ], [ ] );
		var invalid = await RunAsync( [ "--tabs=8,4" ], [ ] );
		var invalidLegacy = await RunAsync( [ "-4,+8" ], [ ] );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var canceled = await RunAsync( [ ], "x"u8.ToArray(), source.Token );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: unexpand", help.TextOutput );
		Assert.Contains( "GNU Coreutils 9.11", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, invalid.Status );
		Assert.Contains( "strictly increasing", invalid.Error );
		Assert.Equal( CommandExitCodes.Failure, invalidLegacy.Status );
		Assert.Contains( "invalid character", invalidLegacy.Error );
		Assert.Equal( CommandExitCodes.Canceled, canceled.Status );
	}

	/// <summary>Verifies that the synchronous wrapper uses the asynchronous engine.</summary>
	[Fact]
	public void SynchronousWrapperUsesTheSameEngine() {
		var output = new StringWriter();
		var status = Command.Run( [ ], new StringReader( "        x" ), output, new StringWriter() );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( "\tx", output.ToString() );
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
			var context = new CommandContext( "unexpand", new StringReader( string.Empty ), textOutput, error, inputStream, outputStream, null, cancellationToken );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
