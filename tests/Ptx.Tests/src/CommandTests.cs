namespace Icod.CoreUtils.Ptx.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Exercises GNU Coreutils 9.11-compatible command behavior.</summary>
public sealed class CommandTests {
	private static string Lines( params string[] values ) => string.Concat(
		values.Select( value => string.Concat( value, Environment.NewLine ) )
	);

	/// <summary>Verifies the GNU default dumb output and bytewise keyword order.</summary>
	[Fact]
	public async Task DefaultOutputMatchesGnuFixture() {
		var result = await RunAsync( [], "the quick brown fox\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal(
			Lines(
				"                           the quick   brown fox",
				"                     the quick brown   fox",
				"                                 the   quick brown fox",
				"                                       the quick brown fox"
			),
			result.Output
		);
		Assert.Equal( string.Empty, result.Error );
	}

	/// <summary>Verifies roff output and the default macro name.</summary>
	[Fact]
	public async Task RoffOutputMatchesGnuFixture() {
		var result = await RunAsync( [ "-O" ], "the quick brown fox\n" );
		Assert.Equal(
			Lines(
				".xx \"\" \"the quick\" \"brown fox\" \"\"",
				".xx \"\" \"the quick brown\" \"fox\" \"\"",
				".xx \"\" \"the\" \"quick brown fox\" \"\"",
				".xx \"\" \"\" \"the quick brown fox\" \"\""
			),
			result.Output
		);
	}

	/// <summary>Verifies TeX output and key/after field separation.</summary>
	[Fact]
	public async Task TexOutputMatchesGnuFixture() {
		var result = await RunAsync( [ "-T" ], "the quick brown fox\n" );
		Assert.Equal(
			Lines(
				"\\xx {}{the quick}{brown}{ fox}{}",
				"\\xx {}{the quick brown}{fox}{}{}",
				"\\xx {}{the}{quick}{ brown fox}{}",
				"\\xx {}{}{the}{ quick brown fox}{}"
			),
			result.Output
		);
	}

	/// <summary>Verifies traditional mode defaults to line contexts and roff.</summary>
	[Fact]
	public async Task TraditionalModeUsesRoffAndIndexesPunctuation() {
		var result = await RunAsync( [ "-G" ], "a, b\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal(
			Lines(
				".xx \"\" \"\" \"a, b\" \"\"",
				".xx \"\" \"a,\" \"b\" \"\""
			),
			result.Output
		);
	}

