namespace Icod.CoreUtils.NL.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests documented <c>nl</c> option and numbering behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies default nonempty body numbering and blank prefixes.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task DefaultsNumberNonemptyBodyLines() {
		var result = await RunAsync( [ ], "a\n\nb"u8.ToArray() );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal(
			Encoding.UTF8.GetBytes( string.Concat( "     1\ta\n       \n     2\tb", Environment.NewLine ) ),
			result.Output
		);
	}

	/// <summary>Verifies signed starts and increments together with zero padding.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SignedNumbersAndZeroPaddingAreSupported() {
		var result = await RunAsync(
			[ "-v-1", "-i-2", "-nrz", "-w6", "-s:" ],
			"a\nb\n"u8.ToArray()
		);
		Assert.Equal( "-00001:a\n-00003:b\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies left, right, and right-zero field formats.</summary>
	/// <param name="format">The format name.</param>
	/// <param name="expectedPrefix">The expected generated prefix.</param>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Theory]
	[InlineData( "ln", "1   :" )]
	[InlineData( "rn", "   1:" )]
	[InlineData( "rz", "0001:" )]
	public async Task NumberFormatsAreSupported( string format, string expectedPrefix ) {
		var result = await RunAsync( [ "-n", format, "-w4", "-s:" ], "x\n"u8.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( expectedPrefix, "x\n" ) ), result.Output );
	}

	/// <summary>Verifies numbering of all lines with grouped blank lines.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task AllStyleCanJoinBlankLines() {
		var result = await RunAsync( [ "--body-numbering=a", "--join-blank-lines=2" ], "a\n\n\n\nb\n"u8.ToArray() );
		Assert.Equal( "     1\ta\n       \n     2\t\n       \n     3\tb\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies GNU basic regular-expression numbering.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task PatternStyleUsesSharedGnuBreEngine() {
		var result = await RunAsync( [ "--body-numbering=p^x" ], "x\ny\nxx\n"u8.ToArray() );
		Assert.Equal( "     1\tx\n       y\n     2\txx\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies help, version, invalid styles, regular expressions, and cancellation.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ControlPathsHaveConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], [ ] );
		var version = await RunAsync( [ "--version" ], [ ] );
		var badStyle = await RunAsync( [ "-bx" ], [ ] );
		var badPattern = await RunAsync( [ "-bp[" ], [ ] );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var canceled = await RunAsync( [ ], "x"u8.ToArray(), source.Token );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: nl", help.TextOutput );
		Assert.Contains( "GNU Coreutils 9.11", version.TextOutput );
		Assert.Equal( CommandExitCodes.Failure, badStyle.Status );
		Assert.Contains( "invalid line numbering style", badStyle.Error );
		Assert.Equal( CommandExitCodes.Failure, badPattern.Status );
		Assert.Contains( "invalid regular expression", badPattern.Error );
		Assert.Equal( CommandExitCodes.Canceled, canceled.Status );
	}

	/// <summary>Verifies the synchronous compatibility wrapper.</summary>
	[Fact]
	public void SynchronousWrapperUsesTheAsynchronousEngine() {
		var output = new StringWriter();
		var status = Command.Run( [ "-w1", "-s:" ], new StringReader( "x\n" ), output, new StringWriter() );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( "1:x\n", output.ToString() );
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
			var context = new CommandContext( "nl", new StringReader( string.Empty ), textOutput, error, inputStream, outputStream, null, cancellationToken );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), textOutput.ToString(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
