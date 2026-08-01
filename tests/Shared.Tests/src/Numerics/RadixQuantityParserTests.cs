namespace Icod.CoreUtils.Shared.Tests.Numerics;

using System.Numerics;
using Icod.CoreUtils.Shared.Numerics;
using Xunit;

/// <summary>Tests radix-aware quantity parsing shared by GNU-style commands.</summary>
public sealed class RadixQuantityParserTests {

	/// <summary>Decimal, octal, and hexadecimal forms use C-style base detection.</summary>
	[Theory]
	[InlineData( "10", 10 )]
	[InlineData( "010", 8 )]
	[InlineData( "0x10", 16 )]
	[InlineData( "0XfF", 255 )]
	[InlineData( "+17", 17 )]
	public void ParsesSupportedRadices( string text, long expected ) {
		var result = RadixQuantityParser.ParseInt64( text );
		Assert.True( result.IsSuccess );
		Assert.Equal( expected, result.Value );
	}

	/// <summary>Suffixes are applied after the radix-specific numeric portion.</summary>
	[Fact]
	public void AppliesExactSuffixes() {
		var suffixes = new NumericSuffixTable(
			new NumericSuffix( string.Empty, BigInteger.One ),
			new NumericSuffix( "K", new BigInteger( 1024 ) )
		);
		var result = RadixQuantityParser.ParseInt64( "010K", suffixes );
		Assert.True( result.IsSuccess );
		Assert.Equal( 8192, result.Value );
		Assert.Equal( "K", result.Suffix );
	}

	/// <summary>Invalid octal digits do not silently become decimal input.</summary>
	[Theory]
	[InlineData( "08" )]
	[InlineData( "09K" )]
	[InlineData( "0x" )]
	public void RejectsMalformedRadixValues( string text ) {
		var result = RadixQuantityParser.ParseInt64( text );
		Assert.False( result.IsSuccess );
	}

	/// <summary>Negative values and arithmetic overflow are rejected by default.</summary>
	[Theory]
	[InlineData( "-1", QuantityParseErrorKind.NegativeSignNotAllowed )]
	[InlineData( "0x8000000000000000", QuantityParseErrorKind.Overflow )]
	public void RejectsDisallowedOrOverflowingValues(
		string text,
		QuantityParseErrorKind expected
	) {
		var result = RadixQuantityParser.ParseInt64( text );
		Assert.False( result.IsSuccess );
		Assert.Equal( expected, result.ErrorKind );
	}
}
