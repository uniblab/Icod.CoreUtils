namespace Icod.CoreUtils.Shared.Tests.Text;

using Icod.CommandFramework.Text;
using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Tests the reusable GNU-style tab-stop grammar.</summary>
public sealed class TabStopParserTests {
	/// <summary>Verifies that one plain value means a globally recurring interval.</summary>
	[Fact]
	public void SingleValueCreatesPeriodicStops() {
		var stops = ParseSuccessfully( "4" );
		Assert.Empty( stops.ExplicitStops );
		Assert.Equal( TabStopContinuationKind.Absolute, stops.Continuation.Kind );
		Assert.Equal( 4UL, stops.Continuation.Interval );
		Assert.Equal<ulong?>( 4UL, stops.GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 8UL, stops.GetNextStop( 4 ) );
	}

	/// <summary>Verifies comma and blank separators in one specification.</summary>
	[Fact]
	public void ParserAcceptsCommaAndBlankSeparators() {
		var stops = ParseSuccessfully( " 4, 8\t12 " );
		Assert.Equal(
			new ulong[] { 4, 8, 12 },
			stops.ExplicitStops.ToArray()
		);
		Assert.Equal( TabStopContinuationKind.None, stops.Continuation.Kind );
	}

	/// <summary>Verifies GNU compatibility for empty lists and redundant separators.</summary>
	[Fact]
	public void EmptyListsAndRedundantSeparatorsUseAvailableValuesOrDefaults() {
		Assert.Same( TabStopSet.Default, ParseSuccessfully( string.Empty ) );
		Assert.Same( TabStopSet.Default, ParseSuccessfully( ", ,\t," ) );
		var stops = ParseSuccessfully( ", 4,,8  ,12," );
		Assert.Equal(
			new ulong[] { 4, 8, 12 },
			stops.ExplicitStops.ToArray()
		);
	}

	/// <summary>Verifies that repeated option values contribute to one ordered list.</summary>
	[Fact]
	public void ParserCombinesRepeatedSpecifications() {
		var result = TabStopParser.Parse(
			new[] { "4,8", "12", "+8" }
		);
		Assert.True( result.IsSuccess );
		var stops = Assert.IsType<TabStopSet>( result.TabStops );
		Assert.Equal(
			new ulong[] { 4, 8, 12 },
			stops.ExplicitStops.ToArray()
		);
		Assert.Equal( TabStopContinuationKind.Relative, stops.Continuation.Kind );
		Assert.Equal( 8UL, stops.Continuation.Interval );
	}

	/// <summary>Verifies the globally aligned GNU <c>/N</c> continuation.</summary>
	[Fact]
	public void AbsoluteContinuationUsesGlobalMultiples() {
		var stops = ParseSuccessfully( "4,10,/8" );
		Assert.Equal<ulong?>( 10UL, stops.GetNextStop( 4 ) );
		Assert.Equal<ulong?>( 16UL, stops.GetNextStop( 10 ) );
		Assert.Equal<ulong?>( 24UL, stops.GetNextStop( 16 ) );
	}

	/// <summary>Verifies the final-explicit-stop-relative GNU <c>+N</c> continuation.</summary>
	[Fact]
	public void RelativeContinuationUsesFinalExplicitStop() {
		var stops = ParseSuccessfully( "4,10,+8" );
		Assert.Equal<ulong?>( 10UL, stops.GetNextStop( 4 ) );
		Assert.Equal<ulong?>( 18UL, stops.GetNextStop( 10 ) );
		Assert.Equal<ulong?>( 26UL, stops.GetNextStop( 18 ) );
	}

