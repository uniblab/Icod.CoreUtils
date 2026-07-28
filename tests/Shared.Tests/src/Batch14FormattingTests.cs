namespace Icod.CoreUtils.Shared.Tests;

using System.Numerics;
using Icod.CoreUtils.Shared.Formatting;
using Icod.CoreUtils.Shared.Numerics;
using Xunit;

public sealed class Batch14FormattingTests {
	[Fact]
	public void EscapeDecoderHandlesSimpleNumericAndUnicodeEscapes() {
		var result = GnuEscapeDecoder.Decode( "a\\n\\101\\x42\\u03bb\\U0001f600" );
		Assert.Equal( "a\nABλ😀", result.Text );
		Assert.False( result.StopOutput );
	}

	[Fact]
	public void EscapeDecoderReportsBackslashC() {
		var result = GnuEscapeDecoder.Decode( "before\\cafter" );
		Assert.Equal( "before", result.Text );
		Assert.True( result.StopOutput );
	}

	[Fact]
	public void BigRationalParsesScientificNotationExactly() {
		Assert.True( BigRational.TryParseDecimal( "-1.25e3", out var value, out var digits ) );
		Assert.Equal( 2, digits );
		Assert.Equal( new BigRational( -1250, 1 ), value );
	}

	[Theory]
	[InlineData( RationalRoundingMode.Up, 2 )]
	[InlineData( RationalRoundingMode.Down, 1 )]
	[InlineData( RationalRoundingMode.FromZero, 2 )]
	[InlineData( RationalRoundingMode.TowardsZero, 1 )]
	[InlineData( RationalRoundingMode.Nearest, 2 )]
	public void BigRationalRoundingIsExplicit( RationalRoundingMode mode, int expected ) {
		var value = new BigRational( 3, 2 );
		Assert.Equal( new BigInteger( expected ), value.Round( mode ) );
	}

	[Fact]
	public void BigRationalPreservesRequestedFractionDigits() {
		var value = new BigRational( 123, 100 );
		Assert.Equal( "1.2300", value.ToFixedString( 4 ) );
		Assert.Equal( "1.23", value.ToDecimalString() );
	}
}
