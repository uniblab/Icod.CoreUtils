namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.CommandLine;
using Xunit;

public sealed class OptionParserTests {

	private static OptionParser CreateParser(
		OptionParserSettings? settings = null
	) {
		return new OptionParser(
			new OptionDefinition[ 5 ] {
				new(
					"all",
					'a',
					new string[ 1 ] { "all" }
				),
				new(
					"brief",
					'b',
					new string[ 2 ] { "brief", "quiet" },
					allowMultiple: false
				),
				new(
					"count",
					'n',
					new string[ 1 ] { "count" },
					OptionValueArity.Required
				),
				new(
					"suffix",
					'i',
					new string[ 1 ] { "in-place" },
					OptionValueArity.Optional
				),
				new(
					"color",
					null,
					new string[ 1 ] { "color" },
					OptionValueArity.Optional,
					optionalValueMayBeSeparate: true
				)
			},
			settings
		);
	}

	[Fact]
	public void ParseShortCluster() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "-ab" }
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( 2, result.Options.Count );
		Assert.Equal( "all", result.Options[ 0 ].Definition.Key );
		Assert.Equal( "brief", result.Options[ 1 ].Definition.Key );
	}

	[Theory]
	[InlineData( "-n10", "10" )]
	[InlineData( "--count=10", "10" )]
	public void ParseAttachedRequiredValues(
		string token,
		string expected
	) {
		var result = CreateParser().Parse(
			new string[ 1 ] { token }
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( expected, result.GetLastValue( "count" ) );
	}

	[Theory]
	[InlineData( "-n" )]
	[InlineData( "--count" )]
	public void ParseSeparateRequiredValues(
		string token
	) {
		var result = CreateParser().Parse(
			new string[ 2 ] { token, "15" }
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( "15", result.GetLastValue( "count" ) );
	}

	[Fact]
	public void OptionalShortValueMustBeAttachedByDefault() {
		var result = CreateParser().Parse(
			new string[ 2 ] { "-i", "backup" }
		);

		Assert.True( result.IsSuccess );
		Assert.Null( result.GetLastValue( "suffix" ) );
		Assert.Equal( new string[ 1 ] { "backup" }, result.Operands );
	}

	[Fact]
	public void OptionalLongValueMayBeSeparateWhenEnabled() {
		var result = CreateParser().Parse(
			new string[ 2 ] { "--color", "always" }
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( "always", result.GetLastValue( "color" ) );
		Assert.Empty( result.Operands );
	}

	[Fact]
	public void SeparateOptionalValueDoesNotConsumeAnotherOption() {
		var result = CreateParser().Parse(
			new string[ 2 ] { "--color", "--all" }
		);

		Assert.True( result.IsSuccess );
		Assert.Null( result.GetLastValue( "color" ) );
		Assert.True( result.HasOption( "all" ) );
	}

	[Fact]
	public void EmptyAttachedValueIsPreserved() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "--in-place=" }
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( string.Empty, result.GetLastValue( "suffix" ) );
	}

	[Fact]
	public void DoubleDashTerminatesOptionParsing() {
		var result = CreateParser().Parse(
			new string[ 3 ] { "--", "-a", "file" }
		);

		Assert.Empty( result.Options );
		Assert.Equal( new string[ 2 ] { "-a", "file" }, result.Operands );
	}

	[Fact]
	public void LoneDashIsAnOperand() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "-" }
		);

		Assert.Equal( new string[ 1 ] { "-" }, result.Operands );
	}

	[Fact]
	public void RequireOrderStopsAtFirstOperand() {
		var result = CreateParser().Parse(
			new string[ 3 ] { "file", "-a", "other" }
		);

		Assert.Empty( result.Options );
		Assert.Equal( new string[ 3 ] { "file", "-a", "other" }, result.Operands );
	}

	[Fact]
	public void PermuteRecognizesOptionsAfterOperands() {
		var settings = new OptionParserSettings {
			Ordering = OptionOrdering.Permute
		};
		var result = CreateParser( settings ).Parse(
			new string[ 3 ] { "file", "-a", "other" }
		);

		Assert.True( result.HasOption( "all" ) );
		Assert.Equal( new string[ 2 ] { "file", "other" }, result.Operands );
		Assert.Equal( OptionParseItemKind.Operand, result.Items[ 0 ].Kind );
		Assert.Equal( OptionParseItemKind.Option, result.Items[ 1 ].Kind );
	}

	[Fact]
	public void LongAliasesResolveToOneLogicalDefinition() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "--quiet" }
		);

		Assert.True( result.HasOption( "brief" ) );
	}

	[Fact]
	public void UniqueLongAbbreviationCanBeEnabled() {
		var settings = new OptionParserSettings {
			AllowLongOptionAbbreviations = true
		};
		var result = CreateParser( settings ).Parse(
			new string[ 1 ] { "--cou=9" }
		);

		Assert.True( result.IsSuccess );
		Assert.Equal( "9", result.GetLastValue( "count" ) );
	}

	[Fact]
	public void AmbiguousLongAbbreviationIsReported() {
		var parser = new OptionParser(
			new OptionDefinition[ 2 ] {
				new( "color", longNames: new string[ 1 ] { "color" } ),
				new( "columns", longNames: new string[ 1 ] { "columns" } )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true
			}
		);
		var result = parser.Parse(
			new string[ 1 ] { "--col" }
		);

		var error = Assert.Single( result.Errors );
		Assert.Equal( OptionParseErrorKind.AmbiguousLongOption, error.Kind );
		Assert.Equal( new string[ 2 ] { "color", "columns" }, error.Candidates );
	}

	[Fact]
	public void UnknownOptionsAreStructuredErrors() {
		var result = CreateParser().Parse(
			new string[ 2 ] { "-x", "--unknown" }
		);

		Assert.Collection(
			result.Errors,
			error => Assert.Equal( OptionParseErrorKind.UnknownShortOption, error.Kind ),
			error => Assert.Equal( OptionParseErrorKind.UnknownLongOption, error.Kind )
		);
	}

	[Fact]
	public void MissingRequiredValueIsReported() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "-n" }
		);

		Assert.Equal(
			OptionParseErrorKind.MissingOptionValue,
			Assert.Single( result.Errors ).Kind
		);
	}

	[Fact]
	public void UnexpectedLongValueIsReported() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "--all=yes" }
		);

		Assert.Equal(
			OptionParseErrorKind.UnexpectedOptionValue,
			Assert.Single( result.Errors ).Kind
		);
	}

	[Fact]
	public void NonRepeatableOptionReportsDuplicateButPreservesOccurrences() {
		var result = CreateParser().Parse(
			new string[ 2 ] { "-b", "--brief" }
		);

		Assert.Equal( 2, result.GetOccurrences( "brief" ).Count() );
		Assert.Equal(
			OptionParseErrorKind.DuplicateOption,
			Assert.Single( result.Errors ).Kind
		);
	}

	[Fact]
	public void LegacyRewriteRulesPreserveOriginalArgumentIndex() {
		var settings = new OptionParserSettings();
		settings.TokenRewriteRules.Add(
			new OptionTokenRewriteRule(
				token => token.Length > 1
					&& '-' == token[ 0 ]
					&& token.Substring( 1 ).All( char.IsDigit )
						? new string[ 2 ] { "-n", token.Substring( 1 ) }
						: null
			)
		);
		var result = CreateParser( settings ).Parse(
			new string[ 2 ] { "-25", "file" }
		);

		var occurrence = Assert.Single( result.Options );
		Assert.Equal( "25", occurrence.Value );
		Assert.Equal( 0, occurrence.ArgumentIndex );
		Assert.Equal( "-25", occurrence.OriginalToken );
		Assert.Equal( new string[ 1 ] { "file" }, result.Operands );
	}

	[Fact]
	public void NullArgumentListIsEmpty() {
		var result = CreateParser().Parse(
			null
		);

		Assert.True( result.IsSuccess );
		Assert.Empty( result.Options );
		Assert.Empty( result.Operands );
	}

	[Fact]
	public void DuplicateDefinitionsAreRejected() {
		Assert.Throws<ArgumentException>(
			() => new OptionParser(
				new OptionDefinition[ 2 ] {
					new( "one", 'a' ),
					new( "two", 'a' )
				}
			)
		);
	}

	[Fact]
	public void DiagnosticFormatterUsesProgramPrefix() {
		var result = CreateParser().Parse(
			new string[ 1 ] { "--unknown" }
		);
		var formatted = OptionDiagnosticFormatter.Format(
			"tool",
			Assert.Single( result.Errors )
		);

		Assert.StartsWith( "tool: ", formatted );
		Assert.Contains( "--unknown", formatted );
	}

	[Fact]
	public void PermuteModeNeverLosesOperands() {
		var settings = new OptionParserSettings {
			Ordering = OptionOrdering.ReturnInOrder
		};
		var parser = CreateParser( settings );
		var random = new Random( 1701 );
		for ( var iteration = 0; iteration < 100; iteration++ ) {
			var arguments = new List<string>();
			var expectedOperands = new List<string>();
			for ( var index = 0; index < 30; index++ ) {
				if ( 0 == random.Next( 3 ) ) {
					arguments.Add( "-a" );
				} else {
					var operand = $"operand-{iteration}-{index}";
					arguments.Add( operand );
					expectedOperands.Add( operand );
				}
			}

			var result = parser.Parse( arguments );

			Assert.True( result.IsSuccess );
			Assert.Equal( expectedOperands, result.Operands );
		}
	}

}
