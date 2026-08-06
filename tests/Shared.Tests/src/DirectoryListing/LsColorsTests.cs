namespace Icod.CoreUtils.Shared.Tests.DirectoryListing;

using Icod.CoreUtils.Shared.DirectoryListing;
using Xunit;

/// <summary>Verifies GNU LS_COLORS parsing, matching, and presentation controls.</summary>
public sealed class LsColorsTests {
	/// <summary>Verifies decoded indicator and extension rules.</summary>
	[Fact]
	public void ParsesIndicatorsPatternsAndEscapes() {
		var colors = LsColors.Parse( "di=01;34:*.cs=00;32:lc=\\e[:rc=m:ec=\\e[0m" );

		Assert.True( colors.TryGetIndicator( "di", out var directory ) );
		Assert.Equal( "01;34", directory );
		Assert.Equal( "00;32", colors.ResolveStyle( "Program.cs", "fi" ) );
		Assert.Equal( "01;34", colors.ResolveStyle( "folder.cs", "di" ) );
		Assert.Equal( "\u001b[00;32mProgram.cs\u001b[0m", colors.Apply( "Program.cs", "00;32" ) );
	}

	/// <summary>Verifies that later extension rules take precedence.</summary>
	[Fact]
	public void LastMatchingPatternWins() {
		var colors = LsColors.Parse( "*.gz=31:*.tar.gz=32:fi=0" );

		Assert.Equal( "32", colors.ResolveStyle( "archive.tar.gz", "fi" ) );
	}

	/// <summary>Verifies escaped separators survive a serialize-and-parse round trip.</summary>
	[Fact]
	public void EscapedSeparatorsRoundTrip() {
		var original = LsColors.Parse( "*.a\\:b=01\\=32:di=01;34" );
		var reparsed = LsColors.Parse( original.Serialize() );

		Assert.Equal( "01=32", reparsed.ResolveStyle( "name.a:b", "fi" ) );
		Assert.True( reparsed.TryGetIndicator( "di", out var directory ) );
		Assert.Equal( "01;34", directory );
	}

	/// <summary>Verifies malformed entries produce a controlled parse failure.</summary>
	[Fact]
	public void RejectsMalformedEntries() {
		Assert.Throws<FormatException>( () => LsColors.Parse( "di=01;34:broken" ) );
		Assert.Throws<FormatException>( () => LsColors.Decode( "\\" ) );
	}
}
