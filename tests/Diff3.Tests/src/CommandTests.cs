namespace Icod.DiffUtils.Diff3.Tests;

using System.Text;
using Icod.DiffUtils.Diff3;
using Xunit;

/// <summary>Exercises GNU-compatible <c>diff3</c> command behavior.</summary>
public sealed class CommandTests {
	private static readonly string Nl = Environment.NewLine;

	/// <summary>The default report identifies the first input as the odd file.</summary>
	[Fact]
	public async Task NormalReportClassifiesMineOnlyChange() {
		using var fixture = new FileFixture();
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "base\n" );
		var yours = fixture.Write( "yours", "base\n" );
		var result = await RunAsync( mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal(
			$"====1{Nl}1:1c{Nl}  mine{Nl}2:1c{Nl}3:1c{Nl}  base{Nl}",
			result.Output
		);
		Assert.Equal( string.Empty, result.Error );
	}

	/// <summary>The historical normal report uses the third input as its pairwise common file.</summary>
	[Fact]
	public async Task NormalReportUsesThirdInputForRepeatedLineAlignment() {
		var mine = fixture.Write( "mine", "A\n" );
		var older = fixture.Write( "older", "A\nA\n" );
		var yours = fixture.Write( "yours", "B\nA\n" );
		var result = await RunAsync( mine, older, yours );
		Assert.Equal(
			$"===={Nl}1:0a{Nl}2:1c{Nl}  A{Nl}3:1c{Nl}  B{Nl}",
			result.Output
		);
	}

