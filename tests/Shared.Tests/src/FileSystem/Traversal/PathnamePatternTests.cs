using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>
/// Tests segment-aware pathname pattern parsing and matching.
/// </summary>
public sealed class PathnamePatternTests {
	/// <summary>
	/// Verifies ordinary wildcard and bracket-expression matching.
	/// </summary>
	[Fact]
	public void MatchesWildcardsAndBracketExpressions() {
		var pattern = PathnamePattern.Parse( "src/[a-c]?*.cs" );

		Assert.True( pattern.IsMatch( System.IO.Path.Combine( "src", "ab-file.cs" ) ) );
		Assert.False( pattern.IsMatch( System.IO.Path.Combine( "src", "db-file.cs" ) ) );
		Assert.False( pattern.IsMatch( System.IO.Path.Combine( "src", "a.cs" ) ) );
	}

	/// <summary>
	/// Verifies that a complete <c>**</c> segment matches zero or more segments.
	/// </summary>
	[Fact]
	public void DoubleStarMatchesZeroOrMoreSegments() {
		var pattern = PathnamePattern.Parse( "a/**/b.txt" );

		Assert.True( pattern.IsMatch( System.IO.Path.Combine( "a", "b.txt" ) ) );
		Assert.True( pattern.IsMatch( System.IO.Path.Combine( "a", "x", "y", "b.txt" ) ) );
		Assert.False( pattern.IsMatch( System.IO.Path.Combine( "a", "x", "b.bin" ) ) );
	}

	/// <summary>
	/// Verifies that ordinary segment wildcards do not cross pathname separators.
	/// </summary>
	[Fact]
	public void OrdinaryStarDoesNotCrossSeparator() {
		var pattern = PathnamePattern.Parse( "a/*.txt" );

		Assert.True( pattern.IsMatch( System.IO.Path.Combine( "a", "b.txt" ) ) );
		Assert.False( pattern.IsMatch( System.IO.Path.Combine( "a", "x", "b.txt" ) ) );
	}

	/// <summary>
	/// Verifies POSIX-style leading-period protection.
	/// </summary>
	[Fact]
	public void LeadingPeriodRequiresExplicitPeriodByDefault() {
		Assert.False( PathnamePatternMatcher.IsSegmentMatch( "*", ".hidden" ) );
		Assert.True( PathnamePatternMatcher.IsSegmentMatch( ".*", ".hidden" ) );
		Assert.True( PathnamePatternMatcher.IsSegmentMatch(
			"*",
			".hidden",
			new PathnamePatternOptions {
				LeadingPeriodPolicy = LeadingPeriodPolicy.WildcardMayMatch
			}
		) );
	}

	/// <summary>
	/// Verifies bracket negation and ordinal case policy.
	/// </summary>
	[Fact]
	public void SupportsNegatedClassesAndExplicitCasePolicy() {
		var options = new PathnamePatternOptions {
			CaseSensitivity = PathCaseSensitivity.Insensitive
		};

		Assert.True( PathnamePatternMatcher.IsSegmentMatch( "[!a-c].TXT", "D.txt", options ) );
		Assert.False( PathnamePatternMatcher.IsSegmentMatch( "[!a-c].TXT", "B.txt", options ) );
	}



	/// <summary>
	/// Verifies lexical current-directory segments in wildcard patterns and candidates.
	/// </summary>
	[Fact]
	public void IgnoresCurrentDirectorySegmentsForWildcardMatching() {
		var pattern = PathnamePattern.Parse( System.IO.Path.Combine( ".", "*.txt" ) );

		Assert.True( pattern.IsMatch( System.IO.Path.Combine( ".", "file.txt" ) ) );
		Assert.True( pattern.IsMatch( "file.txt" ) );
	}

	/// <summary>
	/// Verifies that relative and rooted patterns do not silently match one another's pathname form.
	/// </summary>
	[Fact]
	public void DistinguishesRelativeAndRootedPatterns() {
		var relative = PathnamePattern.Parse( "file.txt" );
		var absolutePath = System.IO.Path.GetFullPath( "file.txt" );

		Assert.False( relative.IsMatch( absolutePath ) );
		Assert.True( PathnamePattern.Parse( absolutePath ).IsMatch( absolutePath ) );
	}

	/// <summary>
	/// Verifies that recursive wildcard matching does not consume a protected leading-period segment.
	/// </summary>
	[Fact]
	public void DoubleStarHonorsLeadingPeriodPolicy() {
		var pattern = PathnamePattern.Parse( "a/**/b.txt" );

		Assert.False( pattern.IsMatch( System.IO.Path.Combine( "a", ".hidden", "b.txt" ) ) );
		Assert.True( PathnamePattern.Parse(
			"a/**/b.txt",
			new PathnamePatternOptions {
				LeadingPeriodPolicy = LeadingPeriodPolicy.WildcardMayMatch
			}
		).IsMatch( System.IO.Path.Combine( "a", ".hidden", "b.txt" ) ) );
	}

	/// <summary>
	/// Verifies that invalid option enumeration values are rejected deterministically.
	/// </summary>
	[Fact]
	public void RejectsInvalidPatternOptionValue() {
		Assert.Throws<ArgumentOutOfRangeException>( () => PathnamePattern.Parse(
			"*",
			new PathnamePatternOptions {
				CaseSensitivity = (PathCaseSensitivity)int.MaxValue
			}
		) );
	}


	/// <summary>
	/// Verifies drive, UNC, and device roots through the Windows pathname parser.
	/// </summary>
	[Fact]
	public void PreservesWindowsRootKindsWhenRunningOnWindows() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		Assert.True( PathnamePattern.Parse( @"C:\root\*.txt" ).IsMatch( @"C:\root\file.txt" ) );
		Assert.False( PathnamePattern.Parse( @"C:\root\*.txt" ).IsMatch( @"D:\root\file.txt" ) );
		Assert.True( PathnamePattern.Parse( @"\\server\share\*.txt" ).IsMatch( @"\\server\share\file.txt" ) );
		Assert.True( PathnamePattern.Parse( @"\\?\C:\root\*.txt" ).IsMatch( @"\\?\C:\root\file.txt" ) );
	}

	/// <summary>
	/// Verifies backslash quoting on platforms where backslash is not a pathname separator.
	/// </summary>
	[Fact]
	public void BackslashCanQuoteMetacharacterOnUnixLikePlatforms() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var options = new PathnamePatternOptions { BackslashEscapes = true };
		Assert.True( PathnamePatternMatcher.IsSegmentMatch( @"a\*b", "a*b", options ) );
		Assert.False( PathnamePatternMatcher.IsSegmentMatch( @"a\*b", "axxb", options ) );
	}
}