	/// <summary>Verifies standalone and legacy repeated continuation prefixes.</summary>
	[Fact]
	public void StandaloneAndRepeatedPrefixesUseColumnZeroOrigin() {
		Assert.Equal<ulong?>( 8UL, ParseSuccessfully( "/8" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 8UL, ParseSuccessfully( "+8" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 5UL, ParseSuccessfully( "//5" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 5UL, ParseSuccessfully( "+/5" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 5UL, ParseSuccessfully( "++5" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 5UL, ParseSuccessfully( "/+5" ).GetNextStop( 0 ) );
	}

	/// <summary>Verifies that a prefix without a number contributes no value.</summary>
	[Fact]
	public void PrefixWithoutNumberLeavesDefaultStops() {
		Assert.Same( TabStopSet.Default, ParseSuccessfully( "/" ) );
		Assert.Same( TabStopSet.Default, ParseSuccessfully( "+" ) );
		Assert.Equal<ulong?>( 5UL, ParseSuccessfully( "/,/5" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 5UL, ParseSuccessfully( "+,+5" ).GetNextStop( 0 ) );
	}

	/// <summary>Verifies GNU compatibility for zero-valued prefixed continuations.</summary>
	[Fact]
	public void ZeroValuedPrefixedContinuationsAreNoOps() {
		Assert.Same( TabStopSet.Default, ParseSuccessfully( "/0" ) );
		Assert.Same( TabStopSet.Default, ParseSuccessfully( "+0" ) );
		Assert.Equal<ulong?>( 4UL, ParseSuccessfully( "4,/0" ).GetNextStop( 0 ) );
		Assert.Equal<ulong?>( 4UL, ParseSuccessfully( "/0,4" ).GetNextStop( 0 ) );
	}

	/// <summary>Verifies deterministic errors for malformed specifications.</summary>
	/// <param name="specification">The malformed specification.</param>
	/// <param name="expectedCode">The expected stable error code.</param>
	[Theory]
	[InlineData( "zero", TabStopParseErrorCode.InvalidCharacter )]
	[InlineData( "18446744073709551616", TabStopParseErrorCode.NumberOverflow )]
	[InlineData( "0", TabStopParseErrorCode.Zero )]
	[InlineData( "8,4", TabStopParseErrorCode.NotIncreasing )]
	[InlineData( "4,4", TabStopParseErrorCode.NotIncreasing )]
	[InlineData( "/8,16", TabStopParseErrorCode.ContinuationNotLast )]
	[InlineData( "/8,+4", TabStopParseErrorCode.MutuallyExclusiveContinuations )]
	[InlineData( "4/8", TabStopParseErrorCode.SpecifierNotAtStart )]
	[InlineData( "4+8", TabStopParseErrorCode.SpecifierNotAtStart )]
	public void MalformedSpecificationsReturnControlledErrors(
		string specification,
		TabStopParseErrorCode expectedCode
	) {
		var result = TabStopParser.Parse( specification );
		Assert.False( result.IsSuccess );
		Assert.Null( result.TabStops );
		var error = Assert.IsType<TabStopParseError>( result.Error );
		Assert.Equal( expectedCode, error.Code );
		Assert.NotEmpty( error.Message );
	}

	/// <summary>Verifies null-input validation at both parser entry points.</summary>
	[Fact]
	public void ParserRejectsNullInput() {
		Assert.Throws<ArgumentNullException>(
			() => TabStopParser.Parse( (string)null! )
		);
		Assert.Throws<ArgumentNullException>(
			() => TabStopParser.Parse( (IEnumerable<string>)null! )
		);
		Assert.Throws<ArgumentNullException>(
			() => TabStopParser.Parse( new string[] { null! } )
		);
	}

	/// <summary>Verifies stable source metadata for a parser error.</summary>
	[Fact]
	public void ParserErrorIdentifiesSpecificationCharacterAndToken() {
		var result = TabStopParser.Parse( new[] { "4,8", "12x" } );
		var error = Assert.IsType<TabStopParseError>( result.Error );
		Assert.Equal( TabStopParseErrorCode.InvalidCharacter, error.Code );
		Assert.Equal( 1, error.SpecificationIndex );
		Assert.Equal( 2, error.CharacterIndex );
		Assert.Equal( "x", error.Token );
	}

	/// <summary>Verifies that an empty repeated-specification sequence selects the default stops.</summary>
	[Fact]
	public void EmptySpecificationSequenceSelectsDefaults() {
		var result = TabStopParser.Parse( Array.Empty<string>() );
		Assert.True( result.IsSuccess );
		Assert.Same( TabStopSet.Default, result.TabStops );
		Assert.Null( result.Error );
	}

	private static TabStopSet ParseSuccessfully( string specification ) {
		var result = TabStopParser.Parse( specification );
		Assert.True( result.IsSuccess );
		Assert.Null( result.Error );
		return Assert.IsType<TabStopSet>( result.TabStops );
	}
}
