namespace Icod.CoreUtils.MkDir.Tests;

using MkDirCommand = Icod.CoreUtils.MkDir.Command;
using Xunit;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using FileCreationMask = Icod.CommandFramework.FileSystem.Modes.FileCreationMask;
using IFileCreationMaskProvider = Icod.CommandFramework.FileSystem.Modes.IFileCreationMaskProvider;
using Icod.CommandFramework.FileSystem.Mutation;

/// <summary>Exercises GNU-compatible <c>mkdir</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies parent creation and verbose diagnostics.</summary>
	[Fact]
	public async Task CreatesParentsAndReportsEachCreatedDirectory() {
		using var temporary = new TemporaryDirectory();
		var first = System.IO.Path.Combine( temporary.Path, "one" );
		var second = System.IO.Path.Combine( first, "two" );
		var output = new StringWriter();
		var error = new StringWriter();
		var context = CreateContext( output, error );

		var status = await MkDirCommand.RunAsync(
			new[] { "--parents", "--verbose", second },
			context,
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			new FixedMaskProvider( FileCreationMask.None )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.True( Directory.Exists( first ) );
		Assert.True( Directory.Exists( second ) );
		Assert.Contains( string.Concat( "mkdir: created directory '", first, "'" ), output.ToString() );
		Assert.Contains( string.Concat( "mkdir: created directory '", second, "'" ), output.ToString() );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies that an explicit numeric mode bypasses the process umask.</summary>
	[Fact]
	public async Task AppliesExplicitNumericModeOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		using var temporary = new TemporaryDirectory();
		var directory = System.IO.Path.Combine( temporary.Path, "private" );
		var context = CreateContext( new StringWriter(), new StringWriter() );

		var status = await MkDirCommand.RunAsync(
			new[] { "--mode=700", directory },
			context,
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			new FixedMaskProvider( new FileCreationMask( 0x01ff ) )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		var actualMode = File.GetUnixFileMode( directory );
		var permissions = actualMode & (
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
		);
		Assert.Equal(
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
			permissions
		);
		Assert.Equal(
			UnixFileMode.None,
			actualMode & (
				UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
					| UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
			)
		);
	}

	/// <summary>Verifies that omitted symbolic subjects remain filtered by the supplied umask.</summary>
	[Fact]
	public async Task AppliesUmaskToOmittedSymbolicModeSubjectsOnUnix() {
		if ( OperatingSystem.IsWindows() ) return;
		using var temporary = new TemporaryDirectory();
		var directory = System.IO.Path.Combine( temporary.Path, "symbolic" );
		var context = CreateContext( new StringWriter(), new StringWriter() );

		var status = await MkDirCommand.RunAsync(
			new[] { "--mode=-w", directory },
			context,
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			new FixedMaskProvider( new FileCreationMask( Convert.ToInt32( "022", 8 ) ) )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		var ordinaryPermissions = File.GetUnixFileMode( directory ) & (
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
				| UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
				| UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
		);
		Assert.Equal(
			UnixFileMode.UserRead | UnixFileMode.UserExecute
				| UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
				| UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute,
			ordinaryPermissions
		);
	}

	/// <summary>Verifies GNU's owner-write-and-search rule for intermediate <c>-p</c> directories.</summary>
	[Fact]
	public async Task ParentDirectoriesRetainOwnerWriteAndSearch() {
		if ( OperatingSystem.IsWindows() ) return;
		using var temporary = new TemporaryDirectory();
		var parent = System.IO.Path.Combine( temporary.Path, "parent" );
		var child = System.IO.Path.Combine( parent, "child" );
		var context = CreateContext( new StringWriter(), new StringWriter() );

		var status = await MkDirCommand.RunAsync(
			new[] { "--parents", child },
			context,
			SystemFileSystemMutationProvider.Instance,
			SystemFileSystemMetadataProvider.Instance,
			new FixedMaskProvider( new FileCreationMask( Convert.ToInt32( "700", 8 ) ) )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		var ownerWriteSearch = UnixFileMode.UserWrite | UnixFileMode.UserExecute;
		Assert.Equal( ownerWriteSearch, File.GetUnixFileMode( parent ) & ownerWriteSearch );
		Assert.Equal( UnixFileMode.None, File.GetUnixFileMode( child ) & ownerWriteSearch );
	}

	/// <summary>Verifies deterministic failure when a destination already exists without <c>-p</c>.</summary>
	[Fact]
	public async Task ExistingDirectoryFailsWithoutParentsOption() {
		using var temporary = new TemporaryDirectory();
		var directory = System.IO.Path.Combine( temporary.Path, "existing" );
		Directory.CreateDirectory( directory );
		var error = new StringWriter();

		var status = await MkDirCommand.RunAsync(
			new[] { directory },
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "File exists", error.ToString() );
	}

	/// <summary>Verifies that an explicit unsupported security context receives a controlled warning.</summary>
	[Fact]
	public async Task ExplicitContextProducesControlledWarning() {
		using var temporary = new TemporaryDirectory();
		var directory = System.IO.Path.Combine( temporary.Path, "labeled" );
		var error = new StringWriter();

		var status = await MkDirCommand.RunAsync(
			new[] { "--context=system_u:object_r:tmp_t:s0", directory },
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Contains( "ignoring --context", error.ToString() );
	}

	/// <summary>Verifies GNU's quiet default-context behavior on hosts without labeling support.</summary>
	[Fact]
	public async Task DefaultContextOptionIsAControlledNoOp() {
		using var temporary = new TemporaryDirectory();
		var directory = System.IO.Path.Combine( temporary.Path, "default-context" );
		var error = new StringWriter();

		var status = await MkDirCommand.RunAsync(
			new[] { "-Z", directory },
			CreateContext( new StringWriter(), error )
		);

		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( string.Empty, error.ToString() );
	}

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( "mkdir", TextReader.Null, output, error );
	}

	private sealed class FixedMaskProvider : IFileCreationMaskProvider {
		private readonly FileCreationMask mask;

		public FixedMaskProvider( FileCreationMask mask ) {
			this.mask = mask;
		}

		public FileCreationMask GetCurrentMask() => mask;
	}

	private sealed class TemporaryDirectory : IDisposable {
		public TemporaryDirectory() {
			Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				string.Concat( "Icod-MkDir-", Guid.NewGuid().ToString( "N" ) )
			);
			Directory.CreateDirectory( Path );
		}

		public string Path { get; }

		public void Dispose() {
			try {
				Directory.Delete( Path, recursive: true );
			} catch ( IOException ) { }
			catch ( UnauthorizedAccessException ) { }
		}
	}
}
