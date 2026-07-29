namespace Icod.CoreUtils.Expr.Tests;

using System.Globalization;
using System.Numerics;
using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.RegularExpressions;
using Xunit;

public sealed class ExprCommandTests {
	[Theory]
	[InlineData( "hello", 0 )]
	[InlineData( "0", 1 )]
	[InlineData( "00", 1 )]
	[InlineData( "-0", 1 )]
	[InlineData( "", 1 )]
	[InlineData( "-", 0 )]
	public async Task BareOperandsPreserveTextAndUseGnuNullRules( string operand, int expectedStatus ) {
		var result = await RunAsync( [ operand ] );
		Assert.Equal( expectedStatus, result.Status );
		Assert.Equal( string.Concat( operand, Environment.NewLine ), result.Output );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task ArithmeticUsesDocumentedPrecedenceAndParentheses() {
		var precedence = await RunAsync( [ "1", "+", "2", "*", "3" ] );
		var grouped = await RunAsync( [ "(", "1", "+", "2", ")", "*", "3" ] );
		Assert.Equal( string.Concat( "7", Environment.NewLine ), precedence.Output );
		Assert.Equal( string.Concat( "9", Environment.NewLine ), grouped.Output );
	}

	[Fact]
	public async Task ArithmeticIsArbitraryPrecision() {
		const string operand = "999999999999999999999999999999999999";
		var result = await RunAsync( [ operand, "*", operand ] );
		var value = BigInteger.Parse( operand, CultureInfo.InvariantCulture );
		Assert.Equal(
			string.Concat( ( value * value ).ToString( CultureInfo.InvariantCulture ), Environment.NewLine ),
			result.Output
		);
		Assert.Equal( 0, result.Status );
	}

	[Theory]
	[InlineData( "20", "/", "3", "6" )]
	[InlineData( "-7", "/", "3", "-2" )]
	[InlineData( "-7", "%", "3", "-1" )]
	[InlineData( "7", "%", "-3", "1" )]
	public async Task DivisionAndRemainderTruncateTowardZero(
		string left,
		string operation,
		string right,
		string expected
	) {
		var result = await RunAsync( [ left, operation, right ] );
		Assert.Equal( string.Concat( expected, Environment.NewLine ), result.Output );
	}

	[Theory]
	[InlineData( "1", "+", "word", "non-integer argument" )]
	[InlineData( "1", "/", "0", "division by zero" )]
	[InlineData( "1", "%", "0", "division by zero" )]
	public async Task ArithmeticFailuresUseStatusTwo(
		string left,
		string operation,
		string right,
		string diagnostic
	) {
		var result = await RunAsync( [ left, operation, right ] );
		Assert.Equal( 2, result.Status );
		Assert.Empty( result.Output );
		Assert.Contains( diagnostic, result.Error );
	}

	[Fact]
	public async Task BooleanOperatorsShortCircuitRuntimeFailuresButStillParse() {
		var orResult = await RunAsync( [ "1", "|", "1", "/", "0" ] );
		var andResult = await RunAsync( [ "0", "&", "1", "/", "0" ] );
		var badSyntax = await RunAsync( [ "1", "|", "(", "2" ] );
		Assert.Equal( 0, orResult.Status );
		Assert.Equal( string.Concat( "1", Environment.NewLine ), orResult.Output );
		Assert.Equal( 1, andResult.Status );
		Assert.Equal( string.Concat( "0", Environment.NewLine ), andResult.Output );
		Assert.Equal( 2, badSyntax.Status );
		Assert.Contains( "expecting ')'", badSyntax.Error );
	}

	[Fact]
	public async Task SkippedBooleanBranchesRetainGnuPrefixEvaluationButSkipRegexMatching() {
		var localeProvider = new CountingLocaleProvider();
		var regularExpressionProvider = new ThrowingRegularExpressionProvider();
		var result = await RunAsync(
			[ "1", "|", "length", "abc", ":", "\\(" ],
			localeProvider,
			regularExpressionProvider
		);
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Concat( "1", Environment.NewLine ), result.Output );
		Assert.Equal( 1, localeProvider.LengthCalls );
		Assert.Equal( 0, regularExpressionProvider.CompileCalls );
	}

