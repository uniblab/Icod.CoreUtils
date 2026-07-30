namespace Icod.CoreUtils.Shared.Tests.Escapes;

using System.Text;
using Icod.CoreUtils.Shared.Escapes;
using Xunit;

/// <summary>Tests the GNU paste delimiter-list grammar.</summary>
public sealed class PasteDelimiterParserTests {

	/// <summary>Verifies that an empty argument denotes one empty separator slot.</summary>
	[Fact]
	public void EmptyArgumentProducesEmptyCycleElement() {
		var result = PasteDelimiterParser.Parse( String.Empty );
		Assert.True( result.IsSuccess );
		var cycle = Assert.IsType<Icod.CoreUtils.Shared.Delimiters.SeparatorCycle>( result.Value );
		Assert.Equal( 1, cycle.Count );
		Assert.True( cycle[0].IsEmpty );
	}

	/// <summary>Verifies named escapes, empty slots, ordinary characters, and multibyte scalars.</summary>
	[Fact]
	public void ParserProducesOneCycleElementPerDelimiterCharacter() {
		var result = PasteDelimiterParser.Parse( "\\0,\\t界" );
		var cycle = Assert.IsType<Icod.CoreUtils.Shared.Delimiters.SeparatorCycle>( result.Value );
		Assert.Equal( 4, cycle.Count );
		Assert.True( cycle[0].IsEmpty );
		Assert.Equal( new byte[] { (byte)',' }, cycle[1].Bytes.ToArray() );
		Assert.Equal( new byte[] { (byte)'\t' }, cycle[2].Bytes.ToArray() );
		Assert.Equal( Encoding.UTF8.GetBytes( "界" ), cycle[3].Bytes.ToArray() );
	}

	/// <summary>Verifies the complete named escape set accepted by paste.</summary>
	[Fact]
	public void ParserRecognizesNamedEscapes() {
		var result = PasteDelimiterParser.Parse( "\\b\\f\\n\\r\\t\\v\\\\" );
		var cycle = Assert.IsType<Icod.CoreUtils.Shared.Delimiters.SeparatorCycle>( result.Value );
		Assert.Equal(
			new byte[] { (byte)'\b', (byte)'\f', (byte)'\n', (byte)'\r', (byte)'\t', (byte)'\v', (byte)'\\' },
			cycle.Separators.SelectMany( value => value.Bytes.ToArray() ).ToArray()
		);
	}

	/// <summary>Verifies that unknown escapes discard the backslash.</summary>
	[Fact]
	public void UnknownEscapeRetainsOnlyItsCharacter() {
		var result = PasteDelimiterParser.Parse( "\\q" );
		var cycle = Assert.IsType<Icod.CoreUtils.Shared.Delimiters.SeparatorCycle>( result.Value );
		Assert.Equal( new byte[] { (byte)'q' }, cycle[0].Bytes.ToArray() );
		Assert.Empty( result.Diagnostics );
	}

	/// <summary>Verifies that a trailing backslash is a structured error.</summary>
	[Fact]
	public void TrailingBackslashIsAnError() {
		var result = PasteDelimiterParser.Parse( "x\\" );
		Assert.False( result.IsSuccess );
		var diagnostic = Assert.Single( result.Diagnostics );
		Assert.Equal( EscapeDiagnosticCode.TrailingBackslash, diagnostic.Code );
		Assert.Equal( EscapeDiagnosticSeverity.Error, diagnostic.Severity );
		Assert.Equal( 1, diagnostic.SourceOffset );
	}

	/// <summary>Verifies injection of a deterministic stateless ordinary-scalar encoding.</summary>
	[Fact]
	public void ParserAcceptsInjectedStatelessEncoding() {
		var result = PasteDelimiterParser.Parse( "é", Encoding.Latin1 );
		var cycle = Assert.IsType<Icod.CoreUtils.Shared.Delimiters.SeparatorCycle>( result.Value );
		Assert.Equal( new byte[] { 0xE9 }, cycle[0].Bytes.ToArray() );
	}

	/// <summary>Verifies deterministic rejection of invalid managed scalar input.</summary>
	[Fact]
	public void InvalidUtf16IsAnError() {
		var result = PasteDelimiterParser.Parse( "\uD800" );
		Assert.False( result.IsSuccess );
		Assert.Equal( EscapeDiagnosticCode.InvalidUnicodeScalar, Assert.Single( result.Diagnostics ).Code );
	}

}
