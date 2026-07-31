namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.RegularExpressions;
using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Exercises the managed GNU/POSIX extended regular-expression profile and byte mapping contract.</summary>
public sealed class GnuExtendedRegularExpressionTests {
	private static readonly GnuExtendedRegularExpressionProvider Provider = new(
		PosixCLocaleRegularExpressionCharacterClassProvider.Instance
	);

	/// <summary>Verifies the source-compatible options default remains Basic.</summary>
	[Fact]
	public void RegularExpressionOptionsDefaultToBasicSyntax() {
		Assert.Equal( GnuRegularExpressionSyntax.Basic, new RegularExpressionOptions().Syntax );
		Assert.Equal( 1, (int)GnuRegularExpressionSyntax.Emacs );
	}

	/// <summary>Verifies Extended uses unescaped grouping, alternation, repetition, and intervals.</summary>
	/// <param name="pattern">The extended pattern.</param>
	/// <param name="input">The input text.</param>
	/// <param name="expected">The expected leftmost-longest match.</param>
	[Theory]
	[InlineData( "(ab|cd)+", "abcdcd!", "abcdcd" )]
	[InlineData( "ab?", "abbb", "ab" )]
	[InlineData( "a{2,3}", "aaaa", "aaa" )]
	[InlineData( "a{,2}", "aaa", "aa" )]
	public void ExtendedOperatorsAreUnescaped(
		string pattern,
		string input,
		string expected
	) {
		var result = Compile( pattern ).Match(
			input,
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	/// <summary>Verifies escaped ERE metacharacters are matched literally.</summary>
	/// <param name="pattern">The extended pattern.</param>
	/// <param name="input">The input text.</param>
	[Theory]
	[InlineData( @"\(", "(" )]
	[InlineData( @"\)", ")" )]
	[InlineData( @"\|", "|" )]
	[InlineData( @"\+", "+" )]
	[InlineData( @"\?", "?" )]
	[InlineData( @"\{", "{" )]
	public void EscapedExtendedMetacharactersAreLiterals( string pattern, string input ) {
		var result = Compile( pattern ).Match(
			input,
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( input, result.Match!.Value );
	}

	/// <summary>Verifies a closing parenthesis is ordinary outside an ERE subexpression.</summary>
	[Fact]
	public void UnmatchedExtendedClosingParenthesisIsLiteral() {
		var result = Compile( ")" ).Match(
			")",
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( ")", result.Match!.Value );
	}

	/// <summary>Verifies captures and GNU back-references use the same managed matcher.</summary>
	[Fact]
	public void CapturesAndBackReferencesUseExtendedGrouping() {
		var result = Compile( "(a|ab)\\1" ).Match(
			"abab",
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( "abab", result.Match!.Value );
		Assert.Equal( "ab", result.Match.Captures[ 0 ].Value );
	}

	/// <summary>Verifies leftmost-longest selection is retained for ERE alternation.</summary>
	[Fact]
	public void AlternationUsesLeftmostLongestSelection() {
		var result = Compile( "a|aa" ).Match( "zaa" );
		Assert.True( result.IsMatch );
		Assert.Equal( 1, result.Match!.Index );
		Assert.Equal( "aa", result.Match.Value );
	}

	/// <summary>Verifies locale character classes remain provider-driven in ERE.</summary>
	[Fact]
	public void CharacterClassesUseTheInjectedLocaleProvider() {
		var expression = Compile( "[[:alpha:]]+" );
		Assert.Equal( "Az", expression.Match( "Az9" ).Match!.Value );
		Assert.False( expression.Match( "é" ).IsMatch );
	}

	/// <summary>Verifies strict ERE produces stable diagnostics for recognized invalid operators.</summary>
	/// <param name="pattern">The invalid pattern.</param>
	/// <param name="expected">The expected diagnostic.</param>
	[Theory]
	[InlineData( "(", RegularExpressionDiagnosticCode.UnterminatedSubexpression )]
	[InlineData( "*a", RegularExpressionDiagnosticCode.InvalidRepetitionOperator )]
	[InlineData( "a+?", RegularExpressionDiagnosticCode.InvalidRepetitionOperator )]
	[InlineData( "a{3,2}", RegularExpressionDiagnosticCode.InvalidInterval )]
	[InlineData( "a{2,3,}", RegularExpressionDiagnosticCode.InvalidInterval )]
	[InlineData( "[z-a]", RegularExpressionDiagnosticCode.InvalidRange )]
	public void InvalidExtendedPatternsProduceStableDiagnostics(
		string pattern,
		RegularExpressionDiagnosticCode expected
	) {
		var result = Provider.Compile( pattern );
		Assert.False( result.IsSuccess );
		Assert.Equal( expected, result.Diagnostic!.Code );
	}

	/// <summary>Verifies GNU-compatible malformed brace text remains literal while recognized intervals remain validated.</summary>
	[Theory]
	[InlineData( "a{", "a{" )]
	[InlineData( "a{2", "a{2" )]
	[InlineData( "a{word}", "a{word}" )]
	[InlineData( "a{2x}", "a{2x}" )]
	public void MalformedExtendedBraceTextRemainsLiteral( string pattern, string input ) {
		var result = Compile( pattern ).Match(
			input,
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( input, result.Match!.Value );
	}

	/// <summary>Verifies the GNU permissive profile accepts leading and adjacent duplicate operators.</summary>
	[Fact]
	public void GnuExtendedCompatibilityAcceptsInvalidDuplicateContexts() {
		var leading = Compile( "*a", RegularExpressionOptions.GnuExtendedCompatibility );
		Assert.Equal( "a", leading.Match( "*a" ).Match!.Value );

		var leadingInterval = Compile( "{2}a", RegularExpressionOptions.GnuExtendedCompatibility );
		Assert.Equal( "2}a", leadingInterval.Match( "{2}a" ).Match!.Value );

		var adjacent = Compile( "a**", RegularExpressionOptions.GnuExtendedCompatibility );
		Assert.Equal( "aaaa", adjacent.Match( "aaaa" ).Match!.Value );

		var mixedAdjacent = Compile( "a+?", RegularExpressionOptions.GnuExtendedCompatibility );
		Assert.Equal( "aaaa", mixedAdjacent.Match( "aaaa" ).Match!.Value );

		var invalidRange = Provider.Compile(
			"[z-a]",
			RegularExpressionOptions.GnuExtendedCompatibility
		);
		Assert.False( invalidRange.IsSuccess );
		Assert.Equal( RegularExpressionDiagnosticCode.InvalidRange, invalidRange.Diagnostic!.Code );
	}

	/// <summary>Verifies ERE compilation and matching honor cancellation and match-state limits.</summary>
	[Fact]
	public async Task ExtendedCompilationAndMatchingHonorOperationalControls() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => {
			_ = await Provider.CompileAsync( "a", cancellationToken: cancellation.Token );
		} );

		var limited = Compile(
			"(a|aa)*b",
			new RegularExpressionOptions { MaximumMatchStates = 10 }
		);
		var result = limited.Match( "aaaaaaaaaaaaaaaa" );
		Assert.False( result.IsSuccess );
		Assert.Equal(
			RegularExpressionDiagnosticCode.MatchResourceLimitExceeded,
			result.Diagnostic!.Code
		);
	}

	/// <summary>Verifies UTF-8 matching reports exact byte offsets and exact capture slices.</summary>
	[Fact]
	public void Utf8ByteMatchingReturnsAuthoritativeByteSlices() {
		var expression = Compile( "(é)+" );
		var input = Encoding.UTF8.GetBytes( "xéé!" );
		var result = expression.Match( input );
		Assert.True( result.IsMatch );
		Assert.Equal( 1, result.Match!.ByteIndex );
		Assert.Equal( 4, result.Match.ByteLength );
		Assert.Equal( Encoding.UTF8.GetBytes( "éé" ), result.Match.Value.ToArray() );
		Assert.Equal( 3, result.Match.Captures[ 0 ].ByteIndex );
		Assert.Equal( Encoding.UTF8.GetBytes( "é" ), result.Match.Captures[ 0 ].Value.ToArray() );
	}

	/// <summary>Verifies a UTF-8 search offset cannot split one decoded scalar.</summary>
	[Fact]
	public void Utf8ByteMatchingRejectsSplitScalarStartOffset() {
		var expression = Compile( "." );
		var result = expression.Match(
			Encoding.UTF8.GetBytes( "é" ),
			matchOptions: new() { StartByteOffset = 1 }
		);
		Assert.False( result.IsSuccess );
		Assert.Equal(
			RegularExpressionDiagnosticCode.InvalidStartByteOffset,
			result.Diagnostic!.Code
		);
	}

	/// <summary>Verifies byte mode exposes every byte as an independent matching unit.</summary>
	[Fact]
	public void ByteModeUsesOneMatchingUnitPerSourceByte() {
		var expression = Compile( ".+" );
		var input = new byte[] { 0x41, 0xFF, 0x42 };
		var result = expression.Match(
			input,
			new() { DecodingMode = TextDecodingMode.Bytes }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( input, result.Match!.Value.ToArray() );
	}

	/// <summary>Verifies each malformed UTF-8 byte is either preserved, replaced, or rejected explicitly.</summary>
	[Fact]
	public void InvalidUtf8PolicyIsExplicitAndBytePreserving() {
		var input = new byte[] { 0x41, 0xFF, 0x42 };
		var dot = Compile( "." );
		var preserved = dot.Match(
			input,
			new() { InvalidEncodingPolicy = InvalidEncodingPolicy.PreserveBytes },
			new() { StartByteOffset = 1, RequireMatchAtStart = true }
		);
		Assert.True( preserved.IsMatch );
		Assert.Equal( new byte[] { 0xFF }, preserved.Match!.Value.ToArray() );

		var replacement = Compile( "�" ).Match(
			input,
			new() { InvalidEncodingPolicy = InvalidEncodingPolicy.Replace },
			new() { StartByteOffset = 1, RequireMatchAtStart = true }
		);
		Assert.True( replacement.IsMatch );
		Assert.Equal( new byte[] { 0xFF }, replacement.Match!.Value.ToArray() );

		Assert.Throws<DecoderFallbackException>( () => dot.Match(
			input,
			new() { InvalidEncodingPolicy = InvalidEncodingPolicy.Throw }
		) );
	}

	/// <summary>Verifies opaque malformed bytes compare only with the same opaque source-byte value in back-references.</summary>
	[Fact]
	public void PreservedInvalidBytesRemainOpaqueToBackReferences() {
		var expression = Compile( "(.)\\1" );
		var opaquePair = expression.Match( new byte[] { 0xFF, 0xFF } );
		Assert.True( opaquePair.IsMatch );
		Assert.Equal( new byte[] { 0xFF, 0xFF }, opaquePair.Match!.Value.ToArray() );

		var validPrivateUse = Encoding.UTF8.GetBytes( char.ConvertFromUtf32( 0xF00FF ) );
		var mixed = new byte[ validPrivateUse.Length + 1 ];
		validPrivateUse.CopyTo( mixed, 0 );
		mixed[ ^1 ] = 0xFF;
		var mixedResult = expression.Match(
			mixed,
			matchOptions: new() { RequireMatchAtStart = true }
		);
		Assert.False( mixedResult.IsMatch );
	}

	private static ICompiledRegularExpression Compile(
		string pattern,
		RegularExpressionOptions? options = null
	) {
		var result = Provider.Compile( pattern, options );
		Assert.True( result.IsSuccess, result.Diagnostic?.Message );
		return result.Expression!;
	}
}
