namespace Icod.DiffUtils.SDiff.Tests;

using System.Text;
using Icod.DiffUtils.SDiff;
using Icod.DiffUtils.Shared.Lines;
using Xunit;

/// <summary>Exercises GNU-compatible <c>sdiff</c> comparison and merge behavior.</summary>
public sealed class CommandTests {
	private static readonly string Nl = Environment.NewLine;

	/// <summary>Ordinary output aligns common and changed rows and returns status one.</summary>
	[Fact]
	public async Task SideBySideOutputReportsDifferences() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "same\nleft\n" );
		var right = fixture.Write( "right", "same\nright\n" );
		var result = await RunAsync( "-t", "-w", "25", left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"same          same{Nl}left        | right{Nl}",
			result.Output
		);
		Assert.Equal( string.Empty, result.Error );
	}

	/// <summary>Equal inputs return status zero.</summary>
	[Fact]
	public async Task EqualInputsReturnZero() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "same\n" );
		var right = fixture.Write( "right", "same\n" );
		var result = await RunAsync( "-t", "-w", "25", left, right );
		Assert.Equal( 0, result.Status );
		Assert.Equal( $"same          same{Nl}", result.Output );
	}

	/// <summary>Common-line controls affect only common rows.</summary>
	[Fact]
	public async Task CommonLineControlsAreHonored() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "same\nleft\n" );
		var right = fixture.Write( "right", "same\nright\n" );
		var suppressed = await RunAsync( "-s", "-t", "-w", "25", left, right );
		Assert.Equal( $"left        | right{Nl}", suppressed.Output );
		var leftOnly = await RunAsync( "-l", "-t", "-w", "25", left, right );
		Assert.StartsWith( $"same        ({Nl}", leftOnly.Output, StringComparison.Ordinal );
		Assert.Contains( $"left        | right{Nl}", leftOnly.Output, StringComparison.Ordinal );
	}

	/// <summary>Case and whitespace policies can make source lines equivalent.</summary>
	[Fact]
	public async Task ComparisonPoliciesCanMakeInputsEquivalent() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "Alpha  beta\n" );
		var right = fixture.Write( "right", "alpha beta   \n" );
		var result = await RunAsync( "-ib", "-w", "35", left, right );
		Assert.Equal( 0, result.Status );
		Assert.DoesNotContain( " | ", result.Output, StringComparison.Ordinal );
	}

	/// <summary>Output tab expansion removes literal tabs while retaining alignment.</summary>
	[Fact]
	public async Task ExpandTabsUsesConfiguredTabStops() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "a\tb\n" );
		var right = fixture.Write( "right", "a\tb\n" );
		var result = await RunAsync( "-t", "--tabsize=4", "-w", "25", left, right );
		Assert.Equal( 0, result.Status );
		Assert.DoesNotContain( "\t", result.Output, StringComparison.Ordinal );
		Assert.Contains( "a   b", result.Output, StringComparison.Ordinal );
	}


	/// <summary>Default output uses tab stops to align the right column like GNU <c>sdiff</c>.</summary>
	[Fact]
	public async Task DefaultLayoutUsesOutputTabs() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "same\nleft\n" );
		var right = fixture.Write( "right", "same\nright\n" );
		var result = await RunAsync( "-w", "25", left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"same\t\tsame{Nl}left\t    |\tright{Nl}", result.Output );
	}

	/// <summary>Very narrow output never exceeds the requested display width.</summary>
	[Theory]
	[InlineData( 1, " ", "|" )]
	[InlineData( 2, "  ", "| " )]
	[InlineData( 3, "   ", " | " )]
	public async Task NarrowWidthsRemainBounded( int width, string common, string changed ) {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "same\nleft\n" );
		var right = fixture.Write( "right", "same\nright\n" );
		var result = await RunAsync( "-w", width.ToString( System.Globalization.CultureInfo.InvariantCulture ), left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"{common}{Nl}{changed}{Nl}", result.Output );
	}

	/// <summary>Asymmetric line termination selects GNU's slash and backslash gutter markers.</summary>
	[Theory]
	[InlineData( "left\n", "right", '/' )]
	[InlineData( "left", "right\n", '\\' )]
	public async Task AsymmetricIncompleteLinesUseDirectionalMarker( string leftText, string rightText, char marker ) {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", leftText );
		var right = fixture.Write( "right", rightText );
		var result = await RunAsync( "-t", "-w", "25", left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"left        {marker} right{Nl}", result.Output );
	}

	/// <summary>Incomplete changed lines use GNU's slash markers and do not invent a final newline.</summary>
	[Fact]
	public async Task IncompleteLinesUseTerminationMarkers() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "left" );
		var right = fixture.Write( "right", "right" );
		var result = await RunAsync( "-t", "-w", "25", left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( "left        | right", result.Output );
	}

	/// <summary>Binary differences use the standard binary report unless text is forced.</summary>
	[Fact]
	public async Task BinaryInputsUseBinaryReport() {
		using var fixture = new FileFixture();
		var left = fixture.WriteBytes( "left", new byte[] { (byte)'a', 0, (byte)'b' } );
		var right = fixture.WriteBytes( "right", new byte[] { (byte)'a', 0, (byte)'c' } );
		var result = await RunAsync( left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"Binary files {left} and {right} differ{Nl}", result.Output );
		var forced = await RunAsync( "-a", "-w", "25", left, right );
		Assert.Equal( 1, forced.Status );
		Assert.Contains( "\0", forced.Output, StringComparison.Ordinal );
	}


	/// <summary>Binary merge mode creates an empty transactional output without prompting.</summary>
	[Theory]
	[InlineData( true, 0 )]
	[InlineData( false, 1 )]
	public async Task BinaryMergeOutputIsEmpty( bool equal, int expectedStatus ) {
		using var fixture = new FileFixture();
		var left = fixture.WriteBytes( "left", new byte[] { (byte)'a', 0, (byte)'b' } );
		var right = fixture.WriteBytes(
			"right",
			equal ? new byte[] { (byte)'a', 0, (byte)'b' } : new byte[] { (byte)'a', 0, (byte)'c' }
		);
		var output = fixture.Write( "merged", "old content" );
		var result = await RunAsync( "-o", output, left, right );
		Assert.Equal( expectedStatus, result.Status );
		Assert.Empty( File.ReadAllBytes( output ) );
		Assert.DoesNotContain( "%", result.Output, StringComparison.Ordinal );
	}

	/// <summary>Interactive left and right choices produce a complete merged file.</summary>
	[Theory]
	[InlineData( "l\n", "left" )]
	[InlineData( "1\n", "left" )]
	[InlineData( "r\n", "right" )]
	[InlineData( "2\n", "right" )]
	public async Task InteractiveChoicesSelectOneSide( string command, string selected ) {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "before\nleft\nafter\n" );
		var right = fixture.Write( "right", "before\nright\nafter\n" );
		var output = fixture.PathFor( "merged" );
		var result = await RunWithInputAsync( command, "-o", output, "-w", "25", left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"before{Nl}{selected}{Nl}after{Nl}", File.ReadAllText( output ) );
		Assert.Contains( "%", result.Output, StringComparison.Ordinal );
	}


	/// <summary>Interactive output preserves a single carriage return from CRLF input.</summary>
	[Fact]
	public async Task MergeDoesNotDuplicateCarriageReturns() {
		using var fixture = new FileFixture();
		var left = fixture.WriteBytes( "left", new byte[] { (byte)'l', (byte)'e', (byte)'f', (byte)'t', 13, 10 } );
		var right = fixture.WriteBytes( "right", new byte[] { (byte)'r', (byte)'i', (byte)'g', (byte)'h', (byte)'t', 13, 10 } );
		var output = fixture.PathFor( "merged" );
		var result = await RunWithInputAsync( "l\n", "-o", output, left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( new byte[] { (byte)'l', (byte)'e', (byte)'f', (byte)'t', 13, 10 }, File.ReadAllBytes( output ) );
	}

	/// <summary>Silent and verbose commands change later common-line display without changing merged content.</summary>
	[Fact]
	public async Task InteractiveSilentCommandSuppressesFollowingCommonDisplay() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "before\nleft\nafter\n" );
		var right = fixture.Write( "right", "before\nright\nafter\n" );
		var output = fixture.PathFor( "merged" );
		var result = await RunWithInputAsync( "s\nl\n", "-o", output, "-t", "-w", "25", left, right );
		Assert.Equal( 1, result.Status );
		Assert.DoesNotContain( "after          after", result.Output, StringComparison.Ordinal );
		Assert.Equal( $"before{Nl}left{Nl}after{Nl}", File.ReadAllText( output ) );
	}

	/// <summary>Quit abandons the temporary output and preserves an existing destination.</summary>
	[Fact]
	public async Task QuitDoesNotCommitOutput() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "left\n" );
		var right = fixture.Write( "right", "right\n" );
		var output = fixture.Write( "merged", "original\n" );
		var result = await RunWithInputAsync( "q\n", "-o", output, left, right );
		Assert.Equal( 2, result.Status );
		Assert.Equal( "original\n", File.ReadAllText( output ).Replace( "\r\n", "\n", StringComparison.Ordinal ) );
	}

	/// <summary>Nonterminal EOF is trouble and leaves the destination untouched.</summary>
	[Fact]
	public async Task NonterminalEofDoesNotCommitOutput() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "left\n" );
		var right = fixture.Write( "right", "right\n" );
		var output = fixture.Write( "merged", "original\n" );
		var result = await RunWithInputAsync( string.Empty, "-o", output, left, right );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "end of file on standard input", result.Error, StringComparison.Ordinal );
		Assert.Equal( "original\n", File.ReadAllText( output ).Replace( "\r\n", "\n", StringComparison.Ordinal ) );
	}

	/// <summary>Editor modes use an injected editor and commit its result.</summary>
	[Theory]
	[InlineData( "e\n", 0 )]
	[InlineData( "eb\n", 2 )]
	[InlineData( "el\n", 1 )]
	[InlineData( "e1\n", 1 )]
	[InlineData( "er\n", 1 )]
	[InlineData( "e2\n", 1 )]
	public async Task EditorCommandsProvideExpectedInitialContent( string command, int expectedCount ) {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "left\n" );
		var right = fixture.Write( "right", "right\n" );
		var output = fixture.PathFor( "merged" );
		var editor = new RecordingEditor( new ComparisonLine( "edited", true ) );
		var result = await RunWithInputAsync( command, editor, "-o", output, left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( expectedCount, editor.Initial.Count );
		Assert.Equal( $"edited{Nl}", File.ReadAllText( output ) );
	}

	/// <summary>The decorated editor command identifies both source ranges.</summary>
	[Fact]
	public async Task DecoratedEditorCommandAddsHeaders() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "before\nleft\n" );
		var right = fixture.Write( "right", "before\nright\n" );
		var output = fixture.PathFor( "merged" );
		var editor = new RecordingEditor( new ComparisonLine( "edited", true ) );
		var result = await RunWithInputAsync( "ed\n", editor, "-o", output, left, right );
		Assert.Equal( 1, result.Status );
		Assert.Equal( 4, editor.Initial.Count );
		Assert.Equal( $"--- {left} 2", editor.Initial[0].Content );
		Assert.Equal( $"+++ {right} 2", editor.Initial[2].Content );
	}

	/// <summary>Editor failure aborts the output transaction.</summary>
	[Fact]
	public async Task EditorFailureDoesNotCommitOutput() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "left\n" );
		var right = fixture.Write( "right", "right\n" );
		var output = fixture.Write( "merged", "original\n" );
		var result = await RunWithInputAsync( "e\n", new FailingEditor(), "-o", output, left, right );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "editor failed", result.Error, StringComparison.Ordinal );
		Assert.Equal( "original\n", File.ReadAllText( output ).Replace( "\r\n", "\n", StringComparison.Ordinal ) );
	}

	/// <summary>Matching-line suppression removes a difference from status and prompting.</summary>
	[Fact]
	public async Task IgnoreMatchingLinesSuppressesDifference() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", "DEBUG one\n" );
		var right = fixture.Write( "right", "DEBUG two\n" );
		var result = await RunAsync( "-I", "^DEBUG", "-w", "35", left, right );
		Assert.Equal( 0, result.Status );
		Assert.DoesNotContain( " | ", result.Output, StringComparison.Ordinal );
	}


	/// <summary>Ignored one-sided changes use common-line gutters and respect left-column mode.</summary>
	[Fact]
	public async Task IgnoredInsertionsUseCommonLineLayout() {
		using var fixture = new FileFixture();
		var left = fixture.Write( "left", string.Empty );
		var right = fixture.Write( "right", "DEBUG two\n" );
		var result = await RunAsync( "-t", "-I", "^DEBUG", "-w", "35", left, right );
		Assert.Equal( 0, result.Status );
		Assert.Contains( ") DEBUG two", result.Output, StringComparison.Ordinal );
		var leftColumn = await RunAsync( "-t", "-l", "-I", "^DEBUG", "-w", "35", left, right );
		Assert.Equal( 0, leftColumn.Status );
		Assert.Equal( string.Empty, leftColumn.Output );
	}

	/// <summary>A directory operand resolves the basename of the file operand.</summary>
	[Fact]
	public async Task DirectoryOperandResolvesMatchingFile() {
		using var fixture = new FileFixture();
		var file = fixture.Write( "left/name.txt", "same\n" );
		var directory = fixture.Directory( "right" );
		fixture.Write( "right/name.txt", "same\n" );
		var result = await RunAsync( file, directory );
		Assert.Equal( 0, result.Status );
	}

	/// <summary>Repeated standard input is rejected.</summary>
	[Fact]
	public async Task RejectsRepeatedStandardInput() {
		var result = await RunAsync( "-", "-" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "standard input may be specified only once", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Interactive mode rejects a standard-input file before consuming merge commands.</summary>
	[Fact]
	public async Task InteractiveModeRejectsStandardInputFile() {
		using var fixture = new FileFixture();
		var right = fixture.Write( "right", "right\n" );
		var output = fixture.PathFor( "merged" );
		var result = await RunWithInputAsync( "left\nl\n", "-o", output, "-", right );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "cannot interactively merge standard input", result.Error, StringComparison.Ordinal );
		Assert.False( File.Exists( output ) );
	}

	/// <summary>An external comparison program is rejected instead of creating a tool dependency.</summary>
	[Fact]
	public async Task RejectsExternalDiffProgram() {
		var result = await RunAsync( "--diff-program=diff", "left", "right" );
		Assert.Equal( 2, result.Status );
		Assert.Contains( "--diff-program is not supported", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Help and version are successful terminal actions.</summary>
	[Theory]
	[InlineData( "--help", "Usage: sdiff" )]
	[InlineData( "--version", "sdiff (Icod.DiffUtils)" )]
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
			new[] { "left", "right" },
			TextReader.Null,
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, status );
	}

	private static Task<CommandResult> RunAsync( params string[] arguments ) {
		return RunWithInputAsync( string.Empty, null, arguments );
	}

	private static Task<CommandResult> RunWithInputAsync( string input, params string[] arguments ) {
		return RunWithInputAsync( input, null, arguments );
	}

	private static async Task<CommandResult> RunWithInputAsync(
		string input,
		ISDiffEditor? editor,
		params string[] arguments
	) {
		var output = new StringWriter();
		var error = new StringWriter();
		await using var stream = new MemoryStream( Encoding.UTF8.GetBytes( input ) );
		var status = await Command.RunAsync(
			arguments,
			new StringReader( input ),
			output,
			error,
			stdinStream: stream,
			editor: editor
		);
		return new CommandResult( status, output.ToString(), error.ToString() );
	}

	private sealed record CommandResult( int Status, string Output, string Error );

	private sealed class RecordingEditor : ISDiffEditor {
		private readonly IReadOnlyList<ComparisonLine> result;

		/// <summary>Initializes an editor that returns the supplied lines.</summary>
		public RecordingEditor( params ComparisonLine[] result ) {
			this.result = result;
		}

		/// <summary>Gets the most recent initial editor contents.</summary>
		public IReadOnlyList<ComparisonLine> Initial { get; private set; } = Array.Empty<ComparisonLine>();

		/// <inheritdoc />
		public Task<IReadOnlyList<ComparisonLine>> EditAsync(
			IReadOnlyList<ComparisonLine> initialContent,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Initial = initialContent.ToArray();
			return Task.FromResult( this.result );
		}
	}

	private sealed class FailingEditor : ISDiffEditor {
		/// <inheritdoc />
		public Task<IReadOnlyList<ComparisonLine>> EditAsync(
			IReadOnlyList<ComparisonLine> initialContent,
			CancellationToken cancellationToken = default
		) {
			throw new IOException( "editor failed" );
		}
	}

	private sealed class FileFixture : IDisposable {
		/// <summary>Initializes a temporary filesystem fixture.</summary>
		public FileFixture() {
			this.Root = Path.Combine( Path.GetTempPath(), $"Icod.DiffUtils.SDiff.Tests-{Guid.NewGuid():N}" );
			Directory.CreateDirectory( this.Root );
		}

		/// <summary>Gets the fixture root.</summary>
		public string Root { get; }

		/// <summary>Returns a path under the fixture root.</summary>
		public string PathFor( string relativePath ) {
			return Path.Combine( this.Root, relativePath.Replace( '/', Path.DirectorySeparatorChar ) );
		}

		/// <summary>Creates a directory under the fixture root.</summary>
		public string Directory( string relativePath ) {
			var path = this.PathFor( relativePath );
			System.IO.Directory.CreateDirectory( path );
			return path;
		}

		/// <summary>Writes UTF-8 text under the fixture root.</summary>
		public string Write( string relativePath, string content ) {
			var path = this.PathFor( relativePath );
			System.IO.Directory.CreateDirectory( Path.GetDirectoryName( path )! );
			File.WriteAllText( path, content, new UTF8Encoding( false ) );
			return path;
		}

		/// <summary>Writes bytes under the fixture root.</summary>
		public string WriteBytes( string relativePath, byte[] content ) {
			var path = this.PathFor( relativePath );
			System.IO.Directory.CreateDirectory( Path.GetDirectoryName( path )! );
			File.WriteAllBytes( path, content );
			return path;
		}

		/// <inheritdoc />
		public void Dispose() {
			try {
				System.IO.Directory.Delete( this.Root, recursive: true );
			} catch ( IOException ) {
				// Best effort fixture cleanup.
			} catch ( UnauthorizedAccessException ) {
				// Best effort fixture cleanup.
			}
		}
	}
}
