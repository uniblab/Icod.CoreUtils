namespace Icod.CoreUtils.Shared.Tests.DirectoryListing;

using Icod.CoreUtils.Shared.DirectoryListing;
using Icod.CoreUtils.Shared.Presentation;
using Xunit;

/// <summary>Verifies dircolors database parsing, terminal selection, and shell output.</summary>
public sealed class DirColorsDatabaseTests {
	/// <summary>Verifies selector groups are ORed and later terminal sections are isolated.</summary>
	[Fact]
	public void CompilesMatchingTerminalDatabase() {
		const string text = "FILE 00\nTERM xterm*\nCOLORTERM true*\nDIR 01;34\n.cs 00;32\nTERM dumb\nDIR 01;35\n";
		var database = DirColorsDatabase.Parse( new StringReader( text ), "test.db" );

		Assert.Empty( database.Diagnostics );
		var xtermColors = database.Compile( "xterm-256color", null );
		Assert.True( xtermColors.TryGetIndicator( "di", out var xtermDirectory ) );
		Assert.Equal( "01;34", xtermDirectory );
		Assert.Equal( "00;32", xtermColors.ResolveStyle( "Command.cs", "fi" ) );

		var colorTerminalColors = database.Compile( "unknown", "truecolor" );
		Assert.True( colorTerminalColors.TryGetIndicator( "di", out var colorTerminalDirectory ) );
		Assert.Equal( "01;34", colorTerminalDirectory );

		var dumbColors = database.Compile( "dumb", null );
		Assert.True( dumbColors.TryGetIndicator( "di", out var dumbDirectory ) );
		Assert.Equal( "01;35", dumbDirectory );
		Assert.Equal( "00", dumbColors.ResolveStyle( "Command.cs", "fi" ) );
	}

	/// <summary>Verifies parser and selected-section diagnostics retain source locations.</summary>
	[Fact]
	public void ReportsDatabaseDiagnostics() {
		var database = DirColorsDatabase.Parse(
			new StringReader( "DIR\nNOT_A_KEYWORD 31\n" ),
			"colors.conf"
		);
		var compilation = database.CompileWithDiagnostics( "xterm", null );

		Assert.Single( database.Diagnostics );
		Assert.Equal( 2, compilation.Diagnostics.Count );
		Assert.Equal( "colors.conf", compilation.Diagnostics[ 0 ].Source );
		Assert.Equal( 1, compilation.Diagnostics[ 0 ].Line );
		Assert.Contains( "missing value", compilation.Diagnostics[ 0 ].Message );
		Assert.Equal( 2, compilation.Diagnostics[ 1 ].Line );
		Assert.Contains( "unrecognized keyword", compilation.Diagnostics[ 1 ].Message );
	}

	/// <summary>Verifies unknown keywords in a nonmatching terminal section are ignored.</summary>
	[Fact]
	public void IgnoresDiagnosticsInUnselectedTerminalSections() {
		var database = DirColorsDatabase.Parse(
			new StringReader( "TERM xterm*\nUNKNOWN 31\nTERM dumb\nDIR 01;34\n" ),
			"colors.conf"
		);

		var compilation = database.CompileWithDiagnostics( "dumb", null );

		Assert.Empty( compilation.Diagnostics );
		Assert.True( compilation.Colors.TryGetIndicator( "di", out var directory ) );
		Assert.Equal( "01;34", directory );
	}

	/// <summary>Verifies the built-in database is valid and useful.</summary>
	[Fact]
	public void BuiltInDatabaseParsesWithoutDiagnostics() {
		var database = DirColorsDatabase.ParseBuiltIn();
		var compilation = database.CompileWithDiagnostics( "xterm-256color", null );

		Assert.Empty( compilation.Diagnostics );
		Assert.True( compilation.Colors.TryGetIndicator( "di", out var directory ) );
		Assert.Equal( "01;34", directory );
		Assert.NotEmpty( compilation.Colors.Patterns );
	}

	/// <summary>Verifies default shell syntax is inferred from SHELL.</summary>
	[Fact]
	public async Task InfersCshOutputFromEnvironment() {
		var output = new StringWriter();
		var error = new StringWriter();
		var environment = new FakeEnvironmentVariableProvider(
			new Dictionary<string, string?> {
				[ "TERM" ] = "xterm",
				[ "SHELL" ] = "/bin/tcsh"
			}
		);

		var exitCode = await DirColorsCommand.RunAsync(
			new[] { "-" },
			new StringReader( "TERM xterm\nDIR 01;34\n" ),
			output,
			error,
			environment
		);

		Assert.Equal( 0, exitCode );
		Assert.StartsWith( "setenv LS_COLORS ", output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies shell output requires either an explicit shell option or SHELL.</summary>
	[Fact]
	public async Task RequiresShellWhenEnvironmentCannotInferOne() {
		var output = new StringWriter();
		var error = new StringWriter();
		var environment = new FakeEnvironmentVariableProvider(
			new Dictionary<string, string?> { [ "TERM" ] = "xterm" }
		);

		var exitCode = await DirColorsCommand.RunAsync(
			new[] { "-" },
			new StringReader( "TERM xterm\nDIR 01;34\n" ),
			output,
			error,
			environment
		);

		Assert.Equal( 1, exitCode );
		Assert.Equal( string.Empty, output.ToString() );
		Assert.Contains( "no SHELL environment variable", error.ToString() );
	}

	/// <summary>Verifies the diagnostic color display does not require shell inference.</summary>
	[Fact]
	public async Task PrintsLsColorsForVisualInspection() {
		var output = new StringWriter();
		var error = new StringWriter();
		var environment = new FakeEnvironmentVariableProvider(
			new Dictionary<string, string?> { [ "TERM" ] = "xterm" }
		);

		var exitCode = await DirColorsCommand.RunAsync(
			new[] { "--print-ls-colors", "-" },
			new StringReader( "TERM xterm\nDIR 01;34\n" ),
			output,
			error,
			environment
		);

		Assert.Equal( 0, exitCode );
		Assert.Contains( "di\t01;34", output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		private readonly IReadOnlyDictionary<string, string?> values;

		/// <summary>Initializes the dictionary-backed environment provider.</summary>
		/// <param name="values">Environment values.</param>
		public FakeEnvironmentVariableProvider( IReadOnlyDictionary<string, string?> values ) {
			this.values = values;
		}

		/// <inheritdoc/>
		public string? GetValue( string name ) {
			return this.values.TryGetValue( name, out var value ) ? value : null;
		}
	}
}
