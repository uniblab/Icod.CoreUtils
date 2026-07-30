namespace Icod.CoreUtils.Shared.Tests.Ordering;

using System.Globalization;
using Icod.CoreUtils.Shared.Ordering;
using Xunit;

/// <summary>Tests byte-oriented collation over C/POSIX and managed-culture profiles.</summary>
public sealed class ByteCollationComparerTests {
	/// <summary>Verifies exact raw-byte ordering for the C locale.</summary>
	[Fact]
	public void BytewiseProfilePreservesRawOrdering() {
		var comparer = new ByteCollationComparer(
			new SystemCollationProvider( CollationProfile.CreateBytewise() )
		);
		Assert.True( 0 < comparer.Compare( new byte[] { 0xff }, new byte[] { 0x7f } ) );
	}

	/// <summary>Verifies ASCII case folding for case-insensitive C-locale comparisons.</summary>
	[Fact]
	public void BytewiseIgnoreCaseFoldsAsciiLetters() {
		var comparer = new ByteCollationComparer(
			new SystemCollationProvider( CollationProfile.CreateBytewise() ),
			ignoreCase: true
		);
		Assert.Equal( 0, comparer.Compare( "Alpha"u8.ToArray(), "aLPHA"u8.ToArray() ) );
	}

	/// <summary>Verifies deterministic raw-byte fallback for invalid UTF-8.</summary>
	[Fact]
	public void InvalidUtf8FallsBackToRawBytes() {
		var comparer = new ByteCollationComparer(
			new SystemCollationProvider(
				CollationProfile.CreateCulture( CultureInfo.GetCultureInfo( "en-US" ) )
			)
		);
		Assert.NotEqual( 0, comparer.Compare( new byte[] { 0xff }, new byte[] { 0xfe } ) );
	}

	/// <summary>Verifies that managed linguistic profiles use their culture's comparison rules.</summary>
	[Fact]
	public void CultureProfileUsesManagedCollation() {
		var comparer = new ByteCollationComparer(
			new SystemCollationProvider(
				CollationProfile.CreateCulture( CultureInfo.GetCultureInfo( "en-US" ) )
			),
			ignoreCase: true
		);
		Assert.Equal( 0, comparer.Compare( "Résumé"u8.ToArray(), "résumé"u8.ToArray() ) );
	}
}
