namespace Icod.CoreUtils.NumFmt.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

public sealed class NumFmtCommandTests {
	[Theory]
	[InlineData( "--to=si", "1000", "1.0k" )]
	[InlineData( "--to=iec", "2048", "2.0K" )]
	[InlineData( "--to=iec-i", "4096", "4.0Ki" )]
	[InlineData( "--from=si", "1K", "1000" )]
	[InlineData( "--from=iec", "1K", "1024" )]
	[InlineData( "--from=auto", "1Ki", "1024" )]
	public async Task DocumentedScaleExamplesAreSupported( string option, string operand, string expected ) {
		var result = await RunAsync( [ option, operand ] );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Concat( expected, Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task MultipleOperandsProduceSeparateRecords() {
		var result = await RunAsync( [ "1", "2", "3" ] );
		Assert.Equal( string.Concat( "1", Environment.NewLine, "2", Environment.NewLine, "3", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task StandardInputDefaultFieldIsConverted() {
		var result = await RunAsync( [ "--to=si" ], "1000 apples\n2000 pears\n" );
		Assert.Equal( string.Concat( "1.0k apples", Environment.NewLine, "2.0k pears", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task FieldRangesSelectMultipleColumns() {
		var result = await RunAsync( [ "--field=2-3", "--to=si" ], "x 1000 2000 y\n" );
		Assert.Equal( string.Concat( "x 1.0k 2.0k y", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task DelimiterPreservesEmptyFields() {
		var result = await RunAsync( [ "-d,", "--field=2", "--to=si" ], "x,1000,,y\n" );
		Assert.Equal( string.Concat( "x,1.0k,,y", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task HeaderRecordsPassThroughUnchanged() {
		var result = await RunAsync( [ "--header", "--to=si" ], "size\n1000\n" );
		Assert.Equal( string.Concat( "size", Environment.NewLine, "1.0k", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task ExplicitHeaderCountIsSupported() {
		var result = await RunAsync( [ "--header=2", "--to=si" ], "one\ntwo\n1000\n" );
		Assert.Equal( string.Concat( "one", Environment.NewLine, "two", Environment.NewLine, "1.0k", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task SuffixAndUnitSeparatorAreAcceptedAndEmitted() {
		var result = await RunAsync( [ "--suffix=B", "--unit-separator= ", "--to=si", "1000 B" ] );
		Assert.Equal( string.Concat( "1.0 kB", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task FromAndToUnitsApplyExactScaling() {
		var from = await RunAsync( [ "--from-unit=1000", "1.5" ] );
		var to = await RunAsync( [ "--to-unit=1000", "1500" ] );
		Assert.Equal( string.Concat( "1500.0", Environment.NewLine ), from.Output );
		Assert.Equal( string.Concat( "2", Environment.NewLine ), to.Output );
	}

	[Theory]
	[InlineData( "up", "2" )]
	[InlineData( "down", "1" )]
	[InlineData( "from-zero", "2" )]
	[InlineData( "towards-zero", "1" )]
	[InlineData( "nearest", "2" )]
	public async Task RoundingModesControlScaledIntegralOutput( string mode, string expected ) {
		var result = await RunAsync( [ string.Concat( "--round=", mode ), "--to-unit=10", "15" ] );
		Assert.Equal( string.Concat( expected, Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task NegativeRoundingDirectionsAreDistinct() {
		var up = await RunAsync( [ "--round=up", "--to-unit=10", "--", "-15" ] );
		var down = await RunAsync( [ "--round=down", "--to-unit=10", "--", "-15" ] );
		Assert.Equal( CommandExitCodes.Success, up.Status );
		Assert.Equal( CommandExitCodes.Success, down.Status );
		Assert.Empty( up.Error );
		Assert.Empty( down.Error );
		Assert.Equal( string.Concat( "-1", Environment.NewLine ), up.Output );
		Assert.Equal( string.Concat( "-2", Environment.NewLine ), down.Output );
	}

	[Fact]
	public async Task PaddingCanAlignBothDirections() {
		var right = await RunAsync( [ "--padding=5", "12" ] );
		var left = await RunAsync( [ "--padding=-5", "12" ] );
		Assert.Equal( string.Concat( "   12", Environment.NewLine ), right.Output );
		Assert.Equal( string.Concat( "12   ", Environment.NewLine ), left.Output );
	}

	[Fact]
	public async Task CustomFormatSupportsPrefixWidthPrecisionAndSuffix() {
		var result = await RunAsync( [ "--format=[%06.1f]", "12.34" ] );
		Assert.Equal( string.Concat( "[0012.4]", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task ZeroTerminatedModeUsesNulRecords() {
		var result = await RunAsync( [ "--zero-terminated", "1", "2" ] );
		Assert.Equal( string.Concat( "1", '\0', "2", '\0' ), result.Output );
	}

	[Fact]
	public async Task InvalidAbortStopsWithStatusTwo() {
		var result = await RunAsync( [ "bad", "2" ] );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "invalid number", result.Error );
	}

	[Fact]
	public async Task InvalidFailContinuesAndReturnsTwo() {
		var result = await RunAsync( [ "--invalid=fail", "bad", "2" ] );
		Assert.Equal( 2, result.Status );
		Assert.Equal( string.Concat( "bad", Environment.NewLine, "2", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task InvalidWarnContinuesAndReturnsZero() {
		var result = await RunAsync( [ "--invalid=warn", "bad" ] );
		Assert.Equal( 0, result.Status );
		Assert.NotEmpty( result.Error );
	}

	[Fact]
	public async Task InvalidIgnoreIsSilent() {
		var result = await RunAsync( [ "--invalid=ignore", "bad" ] );
		Assert.Equal( 0, result.Status );
		Assert.Empty( result.Error );
	}


	[Theory]
	[InlineData( "--padding=0" )]
	[InlineData( "--header=0" )]
	public async Task ZeroValuedStructuralOptionsAreRejected( string option ) {
		var result = await RunAsync( [ option, "1" ] );
		Assert.Equal( 1, result.Status );
		Assert.NotEmpty( result.Error );
	}

	[Fact]
	public async Task MultipleFieldOptionsAreRejected() {
		var result = await RunAsync( [ "--field=1", "--field=2", "1" ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "multiple field", result.Error );
	}

	[Theory]
	[InlineData( "--grouping", "--format=%f" )]
	[InlineData( "--grouping", "--to=si" )]
	public async Task IncompatibleGroupingOptionsAreRejected( string first, string second ) {
		var result = await RunAsync( [ first, second, "1000" ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "grouping", result.Error );
	}

	[Fact]
	public async Task UnitSeparatorIsOnlyInsertedBeforeScaledUnits() {
		var result = await RunAsync( [ "--suffix=B", "--unit-separator= ", "100" ] );
		Assert.Equal( string.Concat( "100B", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task ExplicitEmptyUnitSeparatorRejectsBlankBeforeScaleSuffix() {
		var result = await RunAsync( [ "--from=si", "--unit-separator=", "1 K" ] );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "invalid suffix", result.Error );
	}

	[Fact]
	public async Task ScaleSuffixMayBeFollowedByWhitespace() {
		var result = await RunAsync( [ "--from=si", "1K " ] );
		Assert.Equal( string.Concat( "1000", Environment.NewLine ), result.Output );
	}

	[Fact]
	public async Task UnterminatedFinalInputRecordRemainsUnterminated() {
		var result = await RunAsync( [ "--to=si" ], "1000\n2000" );
		Assert.Equal( string.Concat( "1.0k", Environment.NewLine, "2.0k" ), result.Output );
	}

	[Fact]
	public async Task CancellationReturnsConventionalStatus() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await Command.RunAsync( [], new StringReader( "1\n" ), output, error, source.Token );
		Assert.Equal( CommandExitCodes.Canceled, status );
	}

	[Fact]
	public async Task OutputFailureReturnsFailure() {
		var error = new StringWriter();
		var status = await Command.RunAsync( [ "1" ], new StringReader( string.Empty ), new ThrowingTextWriter(), error );
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "write failed", error.ToString() );
	}

	[Fact]
	public async Task HelpAndVersionAreAvailable() {
		var help = await RunAsync( [ "--help" ] );
		var version = await RunAsync( [ "--version" ] );
		Assert.Contains( "Usage: numfmt", help.Output );
		Assert.Contains( "Icod.CoreUtils", version.Output );
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

	private static Task<(int Status, string Output, string Error)> RunAsync( string[] args ) {
		return RunAsync( args, string.Empty );
	}

	private static async Task<(int Status, string Output, string Error)> RunAsync( string[] args, string input ) {
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await Command.RunAsync( args, new StringReader( input ), output, error );
		return ( status, output.ToString(), error.ToString() );
	}
}
