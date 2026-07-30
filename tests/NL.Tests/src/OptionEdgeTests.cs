namespace Icod.CoreUtils.NL.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests numeric grammar, option precedence, locale-sensitive delimiters, and persistent blank grouping.</summary>
public sealed class OptionEdgeTests {
	/// <summary>Verifies GNU numeric operands accept leading blanks and a plus sign.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NumericOperandsAcceptLeadingWhiteSpaceAndPlus() {
		var result = await RunAsync(
			[ "-ba", "-v", " +2", "-i", " +3", "-l", " +2", "-w", " +2", "-s:" ],
			"x\n\n\n"u8.ToArray()
		);
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( " 2:x\n   \n 5:\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies GNU numeric operands reject trailing white space.</summary>
	/// <param name="option">The numeric option.</param>
	/// <param name="value">The invalid trailing-white-space value.</param>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Theory]
	[InlineData( "-v", "2 " )]
	[InlineData( "-i", "2 " )]
	[InlineData( "-l", "2 " )]
	[InlineData( "-w", "2 " )]
	public async Task NumericOperandsRejectTrailingWhiteSpace( string option, string value ) {
		var result = await RunAsync( [ option, value ], [ ] );
		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Contains( "invalid", result.Error );
	}

	/// <summary>Verifies GNU's quiet clamping rules for the blank-line join count.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BlankJoinZeroAndOverflowAreClamped() {
		var zero = await RunAsync( [ "-ba", "-l0", "-w1", "-s:" ], "\n\n"u8.ToArray() );
		var overflow = await RunAsync(
			[ "-ba", "-l999999999999999999999999999999999999", "-w1", "-s:" ],
			"\n"u8.ToArray()
		);
		Assert.Equal( CommandExitCodes.Success, zero.Status );
		Assert.Equal( "1:\n2:\n"u8.ToArray(), zero.Output );
		Assert.Equal( CommandExitCodes.Success, overflow.Status );
		Assert.Equal( "  \n"u8.ToArray(), overflow.Output );
	}

	/// <summary>Verifies that later style and number-format options replace earlier values.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task LaterOptionsTakePrecedence() {
		var result = await RunAsync(
			[ "-ba", "-bt", "-nrz", "-nln", "-w3", "-s:" ],
			"\nx\n"u8.ToArray()
		);
		Assert.Equal( "    \n1  :x\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies GNU's acceptance of suffix text after simple style letters.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SimpleStyleLettersIgnoreSuffixText() {
		var result = await RunAsync( [ "-baignored", "-w1", "-s:" ], "\n"u8.ToArray() );
		Assert.Equal( "1:\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that an empty GNU basic regular expression matches every line.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task EmptyPatternNumbersEveryLine() {
		var result = await RunAsync( [ "-bp", "-w1", "-s:" ], "\nx\n"u8.ToArray() );
		Assert.Equal( "1:\n2:x\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that blank grouping persists across a logical-page delimiter while numbering resets.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task BlankGroupingPersistsAcrossSectionDelimiters() {
		var result = await RunAsync(
			[ "-ba", "-l2", "-w1", "-s:" ],
			"x\n\n\\:\\:\n\ny\n"u8.ToArray()
		);
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat( "1:x\n  \n", Environment.NewLine, "1:\n2:y\n" )
			),
			result.Output
		);
	}

	/// <summary>Verifies that one-character delimiter completion follows the active locale profile.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task DelimiterCharacterCountFollowsLocaleProfile() {
		var input = Encoding.UTF8.GetBytes( "øø\nx\n" );
		var utf8 = await RunAsync( [ "-dø", "-ba", "-w1", "-s:" ], input, "C.UTF-8" );
		var bytes = await RunAsync( [ "-dø", "-ba", "-w1", "-s:" ], input, "C" );
		Assert.Equal( Encoding.UTF8.GetBytes( "1:øø\n2:x\n" ), utf8.Output );
		Assert.Equal(
			Encoding.UTF8.GetBytes( string.Concat( Environment.NewLine, "1:x\n" ) ),
			bytes.Output
		);
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync(
		string[] args,
		byte[] input,
		string locale = "C.UTF-8"
	) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", locale );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext(
				"nl",
				new StringReader( string.Empty ),
				new StringWriter(),
				error,
				inputStream,
				outputStream
			);
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
