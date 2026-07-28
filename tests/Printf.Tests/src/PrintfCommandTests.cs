namespace Icod.CoreUtils.Printf.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

public sealed class PrintfCommandTests {
	[Fact]
	public async Task LiteralTextAndEscapesAreWrittenWithoutImplicitNewline() {
		var result = await RunAsync( [ "hello\\nworld" ] );
		Assert.Equal( 0, result.Status );
		Assert.Equal( "hello\nworld", result.Output );
	}

	[Fact]
	public async Task FormatIsReusedUntilArgumentsAreConsumed() {
		var result = await RunAsync( [ "%s ", "one", "two", "three" ] );
		Assert.Equal( "one two three ", result.Output );
	}

	[Theory]
	[InlineData( "%d", "42", "42" )]
	[InlineData( "%04d", "42", "0042" )]
	[InlineData( "%+d", "42", "+42" )]
	[InlineData( "%#x", "31", "0x1f" )]
	[InlineData( "%#o", "8", "010" )]
	[InlineData( "%.4x", "15", "000f" )]
	public async Task IntegerConversionsFollowPrintfRules( string format, string argument, string expected ) {
		var result = await RunAsync( [ format, argument ] );
		Assert.Equal( expected, result.Output );
	}

	[Fact]
	public async Task DynamicWidthAndPrecisionConsumeArguments() {
		var result = await RunAsync( [ "%*.*s", "6", "3", "abcdef" ] );
		Assert.Equal( "   abc", result.Output );
	}

	[Fact]
	public async Task PositionalArgumentsCanBeReordered() {
		var result = await RunAsync( [ "%2$s %1$s", "first", "second" ] );
		Assert.Equal( "second first", result.Output );
	}

	[Fact]
	public async Task MissingStringAndNumericArgumentsUseEmptyAndZero() {
		var result = await RunAsync( [ "[%s][%d]" ] );
		Assert.Equal( "[][0]", result.Output );
	}

	[Fact]
	public async Task PercentBExpandsEscapes() {
		var result = await RunAsync( [ "%b", "a\\tb\\n" ] );
		Assert.Equal( "a\tb\n", result.Output );
	}

	[Fact]
	public async Task PercentBBackslashCStopsAllFurtherOutput() {
		var result = await RunAsync( [ "%b-after", "before\\cignored", "unused" ] );
		Assert.Equal( "before", result.Output );
	}

	[Fact]
	public async Task UnicodeEscapesProduceScalars() {
		var result = await RunAsync( [ "\\u03bb \\U0001f600" ] );
		Assert.Equal( "λ 😀", result.Output );
	}

	[Fact]
	public async Task CharacterConversionUsesFirstUnicodeScalar() {
		var result = await RunAsync( [ "%c", "😀x" ] );
		Assert.Equal( "😀", result.Output );
	}

	[Fact]
	public async Task NumericCharacterConstantUsesScalarValue() {
		var result = await RunAsync( [ "%d", "'A" ] );
		Assert.Equal( "65", result.Output );
	}

	[Fact]
	public async Task ShellQuoteProtectsWhitespace() {
		var result = await RunAsync( [ "%q", "a b" ] );
		Assert.Equal( "'a b'", result.Output );
	}

	[Fact]
	public async Task InvalidNumericOperandReportsFailureAndPrintsZero() {
		var result = await RunAsync( [ "%d", "nope" ] );
		Assert.Equal( 1, result.Status );
		Assert.Equal( "0", result.Output );
		Assert.Contains( "expected a numeric value", result.Error );
	}

	[Fact]
	public async Task InvalidConversionReportsFailure() {
		var result = await RunAsync( [ "%Z", "1" ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "invalid conversion", result.Error );
	}

	[Fact]
	public async Task LoneHelpAndVersionOptionsAreRecognized() {
		var help = await RunAsync( [ "--help" ] );
		var version = await RunAsync( [ "--version" ] );
		Assert.Contains( "Usage: printf", help.Output );
		Assert.Contains( "Icod.CoreUtils", version.Output );
	}

	[Fact]
	public async Task DoubleDashAllowsOptionLookingFormat() {
		var result = await RunAsync( [ "--", "--help" ] );
		Assert.Equal( "--help", result.Output );
	}

	[Fact]
	public async Task MissingFormatIsAnError() {
		var result = await RunAsync( [] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "missing operand", result.Error );
	}

	[Fact]
	public async Task OutputFailureReturnsFailure() {
		var error = new StringWriter();
		var status = await Command.RunAsync( [ "%s", "x" ], new ThrowingTextWriter(), error );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "write failed", error.ToString() );
	}

	[Fact]
	public async Task CancellationReturnsConventionalStatus() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await Command.RunAsync( [ "%s", "x" ], output, error, source.Token );
		Assert.Equal( CommandExitCodes.Canceled, status );
	}

	private sealed class ThrowingTextWriter : TextWriter {
		public override Encoding Encoding => Encoding.UTF8;

		public override Task WriteAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			return Task.FromException( new IOException( "write failed" ) );
		}
	}

	private static async Task<(int Status, string Output, string Error)> RunAsync( string[] args ) {
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await Command.RunAsync( args, output, error );
		return ( status, output.ToString(), error.ToString() );
	}
}
