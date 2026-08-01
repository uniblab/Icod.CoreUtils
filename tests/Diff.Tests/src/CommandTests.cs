namespace Icod.DiffUtils.Diff.Tests;

using System.Text;
using Icod.DiffUtils.Diff;
using Xunit;

/// <summary>Exercises GNU-compatible <c>diff</c> command behavior.</summary>
public sealed class CommandTests {
	private static readonly string Nl = Environment.NewLine;

	/// <summary>Traditional output reports a changed line and status one.</summary>
	[Fact]
	public async Task NormalFormatReportsChangedLine() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\ntwo\nthree\n" );
		var second = fixture.Write( "second", "one\nTWO\nthree\n" );
		var result = await RunAsync( first, second );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"2c2{Nl}< two{Nl}---{Nl}> TWO{Nl}", result.Output );
		Assert.Equal( string.Empty, result.Error );
	}

	/// <summary>Unified output uses labels and zero-context ranges consumable by patch.</summary>
	[Fact]
	public async Task UnifiedFormatUsesLabelsAndRanges() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\ntwo\nthree\n" );
		var second = fixture.Write( "second", "one\nTWO\nthree\n" );
		var result = await RunAsync( "-U0", "--label", "OLD", "--label", "NEW", first, second );
		Assert.Equal( 1, result.Status );
		Assert.Equal(
			$"--- OLD{Nl}+++ NEW{Nl}@@ -2 +2 @@{Nl}-two{Nl}+TWO{Nl}",
			result.Output
		);
	}

	/// <summary>Context output omits the unchanged old body for a pure insertion.</summary>
	[Fact]
	public async Task ContextFormatHandlesPureInsertion() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\n" );
		var second = fixture.Write( "second", "x\none\n" );
		var result = await RunAsync( "-C1", "--label", "A", "--label", "B", first, second );
		Assert.Equal(
			$"*** A{Nl}--- B{Nl}***************{Nl}*** 1 ****{Nl}--- 1,2 ----{Nl}+ x{Nl}  one{Nl}",
			result.Output
		);
	}

	/// <summary>Function context appears on the context hunk separator.</summary>
	[Fact]
	public async Task ContextFunctionAppearsOnHunkSeparator() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "SECTION one\nline\nold\n" );
		var second = fixture.Write( "second", "SECTION one\nline\nnew\n" );
		var result = await RunAsync( "-C0", "-F", "^SECTION", "--label", "A", "--label", "B", first, second );
		Assert.Equal(
			$"*** A{Nl}--- B{Nl}*************** SECTION one{Nl}*** 3 ****{Nl}! old{Nl}--- 3 ----{Nl}! new{Nl}",
			result.Output
		);
	}

	/// <summary>The reverse-order ed format can change the first input into the second.</summary>
	[Fact]
	public async Task EdFormatWritesEdScript() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\ntwo\nthree\n" );
		var second = fixture.Write( "second", "one\nTWO\nthree\n" );
		var result = await RunAsync( "-e", first, second );
		Assert.Equal( $"2c{Nl}TWO{Nl}.{Nl}", result.Output );
	}

	/// <summary>Forward ed format uses space-separated ranges and leaves period lines unescaped.</summary>
	[Fact]
	public async Task ForwardEdFormatUsesItsHistoricalSyntax() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "a\nb\nc\nd\n" );
		var second = fixture.Write( "second", "a\nX\nY\nd\n.\n" );
		var result = await RunAsync( "-f", first, second );
		Assert.Equal(
			$"c2 3{Nl}X{Nl}Y{Nl}.{Nl}a4{Nl}.{Nl}.{Nl}",
			result.Output
		);
	}

	/// <summary>Ed formats diagnose incomplete text inputs and return trouble.</summary>
	[Fact]
	public async Task EdFormatTreatsIncompleteInputAsTrouble() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "same" );
		var second = fixture.Write( "second", "same" );
		var result = await RunAsync( "-e", first, second );
		Assert.Equal( 2, result.Status );
		Assert.Equal( string.Empty, result.Output );
		Assert.Equal(
			$"diff: {first}: No newline at end of file{Nl}{Nl}diff: {second}: No newline at end of file{Nl}{Nl}",
			result.Error
		);
	}

	/// <summary>RCS format emits deletion and insertion commands.</summary>
	[Fact]
	public async Task RcsFormatWritesCommands() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\ntwo\nthree\n" );
		var second = fixture.Write( "second", "one\nTWO\nthree\n" );
		var result = await RunAsync( "-n", first, second );
		Assert.Equal( $"d2 1{Nl}a2 1{Nl}TWO{Nl}", result.Output );
	}

	/// <summary>Conditional format retains both alternatives.</summary>
	[Fact]
	public async Task IfDefFormatWritesConditionalMerge() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\ntwo\nthree\n" );
		var second = fixture.Write( "second", "one\nTWO\nthree\n" );
		var result = await RunAsync( "-D", "FEATURE", first, second );
		Assert.Equal(
			$"one{Nl}#ifndef FEATURE{Nl}two{Nl}#else /* FEATURE */{Nl}TWO{Nl}#endif /* FEATURE */{Nl}three{Nl}",
			result.Output
		);
	}

	/// <summary>Whitespace and case policies affect comparison but preserve original output.</summary>
	[Fact]
	public async Task ComparisonPoliciesCanMakeInputsEquivalent() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "Alpha beta\n" );
		var second = fixture.Write( "second", " alpha    BETA \n" );
		var result = await RunAsync( "-i", "-b", first, second );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Empty, result.Output );
	}

	/// <summary>An incomplete changed line receives the standard marker.</summary>
	[Fact]
	public async Task IncompleteLinesAreMarked() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "old" );
		var second = fixture.Write( "second", "new" );
		var result = await RunAsync( first, second );
		Assert.Equal(
			$"1c1{Nl}< old{Nl}\\ No newline at end of file{Nl}---{Nl}> new{Nl}\\ No newline at end of file{Nl}",
			result.Output
		);
	}

	/// <summary>Initial-tab mode preserves context prefixes before the alignment tab.</summary>
	[Fact]
	public async Task InitialTabPreservesContextPrefixes() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "one\ntwo\n" );
		var second = fixture.Write( "second", "one\nTWO\n" );
		var result = await RunAsync( "-U1", "-T", "--label", "A", "--label", "B", first, second );
		Assert.Equal(
			$"--- A{Nl}+++ B{Nl}@@ -1,2 +1,2 @@{Nl}\tone{Nl}-\ttwo{Nl}+\tTWO{Nl}",
			result.Output
		);
	}

	/// <summary>Ifdef output terminates incomplete source lines before directives.</summary>
	[Fact]
	public async Task IfDefFormatTerminatesIncompleteLines() {
		using var fixture = new FileFixture();
		var first = fixture.Write( "first", "old" );
		var second = fixture.Write( "second", "new" );
		var result = await RunAsync( "-D", "FEATURE", first, second );
		Assert.Equal(
			$"#ifndef FEATURE{Nl}old{Nl}#else /* FEATURE */{Nl}new{Nl}#endif /* FEATURE */{Nl}",
			result.Output
		);
	}

	/// <summary>NUL-containing inputs use the binary report unless text is forced.</summary>
	[Fact]
	public async Task BinaryInputsUseBinaryReport() {
		using var fixture = new FileFixture();
		var first = fixture.WriteBytes( "first", new byte[] { 0, 1 } );
		var second = fixture.WriteBytes( "second", new byte[] { 0, 2 } );
		var result = await RunAsync( first, second );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"Binary files {first} and {second} differ{Nl}", result.Output );
	}

	/// <summary>Recursive comparison reports changes in matching descendants.</summary>
	[Fact]
	public async Task RecursiveDirectoriesCompareDescendants() {
		using var fixture = new FileFixture();
		var firstDirectory = fixture.Directory( "first" );
		var secondDirectory = fixture.Directory( "second" );
		Directory.CreateDirectory( Path.Combine( firstDirectory, "nested" ) );
		Directory.CreateDirectory( Path.Combine( secondDirectory, "nested" ) );
		File.WriteAllText( Path.Combine( firstDirectory, "nested", "value.txt" ), "old\n" );
		File.WriteAllText( Path.Combine( secondDirectory, "nested", "value.txt" ), "new\n" );
		var result = await RunAsync( "-r", firstDirectory, secondDirectory );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "1c1", result.Output, StringComparison.Ordinal );
		Assert.Contains( "< old", result.Output, StringComparison.Ordinal );
		Assert.Contains( "> new", result.Output, StringComparison.Ordinal );
	}

	/// <summary>New-file mode compares an absent operand as an empty file.</summary>
	[Fact]
	public async Task NewFileTreatsAbsentInputAsEmpty() {
		using var fixture = new FileFixture();
		var missing = Path.Combine( fixture.Root, "missing" );
		var present = fixture.Write( "present", "new\n" );
		var result = await RunAsync( "-N", missing, present );
		Assert.Equal( 1, result.Status );
		Assert.Equal( $"0a1{Nl}> new{Nl}", result.Output );
	}

	/// <summary>New-file mode does not descend into absent subdirectories without recursion.</summary>
	[Fact]
	public async Task NewFileRequiresRecursiveOptionForAbsentSubdirectories() {
		using var fixture = new FileFixture();
		var missing = Path.Combine( fixture.Root, "missing" );
		var present = fixture.Directory( "present" );
		var child = Path.Combine( present, "nested" );
		Directory.CreateDirectory( child );
		File.WriteAllText( Path.Combine( child, "value.txt" ), "new\n" );
		var result = await RunAsync( "-N", missing, present );
		Assert.Equal( 0, result.Status );
		Assert.Equal(
			$"Common subdirectories: {Path.Combine( missing, "nested" )} and {child}{Nl}",
			result.Output
		);
	}

	/// <summary>Repeated standard input is materialized once and compares equal to itself.</summary>
	[Fact]
	public async Task RepeatedStandardInputComparesEqual() {
		var result = await RunWithInputAsync( "value\n", "-", "-" );
		Assert.Equal( 0, result.Status );
		Assert.Equal( string.Empty, result.Output );
	}

	/// <summary>Pre-cancellation returns the command-framework cancellation status.</summary>
	[Fact]
	public async Task PreCanceledCommandReturnsCanceledStatus() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await Command.RunAsync(
			new[] { "-", "-" },
			new StringReader( "value" ),
			output,
			error,
			cancellation.Token
		);
		Assert.Equal( 130, status );
	}

	/// <summary>Help and version are successful terminal actions.</summary>
	[Theory]
	[InlineData( "--help", "Usage: diff" )]
	[InlineData( "--version", "diff (Icod.DiffUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var result = await RunAsync( option );
		Assert.Equal( 0, result.Status );
		Assert.Contains( expected, result.Output, StringComparison.Ordinal );
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
			this.Root = Path.Combine( Path.GetTempPath(), $"Icod.DiffUtils.Diff.Tests-{Guid.NewGuid():N}" );
			System.IO.Directory.CreateDirectory( this.Root );
		}

		/// <summary>Gets the fixture root.</summary>
		public string Root { get; }

		/// <summary>Creates a named child directory.</summary>
		public string Directory( string name ) {
			var path = Path.Combine( this.Root, name );
			System.IO.Directory.CreateDirectory( path );
			return path;
		}

		/// <summary>Writes UTF-8 text and returns its path.</summary>
		public string Write( string name, string content ) {
			var path = Path.Combine( this.Root, name );
			File.WriteAllText( path, content, new UTF8Encoding( false ) );
			return path;
		}

		/// <summary>Writes bytes and returns their path.</summary>
		public string WriteBytes( string name, byte[] bytes ) {
			var path = Path.Combine( this.Root, name );
			File.WriteAllBytes( path, bytes );
			return path;
		}

		/// <inheritdoc/>
		public void Dispose() {
			try {
				System.IO.Directory.Delete( this.Root, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}
}