	/// <summary>Verifies ignore and only files apply to complete byte words.</summary>
	[Fact]
	public async Task IgnoreAndOnlyFilesFilterWords() {
		var directory = CreateDirectory();
		try {
			var ignore = Path.Combine( directory, "ignore" );
			var only = Path.Combine( directory, "only" );
			await File.WriteAllTextAsync( ignore, "the\n" );
			await File.WriteAllTextAsync( only, "brown\nfox\n" );
			var result = await RunAsync( [ "-i", ignore, "-o", only ], "the quick brown fox\n" );
			Assert.Equal(
				Lines(
					"                           the quick   brown fox",
					"                     the quick brown   fox"
				),
				result.Output
			);
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies a break-character file changes default word recognition.</summary>
	[Fact]
	public async Task BreakFileDefinesWordSeparators() {
		var directory = CreateDirectory();
		try {
			var breaks = Path.Combine( directory, "breaks" );
			await File.WriteAllTextAsync( breaks, " -\n" );
			var result = await RunAsync( [ "-b", breaks, "-O" ], "alpha-beta gamma\n" );
			Assert.Contains( "\"alpha-\" \"beta gamma\"", result.Output, StringComparison.Ordinal );
			Assert.Contains( "\"alpha-beta\" \"gamma\"", result.Output, StringComparison.Ordinal );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies custom word matching is provided by the Shared GNU Emacs regular-expression engine.</summary>
	[Fact]
	public async Task WordRegularExpressionSelectsDigitRuns() {
		var result = await RunAsync( [ "-W", "[0-9]+", "-O" ], "a 12 b 3\n" );
		Assert.Equal(
			Lines(
				".xx \"\" \"a\" \"12 b 3\" \"\"",
				".xx \"\" \"a 12 b\" \"3\" \"\""
			),
			result.Output
		);
	}

	/// <summary>Verifies an empty sentence expression treats the source as one context.</summary>
	[Fact]
	public async Task EmptySentenceExpressionDisablesContextSplitting() {
		var result = await RunAsync( [ "-S", "", "-O" ], "alpha.  beta\n" );
		Assert.Contains( "alpha.  beta", result.Output, StringComparison.Ordinal );
	}

	/// <summary>Verifies escaped plus remains a literal in GNU Emacs regular-expression syntax.</summary>
	[Fact]
	public async Task EscapedPlusInWordRegularExpressionIsLiteral() {
		var result = await RunAsync( [ "-W", @"[0-9]\+", "-O" ], "12 3+\n" );
		Assert.Equal( Lines( ".xx \"\" \"12\" \"3+\" \"\"" ), result.Output );
	}

	/// <summary>Verifies a custom sentence expression includes the matched separator in its context.</summary>
	[Fact]
	public async Task CustomSentenceExpressionIncludesSeparator() {
		var result = await RunAsync( [ "-S", ",", "-O" ], "a,b,c" );
		Assert.Equal(
			Lines(
				".xx \"\" \"\" \"a,\" \"\"",
				".xx \"\" \"\" \"b,\" \"\"",
				".xx \"\" \"\" \"c\" \"\""
			),
			result.Output
		);
	}

	/// <summary>Verifies a zero-length sentence expression is rejected rather than looping.</summary>
	[Fact]
	public async Task ZeroLengthSentenceExpressionIsRejected() {
		var result = await RunAsync( [ "-S", "a*", "-O" ], "bbb" );
		Assert.NotEqual( 0, result.Status );
		Assert.Contains( "match of length zero", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies an empty input operand is another spelling of standard input.</summary>
	[Fact]
	public async Task EmptyInputOperandReadsStandardInput() {
		var result = await RunAsync( [ "-O", "" ], "alpha\n" );
		Assert.Equal( Lines( ".xx \"\" \"\" \"alpha\" \"\"" ), result.Output );
	}

	/// <summary>Verifies custom macro names are used in both structured formats.</summary>
	/// <param name="format">The selected structured-format option.</param>
	/// <param name="prefix">The expected output prefix.</param>
	[Theory]
	[InlineData( "-O", ".index" )]
	[InlineData( "-T", "\\index" )]
	public async Task MacroNameIsConfigurable( string format, string prefix ) {
		var result = await RunAsync( [ format, "-M", "index" ], "alpha\n" );
		Assert.StartsWith( prefix, result.Output, StringComparison.Ordinal );
	}

	/// <summary>Verifies case folding affects ordering and filter membership.</summary>
	[Fact]
	public async Task IgnoreCaseFoldsAsciiWords() {
		var directory = CreateDirectory();
		try {
			var ignore = Path.Combine( directory, "ignore" );
			await File.WriteAllTextAsync( ignore, "ALPHA\n" );
			var result = await RunAsync( [ "-f", "-i", ignore, "-O" ], "alpha Beta\n" );
			Assert.Equal( Lines( ".xx \"\" \"alpha\" \"Beta\" \"\"" ), result.Output );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies input references are excluded from keyword recognition.</summary>
	[Fact]
	public async Task InputReferencesAreExcludedAndPrinted() {
		var result = await RunAsync( [ "-r", "-O" ], "001 alpha beta\n" );
		Assert.Equal(
			Lines(
				".xx \"\" \"\" \"alpha beta\" \"\" \"001\"",
				".xx \"\" \"alpha\" \"beta\" \"\" \"001\""
			),
			result.Output
		);
	}

	/// <summary>Verifies custom sentence boundaries do not mistake mid-line text for a new reference.</summary>
	[Fact]
	public async Task CustomSentenceExpressionPreservesMidLineInputReference() {
		var result = await RunAsync( [ "-r", "-S", ",", "-O" ], "001 alpha,beta\n" );
		Assert.Equal(
			Lines(
				".xx \"\" \"\" \"alpha,\" \"\" \"001\"",
				".xx \"\" \"\" \"beta\" \"\" \"001\""
			),
			result.Output
		);
	}

	/// <summary>Verifies automatic-reference spacing matches GNU output exactly.</summary>
	[Fact]
	public async Task AutoReferenceWidthMatchesGnuFixture() {
		var result = await RunAsync( [ "-A" ], "alpha\n" );
		Assert.Equal(
			Lines( string.Concat( ":1:", new string( ' ', 35 ), "alpha" ) ),
			result.Output
		);
	}

	/// <summary>Verifies automatic references contain the source pathname and one-based line.</summary>
	[Fact]
	public async Task AutoReferencesContainPathAndLine() {
		var directory = CreateDirectory();
		try {
			var input = Path.Combine( directory, "input.txt" );
			await File.WriteAllTextAsync( input, "alpha\nbeta\n" );
			var result = await RunAsync( [ "-A", "-O", input ], string.Empty );
			Assert.Contains( string.Concat( "\"", input, ":1\"" ), result.Output, StringComparison.Ordinal );
			Assert.Contains( string.Concat( "\"", input, ":2\"" ), result.Output, StringComparison.Ordinal );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies C-style escapes are decoded before regular-expression compilation.</summary>
	[Fact]
	public async Task WordRegularExpressionDecodesHexEscapes() {
		var result = await RunAsync( [ "-W", @"\x61+", "-O" ], "aaa b\n" );
		Assert.Equal( Lines( ".xx \"\" \"\" \"aaa b\" \"\"" ), result.Output );
	}

	/// <summary>Verifies C-style escapes are decoded in the truncation marker.</summary>
	[Fact]
	public async Task TruncationMarkerDecodesHexEscapes() {
		var result = await RunAsync(
			[ "-O", "-w", "12", "-F", @"\x2a" ],
			"one two three four five six\n"
		);
		Assert.Equal(
			Lines(
				".xx \"\" \"*\" \"five*\" \"\"",
				".xx \"\" \"*\" \"four*\" \"\"",
				".xx \"\" \"\" \"one*\" \"\"",
				".xx \"\" \"*\" \"six\" \"\"",
				".xx \"\" \"*\" \"three*\" \"\"",
				".xx \"\" \"*\" \"two*\" \"\""
			),
			result.Output
		);
	}

	/// <summary>Verifies right-side input references are aligned outside the selected width.</summary>
	[Fact]
	public async Task RightSideReferencesMatchGnuFixture() {
		var result = await RunAsync( [ "-r", "-R", "-w", "40" ], "001 alpha beta\n" );
		Assert.Equal(
			Lines(
				"                    alpha beta             001",
				"            alpha   beta                   001"
			),
			result.Output
		);
	}

	/// <summary>Verifies roff output doubles embedded quotation marks.</summary>
	[Fact]
	public async Task RoffEscapesQuotationMarks() {
		var result = await RunAsync( [ "-O" ], "a\"b c\n" );
		Assert.Equal(
			Lines(
				".xx \"\" \"\" \"a\"\"b c\" \"\"",
				".xx \"\" \"a\"\"\" \"b c\" \"\"",
				".xx \"\" \"a\"\"b\" \"c\" \"\""
			),
			result.Output
		);
	}

	/// <summary>Verifies TeX output escapes all characters edited by GNU <c>ptx</c>.</summary>
	[Fact]
	public async Task TexEscapesSpecialCharacters() {
		var result = await RunAsync( [ "-T" ], "a$b {c}\\d\n" );
		Assert.Equal(
			Lines(
				@"\xx {}{}{a}{\$b $\{$c$\}$\backslash{}d}{}",
				@"\xx {}{a\$}{b}{ $\{$c$\}$\backslash{}d}{}",
				@"\xx {}{a\$b $\{$}{c}{$\}$\backslash{}d}{}",
				@"\xx {}{a\$b $\{$c$\}$\backslash{}}{d}{}{}"
			),
			result.Output
		);
	}

	/// <summary>Verifies traditional mode writes its optional output operand.</summary>
	[Fact]
	public async Task TraditionalOutputOperandIsWritten() {
		var directory = CreateDirectory();
		try {
			var input = Path.Combine( directory, "input" );
			var output = Path.Combine( directory, "output" );
			await File.WriteAllTextAsync( input, "alpha beta\n" );
			var result = await RunAsync( [ "-G", input, output ], string.Empty );
			Assert.Equal( string.Empty, result.Output );
			Assert.Contains( ".xx", await File.ReadAllTextAsync( output ), StringComparison.Ordinal );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies GNU mode accepts several input files and orders all occurrences together.</summary>
	[Fact]
	public async Task MultipleInputFilesAreCombined() {
		var directory = CreateDirectory();
		try {
			var one = Path.Combine( directory, "one" );
			var two = Path.Combine( directory, "two" );
			await File.WriteAllTextAsync( one, "zeta\n" );
			await File.WriteAllTextAsync( two, "alpha\n" );
			var result = await RunAsync( [ "-O", one, two ], string.Empty );
			Assert.True(
				result.Output.IndexOf( "alpha", StringComparison.Ordinal )
				< result.Output.IndexOf( "zeta", StringComparison.Ordinal )
			);
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies traditional mode rejects a third operand.</summary>
	[Fact]
	public async Task TraditionalModeRejectsExtraOperand() {
		var result = await RunAsync( [ "-G", "one", "two", "three" ], string.Empty );
		Assert.NotEqual( 0, result.Status );
		Assert.Contains( "extra operand", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies invalid width and gap values are usage failures.</summary>
	/// <param name="option">The numeric option under test.</param>
	[Theory]
	[InlineData( "-w" )]
	[InlineData( "-g" )]
	public async Task PositiveNumericOptionsRejectZero( string option ) {
		var result = await RunAsync( [ option, "0" ], string.Empty );
		Assert.NotEqual( 0, result.Status );
		Assert.Contains( "invalid", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies numeric options use GNU base-zero syntax.</summary>
	/// <param name="option">The numeric option under test.</param>
	/// <param name="value">The base-zero numeric spelling.</param>
	[Theory]
	[InlineData( "-w", "010" )]
	[InlineData( "-w", "0x20" )]
	[InlineData( "-w", "+24" )]
	[InlineData( "-g", "010" )]
	public async Task PositiveNumericOptionsAcceptBaseZeroValues( string option, string value ) {
		var result = await RunAsync( [ option, value, "-O" ], "alpha beta\n" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Empty, result.Error );
	}

	/// <summary>Verifies invalid octal digits are rejected in base-zero numeric syntax.</summary>
	/// <param name="option">The numeric option under test.</param>
	[Theory]
	[InlineData( "-w" )]
	[InlineData( "-g" )]
	public async Task PositiveNumericOptionsRejectInvalidOctal( string option ) {
		var result = await RunAsync( [ option, "08" ], string.Empty );
		Assert.NotEqual( 0, result.Status );
		Assert.Contains( "invalid", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies GNU unique abbreviations for structured format names.</summary>
	/// <param name="value">The abbreviated format value.</param>
	/// <param name="expectedPrefix">The expected structured-output prefix.</param>
	[Theory]
	[InlineData( "r", ".xx" )]
	[InlineData( "t", "\\xx" )]
	public async Task FormatNamesAcceptUniqueAbbreviations( string value, string expectedPrefix ) {
		var result = await RunAsync( [ string.Concat( "--format=", value ) ], "alpha\n" );
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( expectedPrefix, result.Output, StringComparison.Ordinal );
	}

	/// <summary>Verifies unsupported format names are diagnosed.</summary>
	[Fact]
	public async Task InvalidFormatIsRejected() {
		var result = await RunAsync( [ "--format=html" ], string.Empty );
		Assert.NotEqual( 0, result.Status );
		Assert.Contains( "invalid argument", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies unknown options are not silently treated as operands.</summary>
	[Fact]
	public async Task UnknownOptionIsRejected() {
		var result = await RunAsync( [ "--definitely-not-an-option" ], string.Empty );
		Assert.NotEqual( 0, result.Status );
		Assert.Contains( "unrecognized option", result.Error, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Verifies help and version do not consume standard input.</summary>
	/// <param name="option">The informational option.</param>
	/// <param name="expected">The expected output fragment.</param>
	[Theory]
	[InlineData( "--help", "Usage: ptx" )]
	[InlineData( "--version", "ptx (Icod.CoreUtils)" )]
	public async Task InformationalOptionsSucceed( string option, string expected ) {
		var result = await RunAsync( [ option ], "ignored" );
		Assert.Equal( 0, result.Status );
		Assert.Contains( expected, result.Output, StringComparison.Ordinal );
	}

	/// <summary>Verifies cancellation maps to the repository cancellation status.</summary>
	[Fact]
	public async Task CancellationReturnsCanceledStatus() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await RunAsync( [], "alpha\n", cancellation.Token );
		Assert.Equal( CommandExitCodes.Canceled, result.Status );
	}

	/// <summary>Verifies injected text readers and writers remain caller-owned.</summary>
	[Fact]
	public async Task InjectedTextStreamsRemainOpen() {
		var input = new TrackingReader( "alpha\n" );
		var output = new TrackingWriter();
		var error = new TrackingWriter();
		var status = await Command.RunAsync( [], input, output, error );
		Assert.Equal( 0, status );
		Assert.False( input.WasDisposed );
		Assert.False( output.WasDisposed );
		Assert.False( error.WasDisposed );
	}

	private static async Task<RunResult> RunAsync(
		string[] args,
		string input,
		CancellationToken cancellationToken = default
	) {
		using var standardInput = new StringReader( input );
		using var standardOutput = new StringWriter();
		using var standardError = new StringWriter();
		var status = await Command.RunAsync(
			args,
			standardInput,
			standardOutput,
			standardError,
			cancellationToken
		);
		return new RunResult( status, standardOutput.ToString(), standardError.ToString() );
	}

	private static string CreateDirectory() {
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "ptx-tests-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private sealed record RunResult( int Status, string Output, string Error );

	private sealed class TrackingReader : StringReader {
		/// <summary>Initializes a tracked reader.</summary>
		/// <param name="value">The source text.</param>
		internal TrackingReader( string value ) : base( value ) { }
		/// <summary>Gets whether disposal was attempted.</summary>
		internal bool WasDisposed { get; private set; }
		/// <summary>Records disposal and then delegates to <see cref="StringReader.Dispose(bool)"/>.</summary>
		/// <param name="disposing">Whether managed resources should be released.</param>
		protected override void Dispose( bool disposing ) {
			this.WasDisposed = true;
			base.Dispose( disposing );
		}
	}

	private sealed class TrackingWriter : StringWriter {
		/// <summary>Gets whether disposal was attempted.</summary>
		internal bool WasDisposed { get; private set; }
		/// <summary>Records disposal and then delegates to <see cref="StringWriter.Dispose(bool)"/>.</summary>
		/// <param name="disposing">Whether managed resources should be released.</param>
		protected override void Dispose( bool disposing ) {
			this.WasDisposed = true;
			base.Dispose( disposing );
		}
	}
}
