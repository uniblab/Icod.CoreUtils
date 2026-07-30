namespace Icod.CoreUtils.Shared.Tests.Escapes;

using Icod.CoreUtils.Shared.Formatting;
using Xunit;

/// <summary>Characterizes the established formatting escape grammar after scanner extraction.</summary>
public sealed class FormattingEscapeCompatibilityTests {

	/// <summary>Verifies named, hexadecimal, Unicode, and octal formatting escapes.</summary>
	[Fact]
	public void FormattingGrammarRemainsCompatible() {
		var result = GnuEscapeDecoder.Decode( "A\\n\\x42\\u0043\\101\\0101" );
		Assert.Equal( "A\nBCAA", result.Text );
		Assert.False( result.StopOutput );
	}

	/// <summary>Verifies that unknown formatting escapes retain their backslash.</summary>
	[Fact]
	public void UnknownFormattingEscapeRetainsBackslash() {
		Assert.Equal( "\\q", GnuEscapeDecoder.Decode( "\\q" ).Text );
	}

	/// <summary>Verifies that a trailing formatting backslash remains literal.</summary>
	[Fact]
	public void TrailingFormattingBackslashRemainsLiteral() {
		Assert.Equal( "x\\", GnuEscapeDecoder.Decode( "x\\" ).Text );
	}

	/// <summary>Verifies stop-output behavior and its disabled interpretation.</summary>
	[Fact]
	public void FormattingStopOutputPolicyRemainsCompatible() {
		var stopped = GnuEscapeDecoder.Decode( "a\\cb" );
		Assert.Equal( "a", stopped.Text );
		Assert.True( stopped.StopOutput );
		var disabled = GnuEscapeDecoder.Decode( "a\\cb", allowStopOutput: false );
		Assert.Equal( "acb", disabled.Text );
		Assert.False( disabled.StopOutput );
	}

	/// <summary>Verifies the existing bare-octal option.</summary>
	[Fact]
	public void BareOctalCanBeDisabled() {
		Assert.Equal( "A", GnuEscapeDecoder.Decode( "\\101" ).Text );
		Assert.Equal( "\\101", GnuEscapeDecoder.Decode( "\\101", allowBareOctal: false ).Text );
	}

	/// <summary>Verifies existing malformed hexadecimal and Unicode diagnostics.</summary>
	[Fact]
	public void MalformedFormattingEscapesStillThrow() {
		Assert.Throws<FormatException>( () => GnuEscapeDecoder.Decode( "\\x" ) );
		Assert.Throws<FormatException>( () => GnuEscapeDecoder.Decode( "\\u12" ) );
		Assert.Throws<FormatException>( () => GnuEscapeDecoder.Decode( "\\U00110000" ) );
	}

}
