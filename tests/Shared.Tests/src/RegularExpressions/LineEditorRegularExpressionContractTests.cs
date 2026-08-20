namespace Icod.CoreUtils.Shared.Tests;

using System.Text;

using Icod.CoreUtils.Shared.RegularExpressions;
using Icod.CommandFramework.Text;

using Xunit;

/// <summary>
/// Verifies the Completion Gate R1 contracts required by GNU Sed and GNU Ed
/// before LineEditor consumers migrate to the Shared regular-expression provider.
/// </summary>
public sealed class LineEditorRegularExpressionContractTests {
	private static readonly GnuBasicRegularExpressionProvider Basic = new(
		PosixCLocaleRegularExpressionCharacterClassProvider.Instance
	);
	private static readonly GnuExtendedRegularExpressionProvider Extended = new(
		PosixCLocaleRegularExpressionCharacterClassProvider.Instance
	);

	[Fact]
	public void BasicRemainsTheDefaultWhileExtendedUsesItsOwnOperatorProfile() {
		var defaults = new RegularExpressionOptions();
		Assert.Equal( GnuRegularExpressionSyntax.Basic, defaults.Syntax );
		Assert.Equal( new Rune( '\n' ), defaults.LineSeparator );
		Assert.False( defaults.DotMatchesNull );
		Assert.Equal(
			GnuRegularExpressionSyntax.Extended,
			RegularExpressionOptions.GnuExtendedCompatibility.Syntax
		);

		var basic = Compile( Basic, "a|aa" ).Match( "aa" );
		Assert.False( basic.IsMatch );

		var extended = Compile( Extended, "a|aa" ).Match( "aa" );
		Assert.True( extended.IsMatch );
		Assert.Equal( "aa", extended.Match!.Value );
	}

	[Fact]
	public void ExtendedGroupingAlternationIntervalsBracketsAndCapturesCompose() {
		var expression = Compile( Extended, "^(a|aa)([[:digit:]]{2})$" );
		var result = expression.Match( "aa42" );

		Assert.True( result.IsSuccess );
		Assert.True( result.IsMatch );
		Assert.Equal( "aa42", result.Match!.Value );
		Assert.Equal( "aa", result.Match.Captures[ 0 ].Value );
		Assert.Equal( "42", result.Match.Captures[ 1 ].Value );
	}

	[Fact]
	public void ExtendedSelectionIsLeftmostLongest() {
		var result = Compile( Extended, "a|aa" ).Match( "zaa aa" );

		Assert.True( result.IsMatch );
		Assert.Equal( 1, result.Match!.Index );
		Assert.Equal( "aa", result.Match.Value );
	}

	[Fact]
	public void LineSensitiveAnchorsCanSelectAnEmbeddedLogicalLine() {
		var expression = Compile(
			Extended,
			"^b$",
			new RegularExpressionOptions { NewLineSensitive = true }
		);
		var result = expression.Match( "a\nb\nc" );

		Assert.True( result.IsMatch );
		Assert.Equal( "b", result.Match!.Value );
	}

	[Fact]
	public void ConsumerSelectedLineSeparatorControlsMultilineAndDotPolicy() {
		var nullLineOptions = new RegularExpressionOptions {
			NewLineSensitive = true,
			LineSeparator = new Rune( '\0' ),
			DotMatchesNull = true
		};
		var anchored = Compile( Extended, "^b$", nullLineOptions );

		var nullSeparated = anchored.Match( "a\0b\0c" );
		Assert.True( nullSeparated.IsMatch );
		Assert.Equal( "b", nullSeparated.Match!.Value );
		Assert.False( anchored.Match( "a\nb\nc" ).IsMatch );

		var ordinaryDot = Compile(
			Extended,
			".",
			new RegularExpressionOptions {
				LineSeparator = new Rune( '\0' ),
				DotMatchesNull = true
			}
		);
		Assert.True( ordinaryDot.Match( "\0" ).IsMatch );

		var multilineDot = Compile( Extended, ".", nullLineOptions );
		Assert.False( multilineDot.Match( "\0" ).IsMatch );

		var negated = Compile( Extended, "[^a]", nullLineOptions );
		Assert.False( negated.Match( "\0" ).IsMatch );
	}

	[Fact]
	public void LocaleClassificationRemainsAnInjectedPolicy() {
		var cLocale = Compile( Extended, "[[:alpha:]]+" );
		Assert.False( cLocale.Match( "é" ).IsMatch );

		var unicodeProvider = new GnuExtendedRegularExpressionProvider(
			UnicodeRegularExpressionCharacterClassProvider.InvariantCulture
		);
		var unicode = Compile( unicodeProvider, "[[:alpha:]]+" );
		Assert.Equal( "é", unicode.Match( "é" ).Match!.Value );
	}

