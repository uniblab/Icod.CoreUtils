namespace Icod.CoreUtils.Expand.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests documented <c>expand</c> option and display-column behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies the default eight-column tab interval.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task DefaultStopsOccurEveryEightColumns() {
		var result = await RunAsync( [ ], "a\tb\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "a       b\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that initial mode stops after the first nonblank unit.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InitialModeStopsAfterFirstNonblank() {
		var result = await RunAsync( [ "--initial" ], " \ta\tb"u8.ToArray() );
		Assert.Equal( "        a\tb"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies the modern and obsolete single-interval spellings.</summary>
	/// <param name="option">The option spelling under test.</param>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Theory]
	[InlineData( "-t4" )]
	[InlineData( "--tabs=4" )]
	[InlineData( "-4" )]
	public async Task ModernAndObsoleteTabWidthsAreSupported( string option ) {
		var result = await RunAsync( [ option ], "x\ty"u8.ToArray() );
		Assert.Equal( "x   y"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies explicit stops, repeated options, and relative continuation.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ExplicitRepeatedAndRelativeStopsAreSupported() {
		var repeated = await RunAsync( [ "-t4", "-t10" ], "\t\t\t"u8.ToArray() );
		var relative = await RunAsync( [ "--tabs=4,10,+4" ], "1234567890\tX"u8.ToArray() );
		Assert.Equal( new string( ' ', 11 ), Encoding.UTF8.GetString( repeated.Output ) );
		Assert.Equal( "1234567890    X"u8.ToArray(), relative.Output );
	}

	/// <summary>Verifies globally aligned continuation and finite-list exhaustion.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task AbsoluteContinuationAndFiniteExhaustionAreSupported() {
		var absolute = await RunAsync( [ "--tabs=4,10,/8" ], "1234567890\tX"u8.ToArray() );
		var exhausted = await RunAsync( [ "--tabs=4,8" ], "12345678\tX"u8.ToArray() );
		Assert.Equal( "1234567890      X"u8.ToArray(), absolute.Output );
		Assert.Equal( "12345678 X"u8.ToArray(), exhausted.Output );
	}

	/// <summary>Verifies that backspace moves one display column left.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BackspaceMovesOneColumnLeft() {
		var result = await RunAsync( [ ], "1234\b\t"u8.ToArray() );
		Assert.Equal( "1234\b     "u8.ToArray(), result.Output );
	}

	/// <summary>Verifies wide and zero-column scalar measurement.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task UnicodeScalarsUseDisplayWidths() {
		var wide = await RunAsync( [ ], Encoding.UTF8.GetBytes( "界\t" ) );
		var combining = await RunAsync( [ ], Encoding.UTF8.GetBytes( "\u0301\t" ) );
		Assert.Equal( Encoding.UTF8.GetBytes( "界      " ), wide.Output );
		Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( "\u0301", new string( ' ', 8 ) ) ), combining.Output );
	}

	/// <summary>Verifies that a malformed UTF-8 byte is preserved and occupies one display column.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InvalidByteCountsAsOneColumn() {
		var result = await RunAsync( [ ], new byte[] { 0xFF, (byte)'\t' } );
		Assert.Equal( new byte[] { 0xFF }.Concat( Enumerable.Repeat( (byte)' ', 7 ) ).ToArray(), result.Output );
	}

	/// <summary>Verifies help, version, tab diagnostics, cancellation, and option termination.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CommandControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [ ] );
		var version = await RunAsync( [ "--version" ], [ ] );
		var invalidOption = await RunAsync( [ "--not-an-option" ], [ ] );
		var invalidTabs = await RunAsync( [ "--tabs=8,4" ], [ ] );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var canceled = await RunAsync( [ ], "x"u8.ToArray(), source.Token );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: expand", help.TextOutput );
		Assert.Contains( "GNU Coreutils 9.11", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, invalidOption.Status );
		Assert.Contains( "unrecognized option", invalidOption.Error );
		Assert.Equal( CommandExitCodes.Failure, invalidTabs.Status );
		Assert.Contains( "strictly increasing", invalidTabs.Error );
		Assert.Equal( CommandExitCodes.Canceled, canceled.Status );
	}

	/// <summary>Verifies that the synchronous wrapper uses the asynchronous engine.</summary>
	[Fact]
	public void SynchronousWrapperUsesTheSameEngine() {
		var output = new StringWriter();
		var status = Command.Run( [ "-t4" ], new StringReader( "x\ty" ), output, new StringWriter() );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( "x   y", output.ToString() );
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
			var context = new CommandContext( "expand", new StringReader( string.Empty ), textOutput, error, inputStream, outputStream, null, cancellationToken );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
