namespace Icod.CoreUtils.NL.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests logical-page sections, delimiters, and style selection.</summary>
public sealed class SectionAndStylesTests {
	/// <summary>Verifies default logical-page delimiter recognition and section resets.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task DefaultDelimitersSelectHeaderBodyAndFooter() {
		var input = "\\:\\:\\:\nH\n\\:\\:\nB\n\\:\nF\n"u8.ToArray();
		var result = await RunAsync( [ "-ha", "-ba", "-fa" ], input );
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat(
					Environment.NewLine, "     1\tH\n",
					Environment.NewLine, "     1\tB\n",
					Environment.NewLine, "     1\tF\n"
				)
			),
			result.Output
		);
	}

	/// <summary>Verifies that no-renumber preserves numbering across sections.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NoRenumberContinuesAcrossSections() {
		var input = "\\:\\:\\:\nH\n\\:\\:\nB\n"u8.ToArray();
		var result = await RunAsync( [ "-p", "-ha", "-ba" ], input );
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat( Environment.NewLine, "     1\tH\n", Environment.NewLine, "     2\tB\n" )
			),
			result.Output
		);
	}

	/// <summary>Verifies one-scalar multibyte delimiter completion with a colon.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task OneScalarDelimiterReceivesColonSecondCharacter() {
		var marker = "ø:";
		var input = Encoding.UTF8.GetBytes( string.Concat( marker, marker, "\nbody\n" ) );
		var result = await RunAsync( [ "--section-delimiter=ø", "--body-numbering=a" ], input );
		Assert.Equal(
			Encoding.UTF8.GetBytes( string.Concat( Environment.NewLine, "     1\tbody\n" ) ),
			result.Output
		);
	}

	/// <summary>Verifies the GNU extension accepting delimiter strings longer than two characters.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task LongDelimiterValueIsRepeatedAsAWhole() {
		var result = await RunAsync( [ "-dabc", "-ba" ], "abcabc\nx\n"u8.ToArray() );
		Assert.Equal(
			Encoding.UTF8.GetBytes( string.Concat( Environment.NewLine, "     1\tx\n" ) ),
			result.Output
		);
	}

	/// <summary>Verifies that an empty delimiter disables section recognition.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task EmptyDelimiterDisablesSectionRecognition() {
		var result = await RunAsync( [ "--section-delimiter=", "-ba" ], "\\:\\:\n"u8.ToArray() );
		Assert.Equal( "     1\t\\:\\:\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies independent header, body, and footer style selection.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SectionsUseIndependentStyles() {
		var input = "\\:\\:\\:\nH\n\\:\\:\nB\n\\:\nF\n"u8.ToArray();
		var result = await RunAsync( [ "-hn", "-ba", "-fn" ], input );
		var blankPrefix = "       ";
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				string.Concat(
					Environment.NewLine, blankPrefix, "H\n",
					Environment.NewLine, "     1\tB\n",
					Environment.NewLine, blankPrefix, "F\n"
				)
			),
			result.Output
		);
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		var original = Environment.GetEnvironmentVariable( "LC_ALL" );
		Environment.SetEnvironmentVariable( "LC_ALL", "C.UTF-8" );
		try {
			using var inputStream = new MemoryStream( input, writable: false );
			using var outputStream = new MemoryStream();
			var error = new StringWriter();
			var context = new CommandContext( "nl", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
			var status = await Command.RunAsync( args, context );
			return ( status, outputStream.ToArray(), error.ToString() );
		} finally {
			Environment.SetEnvironmentVariable( "LC_ALL", original );
		}
	}
}