	[Fact]
	public async Task BooleanOperatorsReturnTheDocumentedOperand() {
		var orResult = await RunAsync( [ "0", "|", "answer" ] );
		var andResult = await RunAsync( [ "answer", "&", "yes" ] );
		var falseAnd = await RunAsync( [ "answer", "&", "0" ] );
		Assert.Equal( string.Concat( "answer", Environment.NewLine ), orResult.Output );
		Assert.Equal( string.Concat( "answer", Environment.NewLine ), andResult.Output );
		Assert.Equal( string.Concat( "0", Environment.NewLine ), falseAnd.Output );
	}

	[Theory]
	[InlineData( "01", "=", "1", "1" )]
	[InlineData( "-2", "<", "-1", "1" )]
	[InlineData( "2", ">=", "10", "0" )]
	[InlineData( "2", "!=", "02", "0" )]
	[InlineData( "2", "==", "2", "1" )]
	public async Task IntegerLookingComparisonsAreNumeric(
		string left,
		string operation,
		string right,
		string expected
	) {
		var result = await RunAsync( [ left, operation, right ] );
		Assert.Equal( string.Concat( expected, Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task StringComparisonsUseTheInjectedCollationProvider() {
		var result = await RunAsync(
			[ "alpha", "<", "beta" ],
			localeProvider: new ReverseOrdinalLocaleProvider()
		);
		Assert.Equal( string.Concat( "0", Environment.NewLine ), result.Output );
		Assert.Equal( 1, result.Status );
	}

	[Fact]
	public async Task LengthUsesLogicalUnicodeScalars() {
		var result = await RunAsync( [ "length", "😀ab" ] );
		Assert.Equal( string.Concat( "3", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task IndexUsesLogicalUnicodeScalars() {
		var result = await RunAsync( [ "index", "😀abc", "c😀" ] );
		Assert.Equal( string.Concat( "1", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task SubstrUsesOneBasedLogicalCharacterPositions() {
		var result = await RunAsync( [ "substr", "😀abcd", "2", "3" ] );
		Assert.Equal( string.Concat( "abc", Environment.NewLine ), result.Output );
		Assert.Equal( 0, result.Status );
	}

	[Theory]
	[InlineData( "0", "2" )]
	[InlineData( "1", "0" )]
	[InlineData( "word", "2" )]
	[InlineData( "1", "word" )]
	public async Task InvalidSubstrPositionsReturnTheNullString( string position, string length ) {
		var result = await RunAsync( [ "substr", "abcdef", position, length ] );
		Assert.Equal( 1, result.Status );
		Assert.Equal( Environment.NewLine, result.Output );
	}

	[Theory]
	[InlineData( "+", "match" )]
	[InlineData( "+", "/" )]
	[InlineData( "+", ")" )]
	public async Task UnaryPlusQuotesTheFollowingToken( string quote, string token ) {
		var result = await RunAsync( [ quote, token ] );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Concat( token, Environment.NewLine ), result.Output );
	}

	[Theory]
	[InlineData( "abc", "a.*", "3", 0 )]
	[InlineData( "abc", "z.*", "0", 1 )]
	[InlineData( "aaa", "a\\+", "3", 0 )]
	public async Task ColonReturnsMatchedLogicalCharacterCount(
		string value,
		string pattern,
		string expected,
		int expectedStatus
	) {
		var result = await RunAsync( [ value, ":", pattern ] );
		Assert.Equal( expectedStatus, result.Status );
		Assert.Equal( string.Concat( expected, Environment.NewLine ), result.Output );
	}

	[Theory]
	[InlineData( "abc", "a\\(.*\\)", "bc", 0 )]
	[InlineData( "abc", "z\\(.*\\)", "", 1 )]
	[InlineData( "b", "\\(a\\)\\|b", "", 1 )]
	public async Task ColonReturnsTheFirstCaptureOrTheNullString(
		string value,
		string pattern,
		string expected,
		int expectedStatus
	) {
		var result = await RunAsync( [ value, ":", pattern ] );
		Assert.Equal( expectedStatus, result.Status );
		Assert.Equal( string.Concat( expected, Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task MatchKeywordIsEquivalentToColon() {
		var result = await RunAsync( [ "match", "abcdef", "abc\\(.*\\)" ] );
		Assert.Equal( string.Concat( "def", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task InvalidRegularExpressionUsesStatusTwo() {
		var result = await RunAsync( [ "abc", ":", "\\(" ] );
		Assert.Equal( 2, result.Status );
		Assert.Empty( result.Output );
		Assert.NotEmpty( result.Error );
	}

	[Fact]
	public async Task PrefixOperatorsAssociateToTheRightLikeGnuExpr() {
		var result = await RunAsync( [ "length", "+", "match" ] );
		Assert.Equal( string.Concat( "5", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task HelpVersionAndOptionTerminatorFollowGnuRules() {
		var help = await RunAsync( [ "--help" ] );
		var abbreviatedHelp = await RunAsync( [ "--h" ] );
		var version = await RunAsync( [ "--version" ] );
		var quotedHelp = await RunAsync( [ "--", "--help" ] );
		var trailing = await RunAsync( [ "--help", "x" ] );
		Assert.Equal( 0, help.Status );
		Assert.StartsWith( "Usage: expr", help.Output );
		Assert.Equal( help.Output, abbreviatedHelp.Output );
		Assert.Equal( 0, version.Status );
		Assert.StartsWith( "expr (Icod.CoreUtils)", version.Output );
		Assert.Equal( string.Concat( "--help", Environment.NewLine ), quotedHelp.Output );
		Assert.Equal( 2, trailing.Status );
		Assert.Contains( "unexpected argument", trailing.Error );
	}

	[Fact]
	public async Task MissingAndMalformedExpressionsUseStatusTwo() {
		var missing = await RunAsync( [] );
		var missingRight = await RunAsync( [ "1", "+" ] );
		var missingClose = await RunAsync( [ "(", "1" ] );
		var unexpectedClose = await RunAsync( [ "1", ")" ] );
		Assert.Equal( 2, missing.Status );
		Assert.Contains( "missing operand", missing.Error );
		Assert.Contains( "Try 'expr --help'", missing.Error );
		Assert.Equal( 2, missingRight.Status );
		Assert.Contains( "missing argument after '+'", missingRight.Error );
		Assert.Contains( "expecting ')'", missingClose.Error );
		Assert.Contains( "unexpected argument ')'", unexpectedClose.Error );
	}

	[Fact]
	public async Task ExcessiveNestingIsAControlledInvalidExpression() {
		var args = Enumerable.Repeat( "length", 300 ).Append( "x" ).ToArray();
		var result = await RunAsync( args );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "nesting depth", result.Error );
	}

	[Fact]
	public async Task CancellationReturnsTheRepositoryCancellationStatus() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var result = await RunAsync( [ "1" ], cancellationToken: source.Token );
		Assert.Equal( CommandExitCodes.Canceled, result.Status );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task OutputFailureReturnsInternalErrorStatus() {
		var result = await RunAsync( [ "1" ], output: new ThrowingTextWriter() );
		Assert.Equal( 3, result.Status );
		Assert.Contains( "simulated output failure", result.Error );
	}

	[Fact]
	public async Task LocaleFailureUsesGnuInvalidExpressionStatus() {
		var result = await RunAsync(
			[ "alpha", "<", "beta" ],
			localeProvider: new FailingLocaleProvider()
		);
		Assert.Equal( 2, result.Status );
		Assert.Contains( "string comparison failed", result.Error );
		Assert.Contains( "set LC_ALL='C'", result.Error );
	}

	[Fact]
	public async Task CallerOwnedWritersAreNotDisposed() {
		var output = new TrackingTextWriter();
		var error = new TrackingTextWriter();
		var result = await RunAsync( [ "1" ], output: output, error: error );
		Assert.Equal( 0, result.Status );
		Assert.False( output.WasDisposed );
		Assert.False( error.WasDisposed );
	}

	private static async Task<CommandResult> RunAsync(
		string[] args,
		IExpressionLocaleProvider? localeProvider = null,
		IRegularExpressionProvider? regularExpressionProvider = null,
		TextWriter? output = null,
		TextWriter? error = null,
		CancellationToken cancellationToken = default
	) {
		var standardOutput = output ?? new StringWriter( CultureInfo.InvariantCulture );
		var standardError = error ?? new StringWriter( CultureInfo.InvariantCulture );
		var status = await Command.RunAsync(
			args,
			new CommandContext(
				"expr",
				TextReader.Null,
				standardOutput,
				standardError,
				cancellationToken: cancellationToken
			),
			regularExpressionProvider ?? new GnuBasicRegularExpressionProvider(
				PosixCLocaleRegularExpressionCharacterClassProvider.Instance
			),
			localeProvider ?? new SystemExpressionLocaleProvider( CultureInfo.InvariantCulture )
		);
		return new CommandResult(
			status,
			standardOutput is StringWriter outputWriter ? outputWriter.ToString() : string.Empty,
			standardError is StringWriter errorWriter ? errorWriter.ToString() : string.Empty
		);
	}

	private sealed record CommandResult( int Status, string Output, string Error );

	private sealed class ReverseOrdinalLocaleProvider : IExpressionLocaleProvider {
		private readonly SystemExpressionLocaleProvider text = new( CultureInfo.InvariantCulture );

		public int Compare(
			string left,
			string right,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return -StringComparer.Ordinal.Compare( left, right );
		}

		public BigInteger GetLength(
			string value,
			CancellationToken cancellationToken = default
		) => this.text.GetLength( value, cancellationToken );

		public BigInteger IndexOfAny(
			string value,
			string characterSet,
			CancellationToken cancellationToken = default
		) => this.text.IndexOfAny( value, characterSet, cancellationToken );

		public string Substring(
			string value,
			BigInteger position,
			BigInteger length,
			CancellationToken cancellationToken = default
		) => this.text.Substring( value, position, length, cancellationToken );
	}

	private sealed class CountingLocaleProvider : IExpressionLocaleProvider {
		private readonly SystemExpressionLocaleProvider text = new( CultureInfo.InvariantCulture );

		public int LengthCalls { get; private set; }

		public int Compare(
			string left,
			string right,
			CancellationToken cancellationToken = default
		) => this.text.Compare( left, right, cancellationToken );

		public BigInteger GetLength(
			string value,
			CancellationToken cancellationToken = default
		) {
			this.LengthCalls++;
			return this.text.GetLength( value, cancellationToken );
		}

		public BigInteger IndexOfAny(
			string value,
			string characterSet,
			CancellationToken cancellationToken = default
		) => this.text.IndexOfAny( value, characterSet, cancellationToken );

		public string Substring(
			string value,
			BigInteger position,
			BigInteger length,
			CancellationToken cancellationToken = default
		) => this.text.Substring( value, position, length, cancellationToken );
	}

	private sealed class ThrowingRegularExpressionProvider : IRegularExpressionProvider {
		public int CompileCalls { get; private set; }

		public RegularExpressionCompileResult Compile(
			string pattern,
			RegularExpressionOptions? options = null,
			CancellationToken cancellationToken = default
		) {
			_ = pattern;
			_ = options;
			_ = cancellationToken;
			this.CompileCalls++;
			throw new InvalidOperationException( "regular expression compilation should have been skipped" );
		}

		public ValueTask<RegularExpressionCompileResult> CompileAsync(
			string pattern,
			RegularExpressionOptions? options = null,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult( this.Compile( pattern, options, cancellationToken ) );
	}

	private sealed class FailingLocaleProvider : IExpressionLocaleProvider {
		private readonly SystemExpressionLocaleProvider text = new( CultureInfo.InvariantCulture );

		public int Compare(
			string left,
			string right,
			CancellationToken cancellationToken = default
		) {
			_ = left;
			_ = right;
			cancellationToken.ThrowIfCancellationRequested();
			throw new InvalidOperationException( "simulated collation failure" );
		}

		public BigInteger GetLength(
			string value,
			CancellationToken cancellationToken = default
		) => this.text.GetLength( value, cancellationToken );

		public BigInteger IndexOfAny(
			string value,
			string characterSet,
			CancellationToken cancellationToken = default
		) => this.text.IndexOfAny( value, characterSet, cancellationToken );

		public string Substring(
			string value,
			BigInteger position,
			BigInteger length,
			CancellationToken cancellationToken = default
		) => this.text.Substring( value, position, length, cancellationToken );
	}

	private sealed class ThrowingTextWriter : TextWriter {
		public override Encoding Encoding => Encoding.UTF8;

		public override Task WriteLineAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			_ = buffer;
			_ = cancellationToken;
			return Task.FromException( new IOException( "simulated output failure" ) );
		}
	}

	private sealed class TrackingTextWriter : StringWriter {
		public bool WasDisposed { get; private set; }

		protected override void Dispose( bool disposing ) {
			this.WasDisposed = true;
			base.Dispose( disposing );
		}
	}
}
