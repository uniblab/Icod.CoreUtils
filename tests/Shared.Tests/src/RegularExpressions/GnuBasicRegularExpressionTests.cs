namespace Icod.CoreUtils.Shared.Tests;

using System.Text;

using Icod.CoreUtils.Shared.RegularExpressions;

using Xunit;

public sealed class GnuBasicRegularExpressionTests {
	private static readonly GnuBasicRegularExpressionProvider Provider = new(
		UnicodeRegularExpressionCharacterClassProvider.InvariantCulture
	);

	[Fact]
	public void SearchSelectsTheLongestMatchAtTheLeftmostPosition() {
		var expression = Compile( @"a\|aa" );
		var result = expression.Match( "zaa aa" );
		Assert.True( result.IsSuccess );
		Assert.True( result.IsMatch );
		Assert.Equal( 1, result.Match!.Index );
		Assert.Equal( "aa", result.Match!.Value );
	}

	[Fact]
	public void SuccessfulNoMatchIsDistinctFromAnEngineFailure() {
		var result = Compile( "z" ).Match( "abc" );
		Assert.True( result.IsSuccess );
		Assert.False( result.IsMatch );
		Assert.Null( result.Match );
		Assert.Null( result.Diagnostic );
	}

	[Fact]
	public void OverallLongestMatchConstrainsSubexpressionSelection() {
		var expression = Compile( @"\(ac*\)\(c*d[ac]*\)\1" );
		var result = expression.Match( "acdacaaa", new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( "acdacaaa", result.Match!.Value );
		Assert.Equal( "a", result.Match!.Captures[ 0 ].Value );
	}

	[Fact]
	public void RepeatedSubexpressionRetainsItsLastParticipatingCapture() {
		var expression = Compile( @"^\(ab*\)*\1$" );
		var result = expression.Match( "ababbabb" );
		Assert.True( result.IsMatch );
		Assert.Equal( "abb", result.Match!.Captures[ 0 ].Value );
	}

	[Theory]
	[InlineData( @"\(a\|aa\)*", "aa", "a", 1 )]
	[InlineData( @"\(aa\|a\)*", "aa", "aa", 0 )]
	[InlineData( @"\(a*\)*", "aaa", "aaa", 0 )]
	public void EqualLengthMatchesFollowGnuGreedyCaptureOrdering(
		string pattern,
		string input,
		string expectedCapture,
		int expectedCaptureIndex
	) {
		var result = Compile( pattern ).Match( input, new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( input, result.Match!.Value );
		Assert.Equal( expectedCapture, result.Match!.Captures[ 0 ].Value );
		Assert.Equal( expectedCaptureIndex, result.Match!.Captures[ 0 ].Index );
	}

	[Fact]
	public void NestedCapturesRetainTheirLastSuccessfulGnuRegisterValues() {
		var result = Compile( @"\(ba\(na\)*s \|nefer\(ti\)* \)*" ).Match(
			"bananas nefertiti ",
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( "nefertiti ", result.Match!.Captures[ 0 ].Value );
		Assert.Equal( "na", result.Match!.Captures[ 1 ].Value );
		Assert.Equal( "ti", result.Match!.Captures[ 2 ].Value );
	}

	[Fact]
	public void EmptyAndNonparticipatingCapturesRemainDistinct() {
		var absent = Compile( @"\(a\)*" ).Match( "", new() { RequireMatchAtStart = true } );
		Assert.True( absent.IsMatch );
		Assert.False( absent.Match!.Captures[ 0 ].Success );

		var empty = Compile( @"\(\)*" ).Match( "", new() { RequireMatchAtStart = true } );
		Assert.True( empty.IsMatch );
		Assert.True( empty.Match!.Captures[ 0 ].Success );
		Assert.Equal( String.Empty, empty.Match!.Captures[ 0 ].Value );
	}

	[Theory]
	[InlineData( @"ab*", "abbb", "abbb" )]
	[InlineData( @"ab\+", "abbb", "abbb" )]
	[InlineData( @"ab\?", "abbb", "ab" )]
	[InlineData( @"ab\{2,3\}", "abbb", "abbb" )]
	[InlineData( @"ab\{2,\}", "abbbb", "abbbb" )]
	[InlineData( @"ab\{2\}", "abbb", "abb" )]
	[InlineData( @"ab\{,2\}", "abbb", "abb" )]
	[InlineData( @"ab\{,\}", "abbb", "abbb" )]
	[InlineData( @"ab\{0\}", "abbb", "a" )]
	public void RepetitionOperatorsUseGnuBasicSyntax( string pattern, string input, string expected ) {
		var result = Compile( pattern ).Match( input, new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	[Fact]
	public void IntervalBoundsPermitArbitrarilyManyLeadingZeros() {
		var expression = Compile( @"a\{00000000000000000000000000000000000000001\}" );
		var result = expression.Match( "a", new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( "a", result.Match!.Value );
	}

	[Fact]
	public void IntervalBoundsAboveTheGnuLimitAreRejected() {
		var result = Provider.Compile( @"a\{32768\}" );
		Assert.False( result.IsSuccess );
		Assert.Equal( RegularExpressionDiagnosticCode.InvalidInterval, result.Diagnostic!.Code );
	}

	[Fact]
	public void ZeroLengthMatchIsASuccessfulMatch() {
		var result = Compile( "a*" ).Match( "bbb" );
		Assert.True( result.IsSuccess );
		Assert.True( result.IsMatch );
		Assert.Equal( 0, result.Match!.Length );
	}

	[Fact]
	public void AnchorsHonorNewLineSensitivePolicy() {
		var expression = Compile(
			"^b$",
			new RegularExpressionOptions { NewLineSensitive = true }
		);
		var result = expression.Match( "a\nb\nc" );
		Assert.True( result.IsMatch );
		Assert.Equal( "b", result.Match!.Value );
	}


	[Fact]
	public void DotMatchesNewlineButNeverNullByDefault() {
		Assert.True( Compile( "." ).Match( "\n" ).IsMatch );
		Assert.False( Compile( "." ).Match( "\0" ).IsMatch );
	}

	[Fact]
	public void DotAndNegatedBracketExcludeNewlineWhenRequested() {
		var options = new RegularExpressionOptions { NewLineSensitive = true };
		Assert.False( Compile( ".", options ).Match( "\n" ).IsMatch );
		Assert.False( Compile( "[^a]", options ).Match( "\n" ).IsMatch );
	}

	[Fact]
	public void GnuClassComplementsRemainIndependentOfNegatedBracketNewlinePolicy() {
		var options = new RegularExpressionOptions { NewLineSensitive = true };
		Assert.True( Compile( @"\W", options ).Match( "\n" ).IsMatch );
		Assert.True( Compile( @"\s", options ).Match( "\n" ).IsMatch );
		Assert.False( Compile( @"\S", options ).Match( "\n" ).IsMatch );
	}

	[Theory]
	[InlineData( @"\<cat\>", "a cat!", "cat" )]
	[InlineData( @"\bcat\b", "a cat!", "cat" )]
	[InlineData( @"\`cat\'", "cat", "cat" )]
	[InlineData( @"\w\+", "--abc_12--", "abc_12" )]
	[InlineData( @"\s\+", "x \t y", " \t " )]
	[InlineData( @"\S\+", "  abc-12 ", "abc-12" )]
	public void GnuWordAndInputAssertionsAreSupported( string pattern, string input, string expected ) {
		var result = Compile( pattern ).Match( input );
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	[Fact]
	public void PosixClassesRangesAndEquivalenceExpressionsAreSupported() {
		Assert.Equal( "Az9", Compile( "[[:alnum:]]*" ).Match( "Az9!" ).Match!.Value );
		Assert.Equal( "b", Compile( "[a-c]" ).Match( "b" ).Match!.Value );
		Assert.Equal( "A", Compile( "[[=a=]]", new() { IgnoreCase = true } ).Match( "A" ).Match!.Value );
		Assert.Equal( "x", Compile( "[[.x.]]" ).Match( "x" ).Match!.Value );
	}


	[Fact]
	public void EmptyRangeCompatibilityCanBeEnabledForExpr() {
		var options = new RegularExpressionOptions { AllowEmptyRanges = true };
		Assert.False( Compile( "[z-a]", options ).Match( "a" ).IsMatch );
		Assert.True( Compile( "[^z-a]", options ).Match( "a" ).IsMatch );
	}

	[Fact]
	public void IgnoreCaseAppliesToCaseCharacterClasses() {
		var expression = Compile(
			@"[[:upper:]]\+",
			new RegularExpressionOptions { IgnoreCase = true }
		);
		var result = expression.Match( "abc" );
		Assert.True( result.IsMatch );
		Assert.Equal( "abc", result.Match!.Value );
	}

	[Fact]
	public void IgnoreCaseAppliesToLiteralsAndBackReferences() {
		var expression = Compile(
			@"\(ab\)\1",
			new RegularExpressionOptions { IgnoreCase = true }
		);
		var result = expression.Match( "abAB" );
		Assert.True( result.IsMatch );
		Assert.Equal( "abAB", result.Match!.Value );
	}

	[Fact]
	public void MatchIndicesRemainUtf16IndicesWhileMatchingUnicodeScalars() {
		var result = Compile( "." ).Match( "x😀y", new() { StartIndex = 1 } );
		Assert.True( result.IsMatch );
		Assert.Equal( 1, result.Match!.Index );
		Assert.Equal( 2, result.Match!.Length );
		Assert.Equal( "😀", result.Match!.Value );
	}

	[Fact]
	public void StartIndexCannotSplitASurrogatePair() {
		var result = Compile( "." ).Match( "😀", new() { StartIndex = 1 } );
		Assert.False( result.IsSuccess );
		Assert.Equal( RegularExpressionDiagnosticCode.InvalidStartIndex, result.Diagnostic!.Code );
	}

	[Theory]
	[InlineData( @"\+", "+", "+" )]
	[InlineData( @"\?", "?", "?" )]
	[InlineData( "**", "***", "***" )]
	[InlineData( "^*", "*", "*" )]
	public void ContextuallyInvalidRepetitionTokensAreOrdinary(
		string pattern,
		string input,
		string expected
	) {
		var result = Compile( pattern ).Match( input, new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	[Theory]
	[InlineData( @"a*\+" )]
	[InlineData( @"a\+\+" )]
	[InlineData( @"a\{1\}\+" )]
	[InlineData( @"a*\?" )]
	[InlineData( @"a\+\?" )]
	[InlineData( @"a\?\+" )]
	public void StrictBasicSyntaxAcceptsGnuValidAdjacentRepetitions( string pattern ) {
		var result = Compile( pattern ).Match( "aaaa", new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( "aaaa", result.Match!.Value );
	}

	[Theory]
	[InlineData( @"\{1\}" )]
	[InlineData( "a**" )]
	[InlineData( @"a\{1\}*" )]
	[InlineData( @"a\{1\}\{2\}" )]
	public void StrictBasicSyntaxRejectsInvalidRepetitionContexts( string pattern ) {
		var result = Provider.Compile( pattern );
		Assert.False( result.IsSuccess );
		Assert.Equal(
			RegularExpressionDiagnosticCode.InvalidRepetitionOperator,
			result.Diagnostic!.Code
		);
	}

	[Theory]
	[InlineData( @"\{1\}", "{1}", "{1}" )]
	[InlineData( "a**", "aaaa", "aaaa" )]
	[InlineData( @"a\?\{2\}", "aaaa", "aa" )]
	[InlineData( @"a\{2\}\{2\}", "aaaaa", "aaaa" )]
	public void ExprCompatibilityAcceptsGnulibInvalidDuplicateProfile(
		string pattern,
		string input,
		string expected
	) {
		var options = RegularExpressionOptions.GnuExprCompatibility;
		var result = Compile( pattern, options ).Match(
			input,
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	[Fact]
	public void FiniteRepetitionOfAnEmptySubexpressionHonorsTheMinimum() {
		var result = Compile( @"\(\)\{2\}" ).Match( "value", new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( 0, result.Match!.Length );
		Assert.True( result.Match!.Captures[ 0 ].Success );
	}

	[Theory]
	[InlineData( @"abc\", RegularExpressionDiagnosticCode.TrailingEscape )]
	[InlineData( "[abc", RegularExpressionDiagnosticCode.UnterminatedBracketExpression )]
	[InlineData( "[[:alpha]", RegularExpressionDiagnosticCode.UnterminatedBracketExpression )]
	[InlineData( "[[.x]", RegularExpressionDiagnosticCode.UnterminatedBracketExpression )]
	[InlineData( @"\(abc", RegularExpressionDiagnosticCode.UnterminatedSubexpression )]
	[InlineData( @"\)", RegularExpressionDiagnosticCode.UnmatchedClosingSubexpression )]
	[InlineData( @"\1", RegularExpressionDiagnosticCode.InvalidBackReference )]
	[InlineData( @"a\{3,2\}", RegularExpressionDiagnosticCode.InvalidInterval )]
	[InlineData( "[z-a]", RegularExpressionDiagnosticCode.InvalidRange )]
	[InlineData( "[a-z-a]", RegularExpressionDiagnosticCode.InvalidRange )]
	[InlineData( "[[:unknown:]]", RegularExpressionDiagnosticCode.InvalidCharacterClass )]
	[InlineData( "[[:DIGIT:]]", RegularExpressionDiagnosticCode.InvalidCharacterClass )]
	[InlineData( "[[:word:]]", RegularExpressionDiagnosticCode.InvalidCharacterClass )]
	[InlineData( "[[.ch.]]", RegularExpressionDiagnosticCode.UnsupportedCollatingElement )]
	public void InvalidPatternsProduceStableDiagnostics(
		string pattern,
		RegularExpressionDiagnosticCode expected
	) {
		var result = Provider.Compile( pattern );
		Assert.False( result.IsSuccess );
		Assert.Equal( expected, result.Diagnostic!.Code );
		Assert.NotNull( result.Diagnostic!.PatternIndex );
	}

	[Fact]
	public void ConfiguredNestingLimitProducesAControlledDiagnostic() {
		var result = Provider.Compile(
			@"\(\(a\)\)",
			new RegularExpressionOptions { MaximumNestingDepth = 1 }
		);
		Assert.False( result.IsSuccess );
		Assert.Equal( RegularExpressionDiagnosticCode.NestingDepthExceeded, result.Diagnostic!.Code );
	}

	[Fact]
	public void ConfiguredNestingLimitAlsoAppliesToAdjacentRepetitions() {
		var result = Provider.Compile(
			@"a\+\+",
			new RegularExpressionOptions { MaximumNestingDepth = 1 }
		);
		Assert.False( result.IsSuccess );
		Assert.Equal( RegularExpressionDiagnosticCode.NestingDepthExceeded, result.Diagnostic!.Code );
	}

	[Fact]
	public void ConfiguredMatchStateLimitProducesAControlledFailure() {
		var expression = Compile(
			@"\(a\|aa\)*b",
			new RegularExpressionOptions { MaximumMatchStates = 10 }
		);
		var result = expression.Match( "aaaaaaaaaaaaaaaa" );
		Assert.False( result.IsSuccess );
		Assert.Equal( RegularExpressionDiagnosticCode.MatchResourceLimitExceeded, result.Diagnostic!.Code );
	}

	[Fact]
	public async Task AsyncCompileAndMatchUseTheSameSemantics() {
		var compile = await Provider.CompileAsync( @"a\|aa" );
		Assert.True( compile.IsSuccess );
		var match = await compile.Expression!.MatchAsync( "aa" );
		Assert.True( match.IsMatch );
		Assert.Equal( "aa", match.Match!.Value );
	}

	[Fact]
	public async Task CompileAndMatchHonorCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			_ = await Provider.CompileAsync( "a", cancellationToken: cancellation.Token );
		} );
		var expression = Compile( "a*" );
		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			_ = await expression.MatchAsync( "aaaa", cancellationToken: cancellation.Token );
		} );
	}

	[Fact]
	public void MatchHonorsCancellationDuringEvaluation() {
		using var cancellation = new CancellationTokenSource();
		var provider = new GnuBasicRegularExpressionProvider(
			new CancellingCharacterProvider( cancellation, 32 )
		);
		var compile = provider.Compile( "a*" );
		Assert.True( compile.IsSuccess );
		Assert.ThrowsAny<OperationCanceledException>( () => compile.Expression!.Match(
			new String( 'a', 1024 ),
			cancellationToken: cancellation.Token
		) );
	}

	[Fact]
	public void PosixCLocaleProviderUsesAsciiClassificationAndOrdinalCollation() {
		var provider = new GnuBasicRegularExpressionProvider(
			PosixCLocaleRegularExpressionCharacterClassProvider.Instance
		);
		var compile = provider.Compile( @"[[:alpha:]]\+" );
		Assert.True( compile.IsSuccess );
		Assert.False( compile.Expression!.Match( "é" ).IsMatch );
		Assert.Equal(
			"Az",
			compile.Expression!.Match( "Az9" ).Match!.Value
		);
	}

	[Fact]
	public void CharacterClassesAreProviderDrivenAndInjectable() {
		var provider = new GnuBasicRegularExpressionProvider( new VowelCharacterProvider() );
		var compile = provider.Compile( @"[[:vowel:]]\+" );
		Assert.True( compile.IsSuccess );
		var result = compile.Expression!.Match( "rhythm aeon" );
		Assert.True( result.IsMatch );
		Assert.Equal( "aeo", result.Match!.Value );

		var wordCompile = provider.Compile( @"\w\+" );
		Assert.True( wordCompile.IsSuccess );
		Assert.Equal( "rhythm", wordCompile.Expression!.Match( "rhythm aeon" ).Match!.Value );
	}

	private static ICompiledRegularExpression Compile(
		string pattern,
		RegularExpressionOptions? options = null
	) {
		var result = Provider.Compile( pattern, options );
		Assert.True( result.IsSuccess, result.Diagnostic?.Message );
		return result.Expression!;
	}

	private sealed class CancellingCharacterProvider(
		CancellationTokenSource cancellation,
		int cancelAfterComparisons
	) : IRegularExpressionCharacterClassProvider {
		private readonly IRegularExpressionCharacterClassProvider inner =
			PosixCLocaleRegularExpressionCharacterClassProvider.Instance;
		private int comparisons;

		public bool IsSupportedClass( string className ) => inner.IsSupportedClass( className );

		public bool IsCharacterClass( Rune value, string className, bool ignoreCase ) =>
			inner.IsCharacterClass( value, className, ignoreCase );

		public bool IsWordCharacter( Rune value ) => inner.IsWordCharacter( value );

		public int Compare( Rune left, Rune right, bool ignoreCase ) =>
			inner.Compare( left, right, ignoreCase );

		public bool AreCharactersEqual( Rune left, Rune right, bool ignoreCase ) {
			comparisons++;
			if ( cancelAfterComparisons == comparisons ) {
				cancellation.Cancel();
			}
			return inner.AreCharactersEqual( left, right, ignoreCase );
		}

		public bool AreCollatingElementsEquivalent( Rune left, Rune right, bool ignoreCase ) =>
			inner.AreCollatingElementsEquivalent( left, right, ignoreCase );
	}

	private sealed class VowelCharacterProvider : IRegularExpressionCharacterClassProvider {
		public bool IsSupportedClass( string className ) => "vowel" == className;

		public bool IsCharacterClass( Rune value, string className, bool ignoreCase ) =>
			"vowel" == className && "aeiouAEIOU".Contains( (char)value.Value );

		public bool IsWordCharacter( Rune value ) => Rune.IsLetterOrDigit( value ) || '_' == value.Value;

		public int Compare( Rune left, Rune right, bool ignoreCase ) {
			var leftValue = ignoreCase ? Rune.ToUpperInvariant( left ) : left;
			var rightValue = ignoreCase ? Rune.ToUpperInvariant( right ) : right;
			return leftValue.Value.CompareTo( rightValue.Value );
		}

		public bool AreCharactersEqual( Rune left, Rune right, bool ignoreCase ) => 0 == Compare( left, right, ignoreCase );

		public bool AreCollatingElementsEquivalent( Rune left, Rune right, bool ignoreCase ) =>
			0 == Compare( left, right, ignoreCase );
	}
}
