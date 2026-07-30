namespace Icod.CoreUtils.Shared.Tests.Escapes;

using System.Text;
using Icod.CoreUtils.Shared.Escapes;
using Xunit;

/// <summary>Tests the low-level GNU tr byte-escape grammar and escaped-state metadata.</summary>
public sealed class TrByteEscapeParserTests {

	/// <summary>Verifies named byte escapes and their escaped metadata.</summary>
	[Fact]
	public void ParserRecognizesNamedEscapes() {
		var result = TrByteEscapeParser.Parse( "\\a\\b\\f\\n\\r\\t\\v\\\\" );
		Assert.True( result.IsSuccess );
		Assert.Equal(
			new byte[] { (byte)'\a', (byte)'\b', (byte)'\f', (byte)'\n', (byte)'\r', (byte)'\t', (byte)'\v', (byte)'\\' },
			result.Bytes.Select( value => value.Value ).ToArray()
		);
		Assert.All( result.Bytes, value => Assert.True( value.WasEscaped ) );
	}

	/// <summary>Verifies one-to-three-digit octal byte escapes.</summary>
	[Fact]
	public void ParserRecognizesOctalEscapes() {
		var result = TrByteEscapeParser.Parse( "\\1\\12\\101" );
		Assert.Equal( new byte[] { 1, 10, 65 }, result.Bytes.Select( value => value.Value ).ToArray() );
		Assert.All( result.Bytes, value => Assert.True( value.WasEscaped ) );
		Assert.Empty( result.Diagnostics );
	}

	/// <summary>Verifies GNU's deterministic treatment of a three-digit octal value above one byte.</summary>
	[Fact]
	public void OverflowingThreeDigitOctalConsumesOnlyTwoDigits() {
		var result = TrByteEscapeParser.Parse( "\\400" );
		Assert.Equal( new byte[] { 32, (byte)'0' }, result.Bytes.Select( value => value.Value ).ToArray() );
		Assert.True( result.Bytes[0].WasEscaped );
		Assert.False( result.Bytes[1].WasEscaped );
		var diagnostic = Assert.Single( result.Diagnostics );
		Assert.Equal( EscapeDiagnosticCode.AmbiguousOctalEscape, diagnostic.Code );
		Assert.Equal( EscapeDiagnosticSeverity.Warning, diagnostic.Severity );
	}

	/// <summary>Verifies that a trailing backslash is retained as unescaped data with a warning.</summary>
	[Fact]
	public void TrailingBackslashIsRetainedWithWarning() {
		var result = TrByteEscapeParser.Parse( "x\\" );
		Assert.Equal( new byte[] { (byte)'x', (byte)'\\' }, result.Bytes.Select( value => value.Value ).ToArray() );
		Assert.False( result.Bytes[0].WasEscaped );
		Assert.False( result.Bytes[1].WasEscaped );
		Assert.Equal( EscapeDiagnosticCode.TrailingBackslash, Assert.Single( result.Diagnostics ).Code );
	}

	/// <summary>Verifies that unknown escapes discard the backslash but retain escaped-state metadata.</summary>
	[Fact]
	public void UnknownEscapeRetainsEscapedCharacter() {
		var result = TrByteEscapeParser.Parse( "\\q" );
		var value = Assert.Single( result.Bytes );
		Assert.Equal( (byte)'q', value.Value );
		Assert.True( value.WasEscaped );
	}

	/// <summary>Verifies ordinary and escaped multibyte characters.</summary>
	[Fact]
	public void UnicodeBytesRetainEscapedState() {
		var ordinary = TrByteEscapeParser.Parse( "界" );
		var escaped = TrByteEscapeParser.Parse( "\\界" );
		Assert.Equal( Encoding.UTF8.GetBytes( "界" ), ordinary.Bytes.Select( value => value.Value ).ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( "界" ), escaped.Bytes.Select( value => value.Value ).ToArray() );
		Assert.All( ordinary.Bytes, value => Assert.False( value.WasEscaped ) );
		Assert.All( escaped.Bytes, value => Assert.True( value.WasEscaped ) );
	}

	/// <summary>Verifies injection of a deterministic stateless ordinary-scalar encoding.</summary>
	[Fact]
	public void ParserAcceptsInjectedStatelessEncoding() {
		var result = TrByteEscapeParser.Parse( "é", Encoding.Latin1 );
		var parsed = Assert.Single( result.Bytes );
		Assert.Equal( (byte)0xE9, parsed.Value );
		Assert.False( parsed.WasEscaped );
	}

	/// <summary>Verifies deterministic rejection of invalid managed scalar input.</summary>
	[Fact]
	public void InvalidUtf16IsAnError() {
		var result = TrByteEscapeParser.Parse( "\uD800" );
		Assert.False( result.IsSuccess );
		Assert.Equal( EscapeDiagnosticCode.InvalidUnicodeScalar, Assert.Single( result.Diagnostics ).Code );
	}

}