	/// <summary>Nonoverlapping changes from the third file are merged without conflict.</summary>
	[Fact]
	public async Task MergeAppliesYoursOnlyChange() {
		var mine = fixture.Write( "mine", "base\n" );
		var older = fixture.Write( "older", "base\n" );
		var yours = fixture.Write( "yours", "new\n" );
		var result = await RunAsync( "-m", mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal( $"new{Nl}", result.Output );
	}

	/// <summary>Default merge mode marks a true overlap with all three labels.</summary>
	[Fact]
	public async Task MergeMarksThreeWayConflict() {
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "yours\n" );
		var result = await RunAsync( "-m", "-L", "M", "-L", "O", "-L", "Y", mine, older, yours );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"<<<<<<< M{Nl}mine{Nl}||||||| O{Nl}old{Nl}======={Nl}yours{Nl}>>>>>>> Y{Nl}",
			result.Output
		);
	}

	/// <summary>Show-overlap merge mode omits the ancestor body.</summary>
	[Fact]
	public async Task ShowOverlapMergeUsesTwoWayMarkers() {
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "yours\n" );
		var result = await RunAsync( "-mE", "-L", "M", "-L", "O", "-L", "Y", mine, older, yours );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"<<<<<<< M{Nl}mine{Nl}======={Nl}yours{Nl}>>>>>>> Y{Nl}",
			result.Output
		);
	}

	/// <summary>Show-all treats an already-applied descendant change as a conflict with the ancestor.</summary>
	[Fact]
	public async Task ShowAllMarksOlderOnlyRegion() {
		var mine = fixture.Write( "mine", "same\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "same\n" );
		var result = await RunAsync( "-mA", "-L", "M", "-L", "O", "-L", "Y", mine, older, yours );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"<<<<<<< O{Nl}old{Nl}======={Nl}same{Nl}>>>>>>> Y{Nl}",
			result.Output
		);
	}

	/// <summary>Show-overlap ignores an already-applied descendant change.</summary>
	[Fact]
	public async Task ShowOverlapIgnoresOlderOnlyRegion() {
		var mine = fixture.Write( "mine", "same\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "same\n" );
		var result = await RunAsync( "-mE", mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal( $"same{Nl}", result.Output );
	}

	/// <summary>The ordinary ed mode writes a reverse-order change command.</summary>
	[Fact]
	public async Task EdModeWritesChangeCommand() {
		var mine = fixture.Write( "mine", "base\n" );
		var older = fixture.Write( "older", "base\n" );
		var yours = fixture.Write( "yours", "new\n" );
		var result = await RunAsync( "-e", mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal( $"1c{Nl}new{Nl}.{Nl}", result.Output );
	}

	/// <summary>Marked ed conflicts preserve mine and append marker blocks around it.</summary>
	[Fact]
	public async Task ShowAllEdModeWritesSplitConflictCommands() {
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "yours\n" );
		var result = await RunAsync( "-A", "-L", "M", "-L", "O", "-L", "Y", mine, older, yours );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"1a{Nl}||||||| O{Nl}old{Nl}======={Nl}yours{Nl}>>>>>>> Y{Nl}.{Nl}0a{Nl}<<<<<<< M{Nl}.{Nl}",
			result.Output
		);
	}

	/// <summary>Marked-overlap-only mode brackets true overlaps while leaving other regions untouched.</summary>
	[Fact]
	public async Task MarkedOverlapOnlyWritesConflictMarkers() {
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "yours\n" );
		var result = await RunAsync( "-X", "-L", "M", "-L", "O", "-L", "Y", mine, older, yours );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"1a{Nl}======={Nl}yours{Nl}>>>>>>> Y{Nl}.{Nl}0a{Nl}<<<<<<< M{Nl}.{Nl}",
			result.Output
		);
	}

	/// <summary>System V compatibility appends write and quit commands to an ed script.</summary>
	[Fact]
	public async Task InitialWriteOptionAppendsWriteAndQuit() {
		var mine = fixture.Write( "mine", "base\n" );
		var older = fixture.Write( "older", "base\n" );
		var yours = fixture.Write( "yours", "new\n" );
		var result = await RunAsync( "-ie", mine, older, yours );
		Assert.Equal( $"1c{Nl}new{Nl}.{Nl}w{Nl}q{Nl}", result.Output );
	}

	/// <summary>Ed payloads escape leading periods and then restore them with a substitution.</summary>
	[Fact]
	public async Task EdModeEscapesLeadingPeriods() {
		var mine = fixture.Write( "mine", "base\n" );
		var older = fixture.Write( "older", "base\n" );
		var yours = fixture.Write( "yours", ".value\n" );
		var result = await RunAsync( "-e", mine, older, yours );
		Assert.Equal( $"1c{Nl}..value{Nl}.{Nl}1s/^\\.//{Nl}", result.Output );
	}

	/// <summary>Ed mode diagnoses an incomplete replacement line but emits a usable script.</summary>
	[Fact]
	public async Task EdModeWarnsForIncompleteLine() {
		var mine = fixture.Write( "mine", "base\n" );
		var older = fixture.Write( "older", "base\n" );
		var yours = fixture.Write( "yours", "new" );
		var result = await RunAsync( "-e", mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal( $"1c{Nl}new{Nl}.{Nl}", result.Output );
		Assert.Equal( $"diff3: No newline at end of file{Nl}", result.Error );
	}

	/// <summary>Normal reports identify incomplete final lines for every displayed input.</summary>
	[Fact]
	public async Task NormalReportMarksIncompleteFinalLines() {
		var mine = fixture.Write( "mine", "mine" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "yours" );
		var result = await RunAsync( mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal(
			$"===={Nl}1:1c{Nl}  mine{Nl}\\ No newline at end of file{Nl}"
			+ $"2:1c{Nl}  old{Nl}3:1c{Nl}  yours{Nl}\\ No newline at end of file{Nl}",
			result.Output
		);
	}

	/// <summary>Initial-tab mode changes normal-report content indentation.</summary>
	[Fact]
	public async Task InitialTabAlignsNormalReportContent() {
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "old\n" );
		var yours = fixture.Write( "yours", "yours\n" );
		var result = await RunAsync( "-T", mine, older, yours );
		Assert.Contains( $"1:1c{Nl}\tmine{Nl}", result.Output, StringComparison.Ordinal );
	}

	/// <summary>Trailing carriage returns are ignored and stripped from changed replacement lines.</summary>
	[Fact]
	public async Task StripTrailingCarriageReturnAffectsComparisonAndMerge() {
		var mine = fixture.Write( "mine", "same\r\nold\r\n" );
		var older = fixture.Write( "older", "same\nold\n" );
		var yours = fixture.Write( "yours", "same\nyours\n" );
		var result = await RunAsync( "-m", "--strip-trailing-cr", mine, older, yours );
		Assert.Equal( 0, result.Status );
		Assert.Equal( $"same\r{Nl}yours{Nl}", result.Output );
	}

	/// <summary>Binary inputs require explicit text mode.</summary>
	[Fact]
	public async Task BinaryInputsRequireTextMode() {
		var mine = fixture.WriteBytes( "mine", new byte[] { (byte)'m', 0, (byte)'\n' } );
		var older = fixture.WriteBytes( "older", new byte[] { (byte)'o', 0, (byte)'\n' } );
		var yours = fixture.WriteBytes( "yours", new byte[] { (byte)'y', 0, (byte)'\n' } );
		var result = await RunAsync( mine, older, yours );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "binary input differs", result.Error, StringComparison.Ordinal );
		var forced = await RunAsync( "-a", mine, older, yours );
		Assert.Equal( 0, forced.Status );
		Assert.Contains( '\0', forced.Output );
	}

	/// <summary>At most one input may be standard input.</summary>
	[Fact]
	public async Task RejectsRepeatedStandardInput() {
		var result = await RunWithInputAsync( "value\n", "-", "-", "third" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "standard input may be specified only once", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Standard input may provide any one operand.</summary>
	[Fact]
	public async Task ReadsThirdOperandFromStandardInput() {
		var mine = fixture.Write( "mine", "mine\n" );
		var older = fixture.Write( "older", "base\n" );
		var result = await RunWithInputAsync( "base\n", mine, older, "-" );
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( $"====1{Nl}", result.Output, StringComparison.Ordinal );
	}

	/// <summary>Mutually exclusive edit policies are rejected.</summary>
	[Theory]
	[InlineData( "-Ae", "incompatible options" )]
	[InlineData( "-mA3", "incompatible options" )]
	[InlineData( "-mi", "options -i and -m are incompatible" )]
	public async Task RejectsIncompatibleOptions( string option, string expected ) {
		var first = fixture.Write( "first", "a\n" );
		var second = fixture.Write( "second", "a\n" );
		var third = fixture.Write( "third", "a\n" );
		var result = await RunAsync( option, first, second, third );
		Assert.Equal( 2, result.Status );
		Assert.Contains( expected, result.Error, StringComparison.Ordinal );
	}

	/// <summary>Exactly three operands are required.</summary>
	[Fact]
	public async Task RejectsMissingAndExtraOperands() {
		var missing = await RunAsync( "first", "second" );
		Assert.Equal( 2, missing.Status );
		Assert.Contains( "missing operand", missing.Error, StringComparison.Ordinal );
		var extra = await RunAsync( "first", "second", "third", "fourth" );
		Assert.Equal( 2, extra.Status );
		Assert.Contains( "extra operand", extra.Error, StringComparison.Ordinal );
	}

	/// <summary>Labels are accepted only by conflict-marking modes and at most three may be supplied.</summary>
	[Fact]
	public async Task ValidatesConflictLabels() {
		var wrongMode = await RunAsync( "-L", "label", "first", "second", "third" );
		Assert.Equal( 2, wrongMode.Status );
		Assert.Contains( "file labels require", wrongMode.Error, StringComparison.Ordinal );
		var tooMany = await RunAsync(
			"-A", "-L", "one", "-L", "two", "-L", "three", "-L", "four",
			"first", "second", "third"
		);
		Assert.Equal( 2, tooMany.Status );
		Assert.Contains( "too many file label options", tooMany.Error, StringComparison.Ordinal );
	}

	/// <summary>The implementation rejects external comparison programs rather than creating a tool dependency.</summary>
	[Fact]
	public async Task RejectsExternalDiffProgram() {
		var result = await RunAsync( "--diff-program=diff", "first", "second", "third" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "not supported", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Help and version are successful terminal actions.</summary>
	[Theory]
	[InlineData( "--help", "Usage: diff3" )]
	[InlineData( "--version", "diff3 (Icod.DiffUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var result = await RunAsync( option );
		Assert.Equal( 0, result.Status );
		Assert.Contains( expected, result.Output, StringComparison.Ordinal );
	}

	/// <summary>Pre-cancellation returns the command-framework cancellation status.</summary>
	[Fact]
	public async Task PreCanceledCommandReturnsCanceledStatus() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await Command.RunAsync(
			new[] { "-", "second", "third" },
			new StringReader( "value" ),
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, status );
	}

	private static Task<CommandResult> RunAsync( params string[] arguments ) {
		return RunWithInputAsync( string.Empty, arguments );
	}

	private static async Task<CommandResult> RunWithInputAsync( string input, params string[] arguments ) {
		var output = new StringWriter();
		var error = new StringWriter();
		await using var stream = new MemoryStream( Encoding.UTF8.GetBytes( input ) );
		var status = await Command.RunAsync(
			arguments,
			new StringReader( input ),
			output,
			error,
			stdinStream: stream
		);
		return new CommandResult( status, output.ToString(), error.ToString() );
	}

	private sealed record CommandResult( int Status, string Output, string Error );

	private sealed class FileFixture : IDisposable {
		/// <summary>Initializes a temporary filesystem fixture.</summary>
		public FileFixture() {
			this.Root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"Icod.DiffUtils.Diff3.Tests-{Guid.NewGuid():N}" );
			Directory.CreateDirectory( this.Root );
		}

		/// <summary>Gets the fixture root.</summary>
		public string Root { get; }

		/// <summary>Writes UTF-8 text and returns its path.</summary>
		public string Write( string name, string content ) {
			var path = System.IO.Path.Combine( this.Root, name );
			File.WriteAllText( path, content, new UTF8Encoding( false ) );
			return path;
		}

		/// <summary>Writes bytes and returns its path.</summary>
		public string WriteBytes( string name, byte[] bytes ) {
			var path = System.IO.Path.Combine( this.Root, name );
			File.WriteAllBytes( path, bytes );
			return path;
		}

		/// <inheritdoc/>
		public void Dispose() {
			try {
				Directory.Delete( this.Root, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}
}
