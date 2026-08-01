namespace Icod.CoreUtils.Pr.Tests;

using System.Globalization;
using Icod.CoreUtils.Shared.Diagnostics;
using Xunit;

/// <summary>Tests GNU-compatible <c>pr</c> pagination, layout, and option behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies deterministic header geometry and the default trailer fill.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task HeaderAndPageGeometryAreDeterministic() {
		var result = await RunAsync( [ "-l", "12", "-D", "DATE", "-h", "TITLE" ], "line" );
		var lines = result.Output.Split( Environment.NewLine );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( 13, lines.Length );
		Assert.Contains( "DATE", lines[2] );
		Assert.Contains( "TITLE", lines[2] );
		Assert.EndsWith( "Page 1", lines[2] );
		Assert.Equal( "line", lines[5] );
	}

	/// <summary>Verifies that header dates use the shared GNU formatter.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task DateFormatUsesSharedGnuFormatter() {
		var directory = CreateTemporaryDirectory();
		try {
			var path = Path.Combine( directory, "dated.txt" );
			await File.WriteAllTextAsync( path, Lines( "x" ) );
			File.SetLastWriteTime( path, new DateTime( 2001, 2, 3, 4, 5, 0, DateTimeKind.Local ) );
			var result = await RunAsync( [ "-l", "12", "-D", "%F %R", path ], string.Empty );
			Assert.Contains( "2001-02-03 04:05", result.Output.Split( Environment.NewLine )[2] );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies ordinary unpaginated output.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task OmitPaginationWritesInputLinesOnly() {
		var result = await RunAsync( [ "-T" ], Lines( "one", "two" ) );
		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( Lines( "one", "two" ), result.Output );
	}

	/// <summary>Verifies balanced down-column output and historical column syntax.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task ColumnsPrintDownByDefault() {
		var result = await RunAsync( [ "-T", "-2", "-s|" ], Lines( "1", "2", "3", "4", "5" ) );
		var uneven = await RunAsync( [ "-T", "-3", "-s|" ], Lines( "1", "2", "3", "4" ) );
		Assert.Equal( Lines( "1|4", "2|5", "3" ), result.Output );
		Assert.Equal( Lines( "1|3|4", "2" ), uneven.Output );
	}

	/// <summary>Verifies across-column output and a harmless one-column across option.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task AcrossPrintsRowsWithoutTrailingEmptyColumns() {
		var result = await RunAsync( [ "-T", "-a", "-2", "-s|" ], Lines( "1", "2", "3", "4", "5" ) );
		var clustered = await RunAsync( [ "-T", "-2a3", "-s|" ], Lines( "1", "2", "3", "4", "5" ) );
		var oneColumn = await RunAsync( [ "-T", "-a", "-1" ], Lines( "x", "y" ) );
		Assert.Equal( Lines( "1|2", "3|4", "5" ), result.Output );
		Assert.Equal( Lines( "1|2|3", "4|5" ), clustered.Output );
		Assert.Equal( Lines( "x", "y" ), oneColumn.Output );
	}

	/// <summary>Verifies parallel file merging and its filename-free default header.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task MergePrintsFilesInParallel() {
		var directory = CreateTemporaryDirectory();
		try {
			var left = Path.Combine( directory, "left.txt" );
			var right = Path.Combine( directory, "right.txt" );
			await File.WriteAllTextAsync( left, Lines( "a", "b" ) );
			await File.WriteAllTextAsync( right, Lines( "c" ) );
			var result = await RunAsync( [ "-T", "-m", "-s|", left, right ], string.Empty );
			Assert.Equal( Lines( "a|c", "b|" ), result.Output );

			File.SetLastWriteTime( left, new DateTime( 2000, 1, 2, 3, 4, 0 ) );
			var headed = await RunAsync( [ "-m", "-l", "12", "-D", "%Y", left, right ], string.Empty );
			var header = headed.Output.Split( Environment.NewLine )[2];
			var repeatedInput = await RunAsync(
				[ "-T", "-m", "-s|", "-", "-" ],
				Lines( "1", "2", "3", "4", "5" )
			);
			Assert.Contains( DateTime.Now.Year.ToString( CultureInfo.InvariantCulture ), header );
			Assert.DoesNotContain( "2000", header );
			Assert.DoesNotContain( "left.txt", header );
			Assert.DoesNotContain( "right.txt", header );
			Assert.Equal( Lines( "1|2", "3|4", "5|" ), repeatedInput.Output );
		} finally {
			Directory.Delete( directory, recursive: true );
		}
	}

	/// <summary>Verifies the different alignment effects of separator options.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task SeparatorAndSeparatorStringRemainDistinct() {
		var unaligned = await RunAsync( [ "-T", "-2", "-s|" ], Lines( "a", "bb" ) );
		var multiCharacter = await RunAsync( [ "-T", "-2", "-sXY" ], Lines( "a", "bb" ) );
		var aligned = await RunAsync( [ "-T", "-2", "-S|", "-W", "10" ], Lines( "a", "bb" ) );
		Assert.Equal( string.Concat( "a|bb", Environment.NewLine ), unaligned.Output );
		Assert.Equal( string.Concat( "aXYbb", Environment.NewLine ), multiCharacter.Output );
		Assert.Contains( "|", aligned.Output );
		Assert.NotEqual( unaligned.Output, aligned.Output );
	}

	/// <summary>Verifies retained, eliminated, and exact-page form-feed behavior.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task InputFormFeedsHonorPaginationMode() {
		var retained = await RunAsync( [ "-t" ], string.Concat( "a\f", "b", Environment.NewLine ) );
		var eliminated = await RunAsync( [ "-T" ], string.Concat( "a\f", "b", Environment.NewLine ) );
		var exact = await RunAsync(
			[ "-t", "-l", "2" ],
			string.Concat( "a", Environment.NewLine, "b", Environment.NewLine, "\f", "c", Environment.NewLine )
		);
		var isolated = await RunAsync( [ "-T" ], "\f" );
		var adjacent = await RunAsync(
			[ "-T", "-l", "2" ],
			string.Concat( "a", Environment.NewLine, "b", Environment.NewLine, "\f", Environment.NewLine, "c", Environment.NewLine )
		);
		Assert.Equal( string.Concat( "a", Environment.NewLine, "\f", "b", Environment.NewLine ), retained.Output );
		Assert.Equal( Lines( "a", "b" ), eliminated.Output );
		Assert.Equal( Lines( "a", "b", "c" ), exact.Output );
		Assert.Empty( isolated.Output );
		Assert.Equal( Lines( "a", "b", "c" ), adjacent.Output );
	}

	/// <summary>Verifies explicit form-feed page separation.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task FormFeedOptionTerminatesEveryPrintedPage() {
		var result = await RunAsync( [ "-f", "-l", "11", "-D", "DATE", "-h", "H" ], Lines( "a", "b" ) );
		Assert.Equal( 2, result.Output.Count( character => '\f' == character ) );
	}

	/// <summary>Verifies double spacing and its effect on page body capacity.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task DoubleSpacingAddsBlankPhysicalLines() {
		var result = await RunAsync( [ "-T", "-d" ], Lines( "a", "b" ) );
		Assert.Equal(
			string.Concat( "a", Environment.NewLine, Environment.NewLine, "b", Environment.NewLine, Environment.NewLine ),
			result.Output
		);
	}

	/// <summary>Verifies line-number syntax and negative first-number values.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task NumberingSupportsCustomAndNegativeStarts() {
		var custom = await RunAsync( [ "-T", "-n:3" ], Lines( "a", "b" ) );
		var negative = await RunAsync( [ "-T", "-n", "-N", "-2" ], Lines( "a", "b" ) );
		var narrow = await RunAsync( [ "-T", "-n", "-W", "1" ], Lines( "abc" ) );
		Assert.Equal( Lines( "  1:a", "  2:b" ), custom.Output );
		Assert.StartsWith( "   -2\t", negative.Output );
		Assert.Equal( Lines( "    1\ta" ), narrow.Output );
	}

	/// <summary>Verifies historical page selection and numbering at the first printed page.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task SelectedPageStartsExplicitNumbering() {
		var result = await RunAsync(
			[ "+2:2", "-l", "11", "-D", "DATE", "-h", "H", "-n", "-N", "-2" ],
			Lines( "first", "second", "third" )
		);
		Assert.DoesNotContain( "first", result.Output );
		Assert.Contains( "   -2\tsecond", result.Output );
		Assert.DoesNotContain( "third", result.Output );
	}

	/// <summary>Verifies control-character rendering and <c>-v</c> precedence.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task ControlRenderingUsesHatOrOctalNotation() {
		var hats = await RunAsync( [ "-T", "-c" ], string.Concat( "a\b", Environment.NewLine ) );
		var octal = await RunAsync( [ "-T", "-c", "-v" ], string.Concat( "a\b", Environment.NewLine ) );
		Assert.Equal( string.Concat( "a^H", Environment.NewLine ), hats.Output );
		Assert.Equal( string.Concat( "a\\010", Environment.NewLine ), octal.Output );
	}

	/// <summary>Verifies input expansion, indentation, truncation, and joined full lines.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task TabsMarginsAndWidthsAreApplied() {
		var expanded = await RunAsync( [ "-T", "-e4" ], string.Concat( "a\tb", Environment.NewLine ) );
		var indented = await RunAsync( [ "-T", "-o", "3" ], Lines( "x" ) );
		var truncated = await RunAsync( [ "-T", "-W", "4" ], Lines( "abcdef" ) );
		var joined = await RunAsync( [ "-T", "-W", "4", "-J" ], Lines( "abcdef" ) );
		Assert.Equal( string.Concat( "a   b", Environment.NewLine ), expanded.Output );
		Assert.Equal( string.Concat( "   x", Environment.NewLine ), indented.Output );
		Assert.Equal( string.Concat( "abcd", Environment.NewLine ), truncated.Output );
		Assert.Equal( string.Concat( "abcdef", Environment.NewLine ), joined.Output );
	}

	/// <summary>Verifies file warning suppression without changing failure status.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task NoFileWarningsSuppressesOnlyTheDiagnostic() {
		var missing = Path.Combine( Path.GetTempPath(), string.Concat( Guid.NewGuid(), ".missing" ) );
		var ordinary = await RunAsync( [ missing ], string.Empty );
		var quiet = await RunAsync( [ "-r", missing ], string.Empty );
		Assert.Equal( CommandExitCodes.Failure, ordinary.Status );
		Assert.NotEmpty( ordinary.Error );
		Assert.Equal( CommandExitCodes.Failure, quiet.Status );
		Assert.Empty( quiet.Error );
	}

	/// <summary>Verifies help, version, invalid input, and cancellation statuses.</summary>
	/// <returns>A task representing the asynchronous test.</returns>
	[Fact]
	public async Task ControlPathsUseConventionalStatuses() {
		var help = await RunAsync( [ "--help" ], string.Empty );
		var version = await RunAsync( [ "--version" ], string.Empty );
		var invalid = await RunAsync( [ "--columns=0" ], string.Empty );
		var conflicting = await RunAsync( [ "-m", "-2" ], string.Empty );
		var narrow = await RunAsync( [ "-T", "-2", "-w1" ], Lines( "a", "b" ) );
		using var source = new CancellationTokenSource();
		source.Cancel();
		var canceled = await RunAsync( [ "-T" ], Lines( "x" ), source.Token );
		Assert.Equal( CommandExitCodes.Success, help.Status );
		Assert.Contains( "Usage: pr", help.Output );
		Assert.Equal( CommandExitCodes.Success, version.Status );
		Assert.Contains( "pr (Icod.CoreUtils) 1.0", version.Output );
		Assert.Equal( CommandExitCodes.UsageError, invalid.Status );
		Assert.Equal( CommandExitCodes.UsageError, conflicting.Status );
		Assert.Equal( CommandExitCodes.UsageError, narrow.Status );
		Assert.Equal( CommandExitCodes.Canceled, canceled.Status );
	}

	/// <summary>Verifies the synchronous compatibility wrapper.</summary>
	[Fact]
	public void SynchronousWrapperUsesTheAsyncEngine() {
		var output = new StringWriter();
		var status = Command.Run( [ "-T" ], new StringReader( "x" ), output, new StringWriter() );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( string.Concat( "x", Environment.NewLine ), output.ToString() );
	}

	private static string Lines( params string[] lines ) {
		return string.Concat( string.Join( Environment.NewLine, lines ), Environment.NewLine );
	}

	private static string CreateTemporaryDirectory() {
		var result = Path.Combine( Path.GetTempPath(), string.Concat( "Icod.CoreUtils.Pr.Tests-", Guid.NewGuid() ) );
		Directory.CreateDirectory( result );
		return result;
	}

	private static async Task<(int Status, string Output, string Error)> RunAsync(
		string[] args,
		string input,
		CancellationToken cancellationToken = default
	) {
		var output = new StringWriter( CultureInfo.InvariantCulture );
		var error = new StringWriter( CultureInfo.InvariantCulture );
		var status = await Command.RunAsync(
			args,
			new StringReader( input ),
			output,
			error,
			cancellationToken
		);
		return ( status, output.ToString(), error.ToString() );
	}
}
