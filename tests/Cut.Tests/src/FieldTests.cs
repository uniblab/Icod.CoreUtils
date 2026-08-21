namespace Icod.CoreUtils.Cut.Tests;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Tests explicit and whitespace-delimited field selection.</summary>
public sealed class FieldTests {
	/// <summary>Verifies ordinary fields and passthrough of undelimited records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SelectsFieldsAndPassesUndelimitedRecords() {
		var result = await RunAsync( [ "-d", ":", "-f", "2" ], "a:b:c\nplain\n"u8.ToArray() );
		Assert.Equal( "b\nplain\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies suppression of undelimited records.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task OnlyDelimitedSuppressesPlainRecords() {
		var result = await RunAsync( [ "-s", "-d", ":", "-f", "2" ], "a:b:c\nplain\n"u8.ToArray() );
		Assert.Equal( "b\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies empty fields and a custom field output delimiter.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RetainsSelectedEmptyFields() {
		var result = await RunAsync( [ "-d", ":", "-f", "1,3", "-O", "|" ], ":b:\n"u8.ToArray() );
		Assert.Equal( "|\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies locale-blank runs and trimmed leading and trailing blanks.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsWhitespaceDelimitedFields() {
		var ordinary = await RunAsync( [ "-w", "-f", "2" ], "a   b c\n"u8.ToArray() );
		var trimmed = await RunAsync( [ "--whitespace-delimited=trimmed", "-f", "1" ], "  a  b  \n"u8.ToArray() );
		Assert.Equal( "b\n"u8.ToArray(), ordinary.Output );
		Assert.Equal( "a\n"u8.ToArray(), trimmed.Output );
	}

	/// <summary>Verifies the <c>-F</c> shorthand and its space output delimiter.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FieldWhitespaceShorthandUsesSpaceOutput() {
		var result = await RunAsync( [ "-F", "1,3" ], "a b c\n"u8.ToArray() );
		Assert.Equal( "a c\n"u8.ToArray(), result.Output );
	}


	/// <summary>Verifies that plain whitespace mode defaults selected-field output to TAB.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task WhitespaceDelimitedModeDefaultsOutputToTab() {
		var result = await RunAsync( [ "-w", "-f", "1,3" ], "a b c\n"u8.ToArray() );
		Assert.Equal( "a\tc\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies that <c>-F</c> retains its space output default when an explicit delimiter disables whitespace parsing.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task FieldWhitespaceShorthandRetainsSpaceOutputWithExplicitDelimiter() {
		var result = await RunAsync( [ "-F", "1,3", "-d", ":" ], "a:b:c\n"u8.ToArray() );
		Assert.Equal( "a c\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies a multibyte delimiter under the UTF-8 profile.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SupportsOneMultibyteDelimiterCharacter() {
		var result = await RunAsync( [ "-d", "界", "-f", "2" ], Encoding.UTF8.GetBytes( "a界b界c\n" ) );
		Assert.Equal( Encoding.UTF8.GetBytes( "b\n" ), result.Output );
	}

	/// <summary>Verifies the special case where the field and record separators are identical.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RecordSeparatorCanAlsoSeparateFields() {
		var result = await RunAsync( [ "-d", string.Empty, "-z", "-f", "2" ], [ (byte)'a', 0, (byte)'b', 0 ] );
		Assert.Equal( new byte[] { (byte)'b', 0 }, result.Output );
	}

	/// <summary>Verifies a trailing record separator terminates the logical field record without creating another field.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TrailingRecordSeparatorDoesNotCreateAnotherField() {
		var ordinary = await RunAsync( [ "-d", string.Empty, "-z", "-f", "2" ], [ (byte)'a', 0 ] );
		var suppressed = await RunAsync( [ "-s", "-d", string.Empty, "-z", "-f", "2" ], [ (byte)'a', 0 ] );
		Assert.Equal( new byte[] { 0 }, ordinary.Output );
		Assert.Empty( suppressed.Output );
	}

	/// <summary>Verifies an interior record separator makes the input a delimited logical record even when no existing field is selected.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task InteriorRecordSeparatorPreventsOnlyDelimitedSuppression() {
		var result = await RunAsync( [ "-s", "-d", string.Empty, "-z", "-f", "3" ], [ (byte)'a', 0, (byte)'b', 0 ] );
		Assert.Equal( new byte[] { 0 }, result.Output );
	}

	private static async Task<(int Status, byte[] Output, string Error)> RunAsync( string[] args, byte[] input ) {
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		var error = new StringWriter();
		var context = new CommandContext( "cut", new StringReader( string.Empty ), new StringWriter(), error, inputStream, outputStream );
		var status = await Command.RunAsync( args, context );
		return (status, outputStream.ToArray(), error.ToString());
	}
}
