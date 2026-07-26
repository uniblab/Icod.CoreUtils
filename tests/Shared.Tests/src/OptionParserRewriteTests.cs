namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.CommandLine;
using Xunit;

public sealed class OptionParserRewriteTests {

	[Fact]
	public void RewriteRulesApplyToCurrentOptions() {
		var parser = CreateParser();

		var result = parser.Parse(
			new string[] { "-25", "file" }
		);

		Assert.True( result.IsSuccess );
		var occurrence = Assert.Single( result.Options );
		Assert.Equal( "25", occurrence.Value );
		Assert.Equal( "-25", occurrence.OriginalToken );
		Assert.Equal( new string[] { "file" }, result.Operands );
	}

	[Fact]
	public void RewriteRulesDoNotAlterRequiredOptionValues() {
		var parser = CreateParser();

		var result = parser.Parse(
			new string[] { "-n", "-25", "file" }
		);

		Assert.True( result.IsSuccess );
		var occurrence = Assert.Single( result.Options );
		Assert.Equal( "-25", occurrence.Value );
		Assert.Equal( new string[] { "file" }, result.Operands );
	}

	[Fact]
	public void RewriteRulesDoNotApplyAfterOptionTerminator() {
		var parser = CreateParser();

		var result = parser.Parse(
			new string[] { "--", "-25" }
		);

		Assert.True( result.IsSuccess );
		Assert.Empty( result.Options );
		Assert.Equal( new string[] { "-25" }, result.Operands );
	}

	private static OptionParser CreateParser() {
		var settings = new OptionParserSettings {
			Ordering = OptionOrdering.Permute
		};
		settings.TokenRewriteRules.Add(
			new OptionTokenRewriteRule(
				token => (
					1 < token.Length
					&& '-' == token[ 0 ]
					&& token.Substring( 1 ).All( char.IsDigit )
				)
					? new string[] { "-n", token.Substring( 1 ) }
					: null
			)
		);
		return new OptionParser(
			new OptionDefinition[] {
				new(
					"lines",
					'n',
					new string[] { "lines" },
					OptionValueArity.Required
				)
			},
			settings
		);
	}

}
