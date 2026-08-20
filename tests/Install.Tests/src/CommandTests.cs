namespace Icod.CoreUtils.Install.Tests;

using InstallCommand = Icod.CoreUtils.Install.Command;
using Xunit;

/// <summary>Exercises Batch 45 <c>install</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies ordinary files are installed through staged replacement.</summary>
	[Fact]
	public async Task InstallsOrdinaryFile() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			Assert.Equal( 0, await RunAsync( new[] { source, destination } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( destination ) );
			Assert.Empty( Directory.EnumerateFiles( root, ".destination.icod-e6-*" ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies <c>-D</c> creates leading destination directories.</summary>
	[Fact]
	public async Task CreatesLeadingDirectories() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "one", "two", "destination" );
			await File.WriteAllTextAsync( source, "content" );
			Assert.Equal( 0, await RunAsync( new[] { "-D", source, destination } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies target-directory mode installs every source by basename.</summary>
	[Fact]
	public async Task InstallsIntoTargetDirectory() {
		var root = CreateTemporaryDirectory();
		try {
			var first = System.IO.Path.Combine( root, "first" );
			var second = System.IO.Path.Combine( root, "second" );
			var destination = Directory.CreateDirectory( System.IO.Path.Combine( root, "destination" ) ).FullName;
			await File.WriteAllTextAsync( first, "one" );
			await File.WriteAllTextAsync( second, "two" );
			Assert.Equal( 0, await RunAsync( new[] { "-t", destination, first, second } ) );
			Assert.Equal( "one", await File.ReadAllTextAsync( System.IO.Path.Combine( destination, "first" ) ) );
			Assert.Equal( "two", await File.ReadAllTextAsync( System.IO.Path.Combine( destination, "second" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a symbolic link to a directory is honored as a target-directory operand.</summary>
	[Fact]
	public async Task InstallsThroughTargetDirectorySymbolicLinkOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var physicalTarget = Directory.CreateDirectory( System.IO.Path.Combine( root, "physical" ) ).FullName;
			var linkedTarget = System.IO.Path.Combine( root, "linked" );
			await File.WriteAllTextAsync( source, "content" );
			Directory.CreateSymbolicLink( linkedTarget, physicalTarget );
			Assert.Equal( 0, await RunAsync( new[] { source, linkedTarget } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( System.IO.Path.Combine( physicalTarget, "source" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies <c>-D</c> may create missing descendants through an explicitly named directory symbolic link.</summary>
	[Fact]
	public async Task CreatesLeadingDirectoriesThroughSymbolicLinkOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var physicalTarget = Directory.CreateDirectory( System.IO.Path.Combine( root, "physical" ) ).FullName;
			var linkedTarget = System.IO.Path.Combine( root, "linked" );
			var destination = System.IO.Path.Combine( linkedTarget, "one", "two", "destination" );
			await File.WriteAllTextAsync( source, "content" );
			Directory.CreateSymbolicLink( linkedTarget, physicalTarget );
			Assert.Equal( 0, await RunAsync( new[] { "-D", source, destination } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( System.IO.Path.Combine( physicalTarget, "one", "two", "destination" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a terminal symbolic-link destination is rejected without modifying its target.</summary>
	[Fact]
	public async Task RejectsTerminalDestinationSymbolicLinkOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var target = System.IO.Path.Combine( root, "target" );
			var link = System.IO.Path.Combine( root, "link" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( target, "old" );
			File.CreateSymbolicLink( link, target );
			Assert.Equal( 1, await RunAsync( new[] { "-T", source, link } ) );
			Assert.Equal( "old", await File.ReadAllTextAsync( target ) );
			Assert.NotNull( new FileInfo( link ).LinkTarget );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies non-directory special sources can be streamed into an installed regular file.</summary>
	[Fact]
	public async Task InstallsNullDeviceOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var destination = System.IO.Path.Combine( root, "destination" );
			Assert.Equal( 0, await RunAsync( new[] { "/dev/null", destination } ) );
			Assert.True( File.Exists( destination ) );
			Assert.Equal( 0, new FileInfo( destination ).Length );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies simple backups retain the former destination.</summary>
	[Fact]
	public async Task RetainsSimpleBackup() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			Assert.Equal( 0, await RunAsync( new[] { "--backup=simple", source, destination } ) );
			Assert.Equal( "new", await File.ReadAllTextAsync( destination ) );
			Assert.Equal( "old", await File.ReadAllTextAsync( string.Concat( destination, "~" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies backup-control values accept unique GNU abbreviations and reject ambiguous ones.</summary>
	[Fact]
	public async Task ParsesBackupControlAbbreviations() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "new" );
			await File.WriteAllTextAsync( destination, "old" );
			Assert.Equal( 0, await RunAsync( new[] { "--backup=sim", source, destination } ) );
			Assert.Equal( "old", await File.ReadAllTextAsync( string.Concat( destination, "~" ) ) );
			Assert.Equal( 1, await RunAsync( new[] { "--backup=n", source, destination } ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies compare mode leaves an equivalent destination untouched.</summary>
	[Fact]
	public async Task CompareRetainsEquivalentDestination() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "same" );
			await File.WriteAllTextAsync( destination, "same" );
			if ( !OperatingSystem.IsWindows() ) {
				File.SetUnixFileMode( source, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute );
				File.SetUnixFileMode( destination, File.GetUnixFileMode( source ) );
			}
			var before = new DateTime( 2001, 2, 3, 4, 5, 6, DateTimeKind.Utc );
			File.SetLastWriteTimeUtc( destination, before );
			Assert.Equal( 0, await RunAsync( new[] { "--compare", source, destination } ) );
			Assert.Equal( before, File.GetLastWriteTimeUtc( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies source modification time can be preserved.</summary>
	[Fact]
	public async Task PreservesModificationTimestamp() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			var sourceTime = new DateTime( 2018, 7, 6, 5, 4, 2, DateTimeKind.Utc );
			File.SetLastWriteTimeUtc( source, sourceTime );
			Assert.Equal( 0, await RunAsync( new[] { "--preserve-timestamps", source, destination } ) );
			Assert.Equal( sourceTime, File.GetLastWriteTimeUtc( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies directory mode creates parents and the requested leaf.</summary>
	[Fact]
	public async Task CreatesDirectoryOperands() {
		var root = CreateTemporaryDirectory();
		try {
			var destination = System.IO.Path.Combine( root, "one", "two" );
			Assert.Equal( 0, await RunAsync( new[] { "-d", destination } ) );
			Assert.True( Directory.Exists( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies an existing directory symbolic-link operand is configured through its target.</summary>
	[Fact]
	public async Task ConfiguresDirectoryOperandThroughSymbolicLinkOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var physicalTarget = Directory.CreateDirectory( System.IO.Path.Combine( root, "physical" ) ).FullName;
			var linkedTarget = System.IO.Path.Combine( root, "linked" );
			Directory.CreateSymbolicLink( linkedTarget, physicalTarget );
			File.SetUnixFileMode( physicalTarget, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute );
			Assert.Equal( 0, await RunAsync( new[] { "--directory", "--mode=0755", linkedTarget } ) );
			Assert.Equal(
				UnixFileMode.UserRead
					| UnixFileMode.UserWrite
					| UnixFileMode.UserExecute
					| UnixFileMode.GroupRead
					| UnixFileMode.GroupExecute
					| UnixFileMode.OtherRead
					| UnixFileMode.OtherExecute,
				File.GetUnixFileMode( physicalTarget ) & (UnixFileMode)0x0fff
			);
			Assert.NotNull( new DirectoryInfo( linkedTarget ).LinkTarget );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies explicit modes are applied on Unix hosts.</summary>
	[Fact]
	public async Task AppliesExplicitModeOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			Assert.Equal( 0, await RunAsync( new[] { "--mode=0640", source, destination } ) );
			Assert.Equal(
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead,
				File.GetUnixFileMode( destination ) & (UnixFileMode)0x0fff
			);
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies default symbolic directory mode clears set-ID bits while an explicit numeric mode preserves them.</summary>
	[Fact]
	public async Task AppliesDirectorySetIdModeRulesOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var destination = Directory.CreateDirectory( System.IO.Path.Combine( root, "destination" ) ).FullName;
			var baseMode = UnixFileMode.UserRead
				| UnixFileMode.UserWrite
				| UnixFileMode.UserExecute
				| UnixFileMode.GroupRead
				| UnixFileMode.GroupExecute
				| UnixFileMode.OtherRead
				| UnixFileMode.OtherExecute;
			File.SetUnixFileMode( destination, baseMode | UnixFileMode.SetGroup );
			Assert.Equal( 0, await RunAsync( new[] { "--directory", destination } ) );
			Assert.Equal( baseMode, File.GetUnixFileMode( destination ) & (UnixFileMode)0x0fff );

			File.SetUnixFileMode( destination, baseMode | UnixFileMode.SetGroup );
			Assert.Equal( 0, await RunAsync( new[] { "--directory", "--mode=0755", destination } ) );
			Assert.Equal( baseMode | UnixFileMode.SetGroup, File.GetUnixFileMode( destination ) & (UnixFileMode)0x0fff );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies the selected strip program operates on the private staged file.</summary>
	[Fact]
	public async Task RunsSelectedStripProgramOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			var stripper = System.IO.Path.Combine( root, "stripper" );
			await File.WriteAllTextAsync( source, "payload" );
			await File.WriteAllTextAsync( stripper, "#!/bin/sh\nprintf stripped >> \"$1\"\n" );
			File.SetUnixFileMode( stripper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute );
			Assert.Equal( 0, await RunAsync( new[] { "--strip", string.Concat( "--strip-program=", stripper ), source, destination } ) );
			Assert.Equal( "payloadstripped", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}


	/// <summary>Verifies installation refuses a destination that identifies the source itself.</summary>
	[Fact]
	public async Task RefusesSameFilesystemEntry() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			await File.WriteAllTextAsync( source, "content" );
			Assert.Equal( 1, await RunAsync( new[] { source, source } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( source ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies the historical <c>-c</c> compatibility option is ignored.</summary>
	[Fact]
	public async Task IgnoresHistoricalCompatibilityOption() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			Assert.Equal( 0, await RunAsync( new[] { "-c", source, destination } ) );
			Assert.Equal( "content", await File.ReadAllTextAsync( destination ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies directory mode accepts preserve-context and reports unsupported SELinux hosts without failing.</summary>
	[Fact]
	public async Task AcceptsPreserveContextForDirectoryOperands() {
		var root = CreateTemporaryDirectory();
		try {
			var destination = System.IO.Path.Combine( root, "destination" );
			var error = new StringWriter();
			Assert.Equal(
				0,
				await InstallCommand.RunAsync(
					new[] { "--directory", "--preserve-context", destination },
					TextReader.Null,
					new StringWriter(),
					error
				)
			);
			Assert.True( Directory.Exists( destination ) );
			if ( error.GetStringBuilder().Length > 0 ) {
				Assert.Contains( "ignoring --preserve-context", error.ToString() );
			}
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies debug output describes private staging and atomic publication.</summary>
	[Fact]
	public async Task DebugExplainsAtomicPublication() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			var output = new StringWriter();
			Assert.Equal(
				0,
				await InstallCommand.RunAsync(
					new[] { "--debug", source, destination },
					TextReader.Null,
					output,
					new StringWriter()
				)
			);
			Assert.Contains( "private sibling stage", output.ToString() );
			Assert.Contains( "atomically published", output.ToString() );
		} finally {
			DeleteTree( root );
		}
	}


	/// <summary>Verifies an unused strip-program selection warns but does not prevent installation.</summary>
	[Fact]
	public async Task WarnsWhenStripProgramIsUnused() {
		var root = CreateTemporaryDirectory();
		try {
			var source = System.IO.Path.Combine( root, "source" );
			var destination = System.IO.Path.Combine( root, "destination" );
			await File.WriteAllTextAsync( source, "content" );
			var error = new StringWriter();
			Assert.Equal(
				0,
				await InstallCommand.RunAsync(
					new[] { "--strip-program=unused-strip", source, destination },
					TextReader.Null,
					new StringWriter(),
					error
				)
			);
			Assert.Equal( "content", await File.ReadAllTextAsync( destination ) );
			Assert.Contains( "ignoring --strip-program", error.ToString() );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies usage, version, and incompatible-option diagnostics.</summary>
	[Fact]
	public async Task ReportsHelpVersionAndUsageErrors() {
		var output = new StringWriter();
		Assert.Equal( 0, await InstallCommand.RunAsync( new[] { "--help" }, TextReader.Null, output, new StringWriter() ) );
		Assert.Contains( "Usage: install", output.ToString() );
		output.GetStringBuilder().Clear();
		Assert.Equal( 0, await InstallCommand.RunAsync( new[] { "--version" }, TextReader.Null, output, new StringWriter() ) );
		Assert.Contains( "Icod.CoreUtils", output.ToString() );
		Assert.Equal( 1, await RunAsync( new[] { "--compare", "--strip", "source", "destination" } ) );
		Assert.Equal( 1, await RunAsync( new[] { "--not-an-option" } ) );
		Assert.Equal( 1, await RunAsync( new[] { "--context=", "source", "destination" } ) );
		Assert.Equal( 1, await RunAsync( new[] { "--directory", "--strip", "destination" } ) );
	}

	private static ValueTask<int> RunAsync( string[] args ) => InstallCommand.RunAsync(
		args,
		TextReader.Null,
		new StringWriter(),
		new StringWriter()
	);

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Install-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static void DeleteTree( string path ) {
		try {
			if ( Directory.Exists( path ) ) Directory.Delete( path, recursive: true );
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}
}
