namespace Icod.CoreUtils.Shared.Tests.Text;

using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Tests deterministic locale-provider selection for text-layout commands.</summary>
public sealed class TextLocaleEnvironmentTests {
	/// <summary>Verifies that LC_ALL takes precedence and selects the C byte profile.</summary>
	[Fact]
	public void LcAllTakesPrecedence() {
		var provider = TextLocaleEnvironment.Resolve( "C", "en_US.UTF-8", "en_US.UTF-8" );
		Assert.IsType<PosixCLocaleProvider>( provider );
	}

	/// <summary>Verifies that UTF-8 and unspecified locales select the Unicode profile.</summary>
	[Theory]
	[InlineData( null, null, null )]
	[InlineData( "", "C.UTF-8", "C" )]
	[InlineData( null, null, "en_US.UTF-8" )]
	public void NonPosixLocalesUseUnicodeProfile( string? lcAll, string? lcCtype, string? lang ) {
		var provider = TextLocaleEnvironment.Resolve( lcAll, lcCtype, lang );
		Assert.IsType<UnicodeTextLocaleProvider>( provider );
	}
}
