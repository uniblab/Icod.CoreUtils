namespace Icod.CoreUtils.Fmt.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests documented <c>fmt</c> command behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies that ordinary equal-indentation lines form one paragraph.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task DefaultModeRefillsParagraphs() {
		var result = await RunAsync( [ ], "one two\nthree four\n"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( Generated( "one two three four" ), result.Output );
	}

	/// <summary>Verifies GNU's optimized narrow-width line breaks.</summary>
	/// <param name="width">The selected maximum width.</param>
	/// <param name="expected">The expected generated lines separated by vertical bars.</param>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Theory]
	[InlineData( "8", "aa bb cc|dd ee" )]
	[InlineData( "7", "aa|bb cc|dd ee" )]
	public async Task WidthUsesParagraphOptimization( string width, string expected ) {
		var result = await RunAsync( [ "--width", width ], "aa bb cc dd ee\n"u8.ToArray() );
		Assert.Equal( Generated( expected.Split( '|' ) ), result.Output );
	}

	/// <summary>Verifies that split-only preserves input-line boundaries while retaining GNU paragraph optimization within each line.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SplitOnlyOptimizesEachInputLineIndependently() {
		var result = await RunAsync( [ "--split-only", "--width=5" ], "aa bb cc\ndd ee\n"u8.ToArray() );

		// The first input line remains a separate paragraph, but GNU's optimizer
		// chooses "aa" and "bb cc" rather than greedily filling "aa bb".
		Assert.Equal( Generated( "aa", "bb cc", "dd ee" ), result.Output );
	}

	/// <summary>Verifies retained versus uniform spacing.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task UniformSpacingNormalizesInteriorSpace() {
		var normal = await RunAsync( [ ], "a   b. c\n"u8.ToArray() );
		var uniform = await RunAsync( [ "--uniform-spacing" ], "a   b. c\n"u8.ToArray() );
		Assert.Equal( Generated( "a   b. c" ), normal.Output );
		Assert.Equal( Generated( "a b. c" ), uniform.Output );
	}

	/// <summary>Verifies modern and obsolete width spellings.</summary>
	/// <param name="option">The width option spelling.</param>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Theory]
	[InlineData( "-8" )]
	[InlineData( "-w8" )]
	[InlineData( "--width=8" )]
	public async Task WidthSpellingsAreSupported( string option ) {
		var result = await RunAsync( [ option ], "aa bb cc dd ee\n"u8.ToArray() );
		Assert.Equal( Generated( "aa bb cc", "dd ee" ), result.Output );
	}

	/// <summary>Verifies help, version, invalid values, and cancellation.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [ ] );
		var version = await RunAsync( [ "--version" ], [ ] );
		var badWidth = await RunAsync( [ "--width=2501" ], [ ] );
		var badGoal = await RunAsync( [ "--width=10", "--goal=11" ], [ ] );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var canceled = await RunAsync( [ ], "x"u8.ToArray(), source.Token );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: fmt", help.TextOutput );
		Assert.Contains( "GNU Coreutils 9.11", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, badWidth.Status );
		Assert.Contains( "invalid width", badWidth.Error );
		Assert.Equal( CommandExitCodes.Failure, badGoal.Status );
		Assert.Contains( "invalid width", badGoal.Error );
		Assert.Equal( CommandExitCodes.Canceled, canceled.Status );
	}

	/// <summary>Verifies that the synchronous compatibility wrapper uses the same engine.</summary>
	[Fact]
	public void SynchronousWrapperUsesTheAsynchronousEngine() {
		var output = new StringWriter();
		var status = Command.Run( [ "-w8" ], new StringReader( "aa bb cc dd ee\n" ), output, new StringWriter() );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( Encoding.UTF8.GetString( Generated( "aa bb cc", "dd ee" ) ), output.ToString() );
	}

	/// <summary>Verifies GNU's width parser and first-argument-only obsolete syntax.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task WidthParsingMatchesGnuRules() {
		var plus = await RunAsync( [ "--width=+1" ], "a b\n"u8.ToArray() );
		var leadingBlank = await RunAsync( [ "--width", "\t1" ], "a b\n"u8.ToArray() );
		var badSuffix = await RunAsync( [ "-72x" ], [ ] );
		var misplaced = await RunAsync( [ "-c", "-72" ], [ ] );
		var excessiveGoal = await RunAsync( [ "--goal=76" ], [ ] );
		Assert.Equal( CommandExitCodes.Success, plus.Status );
		Assert.Equal( CommandExitCodes.Success, leadingBlank.Status );
		Assert.Equal( CommandExitCodes.Failure, badSuffix.Status );
		Assert.Contains( "invalid width", badSuffix.Error );
		Assert.Contains( "72x", badSuffix.Error );
		Assert.Equal( CommandExitCodes.Failure, misplaced.Status );
		Assert.Contains( "recognized only when it is the first", misplaced.Error );
		Assert.Equal( CommandExitCodes.Failure, excessiveGoal.Status );
		Assert.Contains( "invalid width", excessiveGoal.Error );
	}

	private static byte[] Generated( params string[] lines ) {
		return Encoding.UTF8.GetBytes( string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine ) );
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
			var context = new CommandContext( "fmt", new StringReader( string.Empty ), textOutput, error, inputStream, outputStream, null, cancellationToken );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
