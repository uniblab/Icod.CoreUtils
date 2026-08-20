namespace Icod.CoreUtils.MkTemp.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Temporary;
using Xunit;

public sealed class MkTempCommandTests {
	[Fact]
	public async Task OmittedTemplateUsesTmpDirAndCreatesFile() {
		using var workspace = new Workspace();
		var result = await RunAsync( workspace, Array.Empty<string>() );
		Assert.Equal( 0, result.Status );
		Assert.Empty( result.Error );
		Assert.StartsWith( workspace.Root, result.Path, StringComparison.Ordinal );
		Assert.True( File.Exists( result.Path ) );
		Assert.Matches( @"tmp\.[A-Za-z0-9]{10}$", result.Path );
	}

	[Fact]
	public async Task DirectoryOptionCreatesDirectory() {
		using var workspace = new Workspace();
		var result = await RunAsync( workspace, [ "-d", "-p", workspace.Root, "folder.XXXX" ] );
		Assert.Equal( 0, result.Status );
		Assert.True( Directory.Exists( result.Path ) );
		Assert.False( File.Exists( result.Path ) );
	}

	[Fact]
	public async Task ExplicitSuffixIsAppendedAfterReplacementRun() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "--suffix=.txt", "report.XXXX" ]
		);
		Assert.Equal( 0, result.Status );
		Assert.EndsWith( ".txt", result.Path, StringComparison.Ordinal );
		Assert.True( File.Exists( result.Path ) );
	}

	[Fact]
	public async Task InferredSuffixIsPreserved() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "report.XXXX.json" ]
		);
		Assert.Equal( 0, result.Status );
		Assert.EndsWith( ".json", result.Path, StringComparison.Ordinal );
		Assert.True( File.Exists( result.Path ) );
	}

	[Fact]
	public async Task BareLongTmpDirUsesTmpDirEnvironmentWithoutConsumingTemplate() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "--tmpdir", "name.XXXX" ]
		);
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( workspace.Root, result.Path, StringComparison.Ordinal );
	}

	[Fact]
	public async Task ExplicitTmpDirOverridesEnvironment() {
		using var workspace = new Workspace();
		var other = Directory.CreateDirectory( System.IO.Path.Combine( workspace.Root, "other" ) ).FullName;
		var result = await RunAsync(
			workspace,
			[ "--tmpdir=" + other, "name.XXXX" ]
		);
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( other, result.Path, StringComparison.Ordinal );
	}

	[Fact]
	public async Task TraditionalModeGivesTmpDirPrecedenceOverP() {
		using var workspace = new Workspace();
		var other = Directory.CreateDirectory( System.IO.Path.Combine( workspace.Root, "other" ) ).FullName;
		var result = await RunAsync(
			workspace,
			[ "-t", "-p", other, "name.XXXX" ]
		);
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( workspace.Root, result.Path, StringComparison.Ordinal );
	}

	[Fact]
	public async Task TmpDirAllowsRelativeSubdirectoriesButCreatesOnlyFinalComponent() {
		using var workspace = new Workspace();
		var existing = Directory.CreateDirectory( System.IO.Path.Combine( workspace.Root, "existing" ) ).FullName;
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, System.IO.Path.Combine( "existing", "name.XXXX" ) ]
		);
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( existing, result.Path, StringComparison.Ordinal );
		Assert.True( File.Exists( result.Path ) );

		var missingResult = await RunAsync(
			workspace,
			[ "-p", workspace.Root, System.IO.Path.Combine( "missing", "name.XXXX" ) ]
		);
		Assert.Equal( 1, missingResult.Status );
		Assert.False( Directory.Exists( System.IO.Path.Combine( workspace.Root, "missing" ) ) );
	}

	[Fact]
	public async Task TmpDirRejectsAbsoluteTemplate() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, System.IO.Path.Combine( workspace.Root, "name.XXXX" ) ]
		);
		Assert.Equal( 1, result.Status );
		Assert.Contains( "may not be absolute", result.Error );
	}

	[Fact]
	public async Task TraditionalModeRejectsDirectorySeparators() {
		using var workspace = new Workspace();
		var result = await RunAsync( workspace, [ "-t", System.IO.Path.Combine( "sub", "name.XXXX" ) ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "contains directory separator", result.Error );
	}

	[Fact]
	public async Task TooFewReplacementCharactersAreRejected() {
		using var workspace = new Workspace();
		var result = await RunAsync( workspace, [ "-p", workspace.Root, "name.XX" ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "too few X's", result.Error );
	}

	[Fact]
	public async Task ExplicitSuffixRequiresTemplateEndingInX() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "--suffix=.txt", "name.XXXX.old" ]
		);
		Assert.Equal( 1, result.Status );
		Assert.Contains( "must end in X", result.Error );
	}

	[Fact]
	public async Task SuffixMayNotContainDirectorySeparator() {
		using var workspace = new Workspace();
		var suffix = string.Concat( ".a", System.IO.Path.DirectorySeparatorChar, "b" );
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "--suffix=" + suffix, "name.XXXX" ]
		);
		Assert.Equal( 1, result.Status );
		Assert.Contains( "directory separator", result.Error );
	}

	[Fact]
	public async Task MoreThanOneTemplateIsRejected() {
		using var workspace = new Workspace();
		var result = await RunAsync( workspace, [ "one.XXXX", "two.XXXX" ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "too many templates", result.Error );
	}

	[Fact]
	public async Task DryRunPrintsUnusedNameWithoutCreatingIt() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-u", "-p", workspace.Root, "name.XXXX" ]
		);
		Assert.Equal( 0, result.Status );
		Assert.False( File.Exists( result.Path ) );
		Assert.False( Directory.Exists( result.Path ) );
	}

	[Fact]
	public async Task DryRunMayReportANameBeneathAMissingDirectory() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-u", "-p", workspace.Root, System.IO.Path.Combine( "missing", "name.XXXX" ) ]
		);
		Assert.Equal( 0, result.Status );
		Assert.StartsWith( 
			System.IO.Path.Combine( workspace.Root, "missing" ),
			result.Path, 
			StringComparison.Ordinal
		);
		Assert.False( Directory.Exists( System.IO.Path.Combine( workspace.Root, "missing" ) ) );
	}

	[Fact]
	public async Task DryRunRejectsAnExistingNonDirectoryPathComponent() {
		using var workspace = new Workspace();
		await File.WriteAllTextAsync( System.IO.Path.Combine( workspace.Root, "parent" ), "file" );
		var result = await RunAsync(
			workspace,
			[ "-u", "-p", workspace.Root, System.IO.Path.Combine( "parent", "name.XXXX" ) ]
		);
		Assert.Equal( 1, result.Status );
		Assert.Contains( "not a directory", result.Error.ToLowerInvariant() );
	}

	[Fact]
	public async Task QuietSuppressesCreationDiagnostics() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-q", "-p", System.IO.Path.Combine( workspace.Root, "missing" ), "name.XXXX" ]
		);
		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Error );
	}

	[Fact]
	public async Task CollisionsAreRetriedWithNewRandomCharacters() {
		using var workspace = new Workspace();
		var collision = System.IO.Path.Combine( workspace.Root, "name.aaaa" );
		await File.WriteAllTextAsync( collision, "existing" );
		var random = new SequenceRandomSource(
			[ 0, 0, 0, 0, 1, 1, 1, 1 ]
		);
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "name.XXXX" ],
			random
		);
		Assert.Equal( 0, result.Status );
		Assert.EndsWith( "name.bbbb", result.Path, StringComparison.Ordinal );
		Assert.Equal( "existing", await File.ReadAllTextAsync( collision ) );
	}

	[Fact]
	public async Task ExistingSymbolicLinkIsANameCollisionAndIsNotFollowed() {
		using var workspace = new Workspace();
		var target = System.IO.Path.Combine( workspace.Root, "target" );
		var link = System.IO.Path.Combine( workspace.Root, "name.aaaa" );
		await File.WriteAllTextAsync( target, "preserve" );
		try {
			File.CreateSymbolicLink( link, target );
		} catch ( Exception exception ) when (
			exception is UnauthorizedAccessException
				or IOException
				or NotSupportedException
		) {
			return;
		}
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "name.XXXX" ],
			new SequenceRandomSource( [ 0, 0, 0, 0, 1, 1, 1, 1 ] )
		);
		Assert.Equal( 0, result.Status );
		Assert.EndsWith( "name.bbbb", result.Path, StringComparison.Ordinal );
		Assert.Equal( "preserve", await File.ReadAllTextAsync( target ) );
	}

	[Fact]
	public async Task UnixObjectsDoNotGrantGroupOrOtherPermissions() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}
		using var workspace = new Workspace();
		var fileResult = await RunAsync( workspace, [ "-p", workspace.Root, "file.XXXX" ] );
		var directoryResult = await RunAsync( workspace, [ "-d", "-p", workspace.Root, "dir.XXXX" ] );
		var forbidden = UnixFileMode.GroupRead
			| UnixFileMode.GroupWrite
			| UnixFileMode.GroupExecute
			| UnixFileMode.OtherRead
			| UnixFileMode.OtherWrite
			| UnixFileMode.OtherExecute;
		Assert.Equal( ( UnixFileMode )0, File.GetUnixFileMode( fileResult.Path ) & forbidden );
		Assert.Equal( ( UnixFileMode )0, File.GetUnixFileMode( directoryResult.Path ) & forbidden );
	}

	[Fact]
	public async Task OutputFailureRemovesCreatedFile() {
		using var workspace = new Workspace();
		var output = new ThrowingTextWriter();
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "name.XXXX" ],
			output: output
		);
		Assert.Equal( 1, result.Status );
		Assert.Empty( Directory.GetFiles( workspace.Root, "name.*" ) );
	}

	[Fact]
	public async Task QuietSuppressesOutputFailureDiagnosticAndStillCleansUp() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-q", "-p", workspace.Root, "name.XXXX" ],
			output: new ThrowingTextWriter()
		);
		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Error );
		Assert.Empty( Directory.GetFiles( workspace.Root, "name.*" ) );
	}

	[Fact]
	public async Task OutputFailureRemovesCreatedDirectory() {
		using var workspace = new Workspace();
		var result = await RunAsync(
			workspace,
			[ "-d", "-p", workspace.Root, "name.XXXX" ],
			output: new ThrowingTextWriter()
		);
		Assert.Equal( 1, result.Status );
		Assert.Empty( Directory.GetDirectories( workspace.Root, "name.*" ) );
	}

	[Fact]
	public async Task CancellationWhileReportingNameRemovesCreatedObject() {
		using var workspace = new Workspace();
		using var source = new CancellationTokenSource();
		var output = new CancelingTextWriter( source );
		var result = await RunAsync(
			workspace,
			[ "-p", workspace.Root, "name.XXXX" ],
			output: output,
			cancellationToken: source.Token
		);
		Assert.Equal( 130, result.Status );
		Assert.Empty( Directory.GetFiles( workspace.Root, "name.*" ) );
	}

	[Fact]
	public async Task HelpAndVersionDoNotCreateObjects() {
		using var workspace = new Workspace();
		var help = await RunAsync( workspace, [ "--help" ] );
		var version = await RunAsync( workspace, [ "--version" ] );
		var shortVersion = await RunAsync( workspace, [ "-V" ] );
		Assert.Equal( 0, help.Status );
		Assert.Contains( "Usage: mktemp", help.Output );
		Assert.Equal( 0, version.Status );
		Assert.Contains( "mktemp", version.Output );
		Assert.Equal( version.Output, shortVersion.Output );
		Assert.Empty( Directory.GetFileSystemEntries( workspace.Root ) );
	}

	[Fact]
	public async Task UnknownOptionReturnsFailure() {
		using var workspace = new Workspace();
		var result = await RunAsync( workspace, [ "--not-an-option" ] );
		Assert.Equal( 1, result.Status );
		Assert.Contains( "unrecognized option", result.Error );
	}

	[Fact]
	public async Task CallerOwnedWritersAreNotDisposed() {
		using var workspace = new Workspace();
		var output = new TrackingTextWriter();
		var error = new TrackingTextWriter();
		var result = await RunAsync(
			workspace,
			[ "-u", "-p", workspace.Root, "name.XXXX" ],
			output: output,
			error: error
		);
		Assert.Equal( 0, result.Status );
		Assert.False( output.WasDisposed );
		Assert.False( error.WasDisposed );
	}

	private static async Task<CommandResult> RunAsync(
		Workspace workspace,
		string[] args,
		ISecureRandomSource? random = null,
		TextWriter? output = null,
		TextWriter? error = null,
		CancellationToken cancellationToken = default
	) {
		var standardOutput = output ?? new StringWriter();
		var standardError = error ?? new StringWriter();
		var creator = new SecureTemporaryObjectCreator(
			SystemTemporaryObjectFileSystem.Instance,
			random ?? CryptographicRandomSource.Instance,
			maximumAttempts: 100
		);
		var status = await Command.RunAsync(
			args,
			new CommandContext(
				"mktemp",
				TextReader.Null,
				standardOutput,
				standardError,
				cancellationToken: cancellationToken
			),
			creator,
			new TestEnvironment( workspace.Root )
		);
		var outputText = standardOutput.ToString() ?? string.Empty;
		return new CommandResult(
			status,
			outputText,
			standardError.ToString() ?? string.Empty,
			outputText.TrimEnd( '\r', '\n' )
		);
	}

	private sealed record CommandResult(
		int Status,
		string Output,
		string Error,
		string Path
	);

	private sealed class TestEnvironment : IMkTempEnvironment {
		private readonly string temporaryDirectory;

		public TestEnvironment( string temporaryDirectory ) {
			this.temporaryDirectory = temporaryDirectory;
		}

		public string? GetEnvironmentVariable( string name ) {
			return "TMPDIR" == name ? temporaryDirectory : null;
		}

		public string GetDefaultTemporaryDirectory() {
			return temporaryDirectory;
		}
	}

	private sealed class SequenceRandomSource : ISecureRandomSource {
		private readonly Queue<int> values;

		public SequenceRandomSource( IEnumerable<int> values ) {
			this.values = new Queue<int>( values );
		}

		public int GetInt32( int exclusiveUpperBound ) {
			Assert.NotEmpty( values );
			var value = values.Dequeue();
			Assert.InRange( value, 0, exclusiveUpperBound - 1 );
			return value;
		}
	}

	private sealed class ThrowingTextWriter : TextWriter {
		public override Encoding Encoding => Encoding.UTF8;

		public override Task WriteLineAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			return Task.FromException( new IOException( "simulated output failure" ) );
		}
	}

	private sealed class CancelingTextWriter : TextWriter {
		private readonly CancellationTokenSource source;

		public CancelingTextWriter( CancellationTokenSource source ) {
			this.source = source;
		}

		public override Encoding Encoding => Encoding.UTF8;

		public override Task WriteLineAsync(
			ReadOnlyMemory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			source.Cancel();
			return Task.FromCanceled( source.Token );
		}
	}

	private sealed class TrackingTextWriter : StringWriter {
		public bool WasDisposed { get; private set; }

		protected override void Dispose( bool disposing ) {
			WasDisposed = true;
			base.Dispose( disposing );
		}
	}

	private sealed class Workspace : IDisposable {
		public Workspace() {
			Root = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "Icod.CoreUtils.MkTemp.Tests-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( Root );
		}

		public string Root { get; }

		public void Dispose() {
			try {
				Directory.Delete( Root, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}
}
