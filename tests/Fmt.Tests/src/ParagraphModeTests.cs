namespace Icod.CoreUtils.Fmt.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests prefix, crown, tagged, goal, and sentence behavior.</summary>
public sealed class ParagraphModeTests {
	/// <summary>Verifies crown-margin recognition and secondary indentation.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task CrownMarginUsesSecondLineIndentation() {
		var result = await RunAsync(
			[ "--crown-margin", "--width=15" ],
			"Head words here\n  body words here\n  more body words\n"u8.ToArray()
		);
		Assert.Equal( Generated( "Head words here", "  body words", "  here more", "  body words" ), result.Output );
	}

	/// <summary>Verifies tagged-paragraph recognition.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task TaggedParagraphRequiresDifferentFirstAndSecondIndents() {
		var result = await RunAsync(
			[ "--tagged-paragraph", "--width=15" ],
			"tag words here\n  body words here\n  more body words\n"u8.ToArray()
		);
		Assert.Equal( Generated( "tag words here", "  body words", "  here more", "  body words" ), result.Output );
	}

	/// <summary>Verifies prefix matching, joining, and exact copying of nonmatching lines.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task PrefixLimitsParagraphFormatting() {
		var result = await RunAsync(
			[ "--prefix=>" ],
			">one two\n>three four\nnot prefix\n"u8.ToArray()
		);
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat( ">one two three four", Environment.NewLine, "not prefix", Environment.NewLine )
			),
			result.Output
		);
	}

	/// <summary>Verifies that prefix-only lines are copied unchanged.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task PrefixOnlyLinesAreNotReformatted() {
		var result = await RunAsync( [ "-p", ">" ], ">\n>x\n"u8.ToArray() );
		Assert.Equal( Generated( ">", ">x" ), result.Output );
	}

	/// <summary>Verifies that a goal without an explicit width derives a ten-column maximum.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task GoalDerivesMaximumWhenWidthIsAbsent() {
		var result = await RunAsync( [ "--goal=0" ], "a b c d e f\n"u8.ToArray() );
		Assert.Equal( Generated( "a", "b c d e f" ), result.Output );
	}

	/// <summary>Verifies sentence spacing after punctuation followed by two source spaces.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SentenceSpacingIsRetainedAcrossReflow() {
		var result = await RunAsync( [ ], "One.  Two three.\nFour.\n"u8.ToArray() );
		Assert.Equal( Generated( "One.  Two three.  Four." ), result.Output );
	}

	/// <summary>Verifies byte-counted UTF-8 prefixes and word widths.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task MultibyteTextUsesSourceByteWidths() {
		var prefix = await RunAsync( [ "--prefix=ç" ], Encoding.UTF8.GetBytes( "ça\nçb\n" ) );
		var wideWords = await RunAsync( [ "--width=6" ], Encoding.UTF8.GetBytes( "界 界 界\n" ) );
		Assert.Equal( Encoding.UTF8.GetBytes( string.Concat( "ça b", Environment.NewLine ) ), prefix.Output );
		Assert.Equal( Generated( "界", "界", "界" ), wideWords.Output );
	}

	/// <summary>Verifies that trailing spaces in a prefix impose a minimum post-prefix indentation.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task TrailingPrefixSpacesAreRequiredButNotReattached() {
		var result = await RunAsync( [ "--prefix=> " ], ">x\n> y\n>"u8.ToArray() );
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat( ">x", Environment.NewLine, "> y", Environment.NewLine, ">", Environment.NewLine )
			),
			result.Output
		);
	}

	/// <summary>Verifies GNU's normalized prefix-copy path and precise tab discovery.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task PrefixCopyNormalizesOnlyScannedIndentation() {
		var scannedTab = await RunAsync( [ "--prefix=abc" ], " \tabX\n \tabc   \n"u8.ToArray() );
		var unscannedTab = await RunAsync( [ "--prefix=abc" ], "        x\ty\n"u8.ToArray() );
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat( "\tabX", Environment.NewLine, "\tabc", Environment.NewLine )
			),
			scannedTab.Output
		);
		Assert.Equal(
			Encoding.UTF8.GetBytes( string.Concat( "        x\ty", Environment.NewLine ) ),
			unscannedTab.Output
		);
	}

	private static byte[] Generated( params string[] lines ) {
		return Encoding.UTF8.GetBytes( string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine ) );
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "fmt", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
