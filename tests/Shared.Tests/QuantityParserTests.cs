namespace Icod.CoreUtils.Shared.Tests;

using System.Globalization;
using Icod.CoreUtils.Shared.Numerics;
using Xunit;

public sealed class QuantityParserTests {

	[Theory]
	[InlineData( "10", 10L )]
	[InlineData( "+10", 10L )]
	[InlineData( "2K", 2048L )]
	[InlineData( "2kB", 2000L )]
	[InlineData( "1MiB", 1048576L )]
	[InlineData( "1b", 512L )]
	public void ParsesGnuIntegerCounts(
		string text,
		long expected
	) {
		var result = QuantityParser.ParseInt64(
			text,
			NumericSuffixTable.GnuCounts
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( expected, result.Value );
	}

	[Fact]
	public void NegativeValuesCanBeEnabled() {
		var result = QuantityParser.ParseInt64(
			"-15",
			allowLeadingMinus: true
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( -15, result.Value );
	}

	[Fact]
	public void DisallowedSignsAreReported() {
		Assert.Equal(
			QuantityParseErrorKind.PositiveSignNotAllowed,
			QuantityParser.ParseInt64(
				"+1",
				allowLeadingPlus: false
			).ErrorKind
		);
		Assert.Equal(
			QuantityParseErrorKind.NegativeSignNotAllowed,
			QuantityParser.ParseInt64(
				"-1"
			).ErrorKind
		);
	}

	[Fact]
	public void UppercaseKbIsNotTheGnuDecimalKilobyteSuffix() {
		var result = QuantityParser.ParseInt64(
			"2KB",
			NumericSuffixTable.GnuCounts
		);

		Assert.False( result.IsSuccess );
		Assert.Equal( QuantityParseErrorKind.InvalidSuffix, result.ErrorKind );
	}

	[Fact]
	public void InvalidSuffixIsReported() {
		var result = QuantityParser.ParseInt64(
			"10KB",
			NumericSuffixTable.GnuCounts
		);

		Assert.False( result.IsSuccess );
		Assert.Equal( QuantityParseErrorKind.InvalidSuffix, result.ErrorKind );
		Assert.Equal( "KB", result.Suffix );
	}

	[Fact]
	public void OverflowCanBeRejectedOrClamped() {
		var rejected = QuantityParser.ParseInt64(
			"999999999999999999999Q",
			NumericSuffixTable.GnuCounts
		);
		var clamped = QuantityParser.ParseInt64(
			"999999999999999999999Q",
			NumericSuffixTable.GnuCounts,
			overflowBehavior: OverflowBehavior.Clamp
		);

		Assert.Equal( QuantityParseErrorKind.Overflow, rejected.ErrorKind );
		Assert.True( clamped.IsSuccess );
		Assert.Equal( long.MaxValue, clamped.Value );
	}

	[Theory]
	[InlineData( "0.1s", 0.1 )]
	[InlineData( "1.5m", 90.0 )]
	[InlineData( "2h", 7200.0 )]
	[InlineData( "1e3s", 1000.0 )]
	public void ParsesFloatingDurations(
		string text,
		double expected
	) {
		var result = QuantityParser.ParseDouble(
			text,
			FloatingSuffixTable.TimeSeconds
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( expected, result.Value, precision: 10 );
	}

	[Fact]
	public void ParsingIsCultureInvariant() {
		var prior = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = new CultureInfo(
				"fr-FR"
			);
			var result = QuantityParser.ParseDouble(
				"1.5s",
				FloatingSuffixTable.TimeSeconds
			);

			Assert.True( result.IsSuccess );
			Assert.Equal( 1.5, result.Value );
		} finally {
			CultureInfo.CurrentCulture = prior;
		}
	}

}