	[Fact]
	public void StringAndUtf8ByteSurfacesReturnAuthoritativeCoordinates() {
		var expression = Compile( Extended, "(.)" );

		var textResult = expression.Match( "x😀y", new() { StartIndex = 1 } );
		Assert.True( textResult.IsMatch );
		Assert.Equal( 1, textResult.Match!.Index );
		Assert.Equal( 2, textResult.Match.Length );
		Assert.Equal( "😀", textResult.Match.Value );
		Assert.Equal( 1, textResult.Match.Captures[ 0 ].Index );
		Assert.Equal( 2, textResult.Match.Captures[ 0 ].Length );

		var bytes = Encoding.UTF8.GetBytes( "x😀y" );
		var byteResult = expression.Match(
			bytes,
			new RegularExpressionInputOptions { DecodingMode = TextDecodingMode.Utf8 },
			new RegularExpressionByteMatchOptions { StartByteOffset = 1 }
		);
		Assert.True( byteResult.IsMatch );
		Assert.Equal( 1, byteResult.Match!.ByteIndex );
		Assert.Equal( 4, byteResult.Match.ByteLength );
		Assert.Equal( Encoding.UTF8.GetBytes( "😀" ), byteResult.Match.Value.ToArray() );
		Assert.Equal( 1, byteResult.Match.Captures[ 0 ].ByteIndex );
		Assert.Equal( 4, byteResult.Match.Captures[ 0 ].ByteLength );
		Assert.Equal(
			Encoding.UTF8.GetBytes( "😀" ),
			byteResult.Match.Captures[ 0 ].Value.ToArray()
		);
	}

	[Fact]
	public void Utf8StartOffsetCannotSplitASourceScalar() {
		var expression = Compile( Extended, "." );
		var bytes = Encoding.UTF8.GetBytes( "x😀y" );
		var result = expression.Match(
			bytes,
			new RegularExpressionInputOptions { DecodingMode = TextDecodingMode.Utf8 },
			new RegularExpressionByteMatchOptions { StartByteOffset = 2 }
		);

		Assert.False( result.IsSuccess );
		Assert.NotNull( result.Diagnostic );
	}

	[Fact]
	public void MalformedUtf8CanRemainAnExactAuthoritativeByte() {
		var expression = Compile( Extended, "." );
		var input = new byte[] { 0xff };
		var result = expression.Match(
			input,
			new RegularExpressionInputOptions {
				DecodingMode = TextDecodingMode.Utf8,
				InvalidEncodingPolicy = InvalidEncodingPolicy.PreserveBytes
			}
		);

		Assert.True( result.IsMatch );
		Assert.Equal( 0, result.Match!.ByteIndex );
		Assert.Equal( 1, result.Match.ByteLength );
		Assert.Equal( input, result.Match.Value.ToArray() );
	}

	[Fact]
	public void ExtendedSyntaxFailuresReturnStableDiagnostics() {
		var result = Extended.Compile( "(abc" );

		Assert.False( result.IsSuccess );
		Assert.Equal(
			RegularExpressionDiagnosticCode.UnterminatedSubexpression,
			result.Diagnostic!.Code
		);
		Assert.NotNull( result.Diagnostic.PatternIndex );
	}

	[Fact]
	public void MatchStateLimitReturnsAControlledFailure() {
		var expression = Compile(
			Extended,
			"(a|aa)*b",
			new RegularExpressionOptions { MaximumMatchStates = 10 }
		);
		var result = expression.Match( "aaaaaaaaaaaaaaaa" );

		Assert.False( result.IsSuccess );
		Assert.Equal(
			RegularExpressionDiagnosticCode.MatchResourceLimitExceeded,
			result.Diagnostic!.Code
		);
	}

	[Fact]
	public async Task CompileAndMatchHonorCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			_ = await Extended.CompileAsync(
				"a+",
				cancellationToken: cancellation.Token
			);
		} );

		var expression = Compile( Extended, "a+" );
		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			_ = await expression.MatchAsync(
				"aaaa",
				cancellationToken: cancellation.Token
			);
		} );
	}

	private static ICompiledRegularExpression Compile(
		IRegularExpressionProvider provider,
		string pattern,
		RegularExpressionOptions? options = null
	) {
		var result = provider.Compile( pattern, options );
		Assert.True( result.IsSuccess, result.Diagnostic?.Message );
		return result.Expression!;
	}
}
