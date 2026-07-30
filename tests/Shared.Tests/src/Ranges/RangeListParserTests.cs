namespace Icod.CoreUtils.Shared.Tests.Ranges;

using Icod.CoreUtils.Shared.Ranges;
using Xunit;

/// <summary>Tests GNU-style positional range-list parsing and structured failures.</summary>
public sealed class RangeListParserTests {

	/// <summary>Verifies all four positional range forms.</summary>
	[Fact]
	public void ParserAcceptsClosedLeadingAndTrailingOpenForms() {
		var result = RangeListParser.Parse( "3,5-,8-10,-2" );
		Assert.True( result.IsSuccess );
		var ranges = Assert.IsType<RangeSet>( result.Value ).Ranges;
		Assert.Equal( 3, ranges.Count );
		Assert.Equal( new InclusiveRange( 1, 2 ), ranges[0] );
		Assert.Equal( new InclusiveRange( 3, 3 ), ranges[1] );
		Assert.Equal( new InclusiveRange( 5, null ), ranges[2] );
	}

	/// <summary>Verifies commas, spaces, and tabs as separators.</summary>
	[Fact]
	public void ParserAcceptsSupportedSeparators() {
		var result = RangeListParser.Parse( "1,3 5\t7" );
		Assert.True( result.IsSuccess );
		Assert.Equal(
			new ulong[] { 1, 3, 5, 7 },
			Assert.IsType<RangeSet>( result.Value ).Ranges.Select( value => value.Start ).ToArray()
		);
	}

	/// <summary>Verifies the deterministic ASCII-only blank-separator profile.</summary>
	[Fact]
	public void ParserRejectsNonAsciiOrVerticalWhiteSpaceSeparators() {
		Assert.Equal(
			RangeParseErrorCode.UnexpectedCharacter,
			RangeListParser.Parse( "1\n2" ).Error?.Code
		);
		Assert.Equal(
			RangeParseErrorCode.UnexpectedCharacter,
			RangeListParser.Parse( "1\u00A02" ).Error?.Code
		);
	}

	/// <summary>Verifies that overlapping values normalize while adjacent ranges retain boundaries.</summary>
	[Fact]
	public void ParserPreservesAdjacentRangeBoundaries() {
		var result = RangeListParser.Parse( "3-4,1-2,4-6" );
		var ranges = Assert.IsType<RangeSet>( result.Value ).Ranges;
		Assert.Equal( 2, ranges.Count );
		Assert.Equal( new InclusiveRange( 1, 2 ), ranges[0] );
		Assert.Equal( new InclusiveRange( 3, 6 ), ranges[1] );
	}

	/// <summary>Verifies complement parsing over the configured one-based domain.</summary>
	[Fact]
	public void ParserCanComplementSelection() {
		var result = RangeListParser.Parse(
			"2-4,7-",
			new RangeListParserOptions { Complement = true }
		);
		Assert.Equal(
			new[] {
				new InclusiveRange( 1, 1 ),
				new InclusiveRange( 5, 6 )
			},
			Assert.IsType<RangeSet>( result.Value ).Ranges
		);
	}

	/// <summary>Verifies optional bare-hyphen whole-domain syntax.</summary>
	[Fact]
	public void ParserCanEnableSingleDash() {
		var disabled = RangeListParser.Parse( "-" );
		Assert.Equal( RangeParseErrorCode.MissingEndpoint, disabled.Error?.Code );
		var enabled = RangeListParser.Parse(
			"-",
			new RangeListParserOptions { AllowSingleDash = true }
		);
		Assert.Equal(
			new InclusiveRange( 1, null ),
			Assert.Single( Assert.IsType<RangeSet>( enabled.Value ).Ranges )
		);
	}

	/// <summary>Verifies configurable zero-based general ranges.</summary>
	[Fact]
	public void ParserSupportsGeneralZeroBasedProfile() {
		var result = RangeListParser.Parse(
			"0-2",
			new RangeListParserOptions { MinimumValue = 0 }
		);
		Assert.Equal(
			new InclusiveRange( 0, 2 ),
			Assert.Single( Assert.IsType<RangeSet>( result.Value ).Ranges )
		);
	}

	/// <summary>Verifies deterministic failures for empty and repeated separators.</summary>
	[Theory]
	[InlineData( "", RangeParseErrorCode.EmptyList, 0 )]
	[InlineData( ",1", RangeParseErrorCode.ExpectedNumber, 0 )]
	[InlineData( "1,,2", RangeParseErrorCode.ExpectedNumber, 2 )]
	[InlineData( "1,", RangeParseErrorCode.ExpectedNumber, 2 )]
	[InlineData( "1  2", RangeParseErrorCode.ExpectedNumber, 2 )]
	public void ParserRejectsMissingListElements(
		string value,
		RangeParseErrorCode code,
		int index
	) {
		var result = RangeListParser.Parse( value );
		Assert.False( result.IsSuccess );
		Assert.Equal( code, result.Error?.Code );
		Assert.Equal( index, result.Error?.CharacterIndex );
	}

	/// <summary>Verifies deterministic failures for malformed endpoints and ranges.</summary>
	[Theory]
	[InlineData( "0", RangeParseErrorCode.ValueBelowMinimum )]
	[InlineData( "3-2", RangeParseErrorCode.DecreasingRange )]
	[InlineData( "1-2-3", RangeParseErrorCode.MultipleDashes )]
	[InlineData( "+1", RangeParseErrorCode.UnexpectedCharacter )]
	[InlineData( "a", RangeParseErrorCode.UnexpectedCharacter )]
	[InlineData( "18446744073709551616", RangeParseErrorCode.NumberOverflow )]
	[InlineData( "18446744073709551615", RangeParseErrorCode.ValueAboveMaximum )]
	public void ParserRejectsMalformedValues(
		string value,
		RangeParseErrorCode code
	) {
		var result = RangeListParser.Parse( value );
		Assert.False( result.IsSuccess );
		Assert.Equal( code, result.Error?.Code );
		Assert.False( String.IsNullOrEmpty( result.Error?.Message ) );
	}

	/// <summary>Verifies profile restrictions for leading and trailing open ranges.</summary>
	[Fact]
	public void ParserHonorsOpenRangeRestrictions() {
		var trailing = RangeListParser.Parse(
			"2-",
			new RangeListParserOptions { AllowOpenEnded = false }
		);
		Assert.Equal( RangeParseErrorCode.OpenEndedNotAllowed, trailing.Error?.Code );
		var leading = RangeListParser.Parse(
			"-2",
			new RangeListParserOptions { AllowLeadingOpenRange = false }
		);
		Assert.Equal( RangeParseErrorCode.LeadingOpenRangeNotAllowed, leading.Error?.Code );
	}

	/// <summary>Verifies explicit maximum validation and invalid parser profiles.</summary>
	[Fact]
	public void ParserHonorsMaximumAndValidatesProfile() {
		var result = RangeListParser.Parse(
			"6",
			new RangeListParserOptions { MaximumValue = 5 }
		);
		Assert.Equal( RangeParseErrorCode.ValueAboveMaximum, result.Error?.Code );
		Assert.Throws<ArgumentException>(
			() => RangeListParser.Parse(
				"1",
				new RangeListParserOptions {
					MinimumValue = 2,
					MaximumValue = 1
				}
			)
		);
	}

}
