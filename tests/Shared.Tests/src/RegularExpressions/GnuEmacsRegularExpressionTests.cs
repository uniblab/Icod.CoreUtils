namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.RegularExpressions;
using Xunit;

/// <summary>Exercises the GNU Emacs syntax profile used by Coreutils <c>ptx</c>.</summary>
public sealed class GnuEmacsRegularExpressionTests {
	private static readonly GnuEmacsRegularExpressionProvider Provider = new(
		PosixCLocaleRegularExpressionCharacterClassProvider.Instance
	);

	/// <summary>Verifies plus and question mark are unescaped repetition operators.</summary>
	/// <param name="pattern">The Emacs regular expression.</param>
	/// <param name="input">The input text.</param>
	/// <param name="expected">The expected leftmost-longest match.</param>
	[Theory]
	[InlineData( "ab+", "abbb", "abbb" )]
	[InlineData( "ab?", "abbb", "ab" )]
	public void PlusAndQuestionMarkAreUnescapedOperators(
		string pattern,
		string input,
		string expected
	) {
		var result = Compile( pattern ).Match( input, new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	/// <summary>Verifies escaped plus and question mark are ordinary literals.</summary>
	/// <param name="pattern">The Emacs regular expression.</param>
	/// <param name="input">The input text.</param>
	/// <param name="expected">The expected literal match.</param>
	[Theory]
	[InlineData( @"a\+", "a+", "a+" )]
	[InlineData( @"a\?", "a?", "a?" )]
	public void EscapedPlusAndQuestionMarkAreLiterals(
		string pattern,
		string input,
		string expected
	) {
		var result = Compile( pattern ).Match( input, new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( expected, result.Match!.Value );
	}

	/// <summary>Verifies Emacs retains escaped grouping, alternation, and interval braces.</summary>
	[Fact]
	public void GroupingAlternationAndIntervalsRemainEscaped() {
		var result = Compile( @"\(ab\|cd\)\{2\}" ).Match(
			"abcd",
			new() { RequireMatchAtStart = true }
		);
		Assert.True( result.IsMatch );
		Assert.Equal( "abcd", result.Match!.Value );
	}

	/// <summary>Verifies the Emacs dot syntax bit combination used by Gnulib.</summary>
	[Fact]
	public void DotMatchesNullButNotLineFeed() {
		Assert.True( Compile( "." ).Match( "\0" ).IsMatch );
		Assert.False( Compile( "." ).Match( "\n" ).IsMatch );
	}

	/// <summary>Verifies that backslash is an ordinary bracket character in the Emacs profile.</summary>
	[Fact]
	public void BackslashDoesNotEscapeClosingBracket() {
		var result = Compile( @"[\]]" ).Match( @"\]", new() { RequireMatchAtStart = true } );
		Assert.True( result.IsMatch );
		Assert.Equal( @"\]", result.Match!.Value );
	}

	/// <summary>Verifies the default GNU <c>ptx</c> sentence expression compiles and includes punctuation.</summary>
	[Fact]
	public void PtxSentenceExpressionCompiles() {
		var expression = Compile( "[.?!][]\"')}]*\\($\\|\t\\|  \\)[ \t\n]*" );
		var result = expression.Match( "First.  Second" );
		Assert.True( result.IsMatch );
		Assert.Equal( ".  ", result.Match!.Value );
	}

	private static ICompiledRegularExpression Compile( string pattern ) {
		var result = Provider.Compile( pattern );
		Assert.True( result.IsSuccess, result.Diagnostic?.Message );
		return result.Expression!;
	}
}
