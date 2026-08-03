namespace Icod.CoreUtils.Ln.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using LnCommand = Icod.CoreUtils.Ln.Command;
using Xunit;

/// <summary>Exercises GNU-compatible <c>ln</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Creates a symbolic link whose stored target is relative to the link location.</summary>
	[Fact]
	public async Task CreatesRelativeSymbolicLink() {
		using var temporary = new TemporaryDirectory();
		var source = Path.Combine( temporary.Path, "source" );
		var links = Path.Combine( temporary.Path, "links" );
		Directory.CreateDirectory( links );
		File.WriteAllText( source, "data" );
		if ( !await CanCreateSymbolicLinksAsync( source, temporary.Path ) ) {
			return;
		}
		var link = Path.Combine( links, "item" );
		var error = new StringWriter();
		var status = await LnCommand.RunAsync( new[] { "-s", "-r", source, link }, new CommandContext( "ln", TextReader.Null, TextWriter.Null, error ) );
		Assert.True( status == CommandExitCodes.Success, error.ToString() );
		Assert.Equal( "data", File.ReadAllText( link ) );
		Assert.False( Path.IsPathFullyQualified( new FileInfo( link ).LinkTarget! ) );
	}

	/// <summary>Creates multiple links in an explicit target directory.</summary>
	[Fact]
	public async Task CreatesMultipleLinksInTargetDirectory() {
		using var temporary = new TemporaryDirectory();
		var first = Path.Combine( temporary.Path, "first" );
		var second = Path.Combine( temporary.Path, "second" );
		var target = Path.Combine( temporary.Path, "target" );
		File.WriteAllText( first, "1" ); File.WriteAllText( second, "2" ); Directory.CreateDirectory( target );
		var status = await LnCommand.RunAsync( new[] { "-t", target, first, second }, new CommandContext( "ln", TextReader.Null, TextWriter.Null, new StringWriter() ) );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( "1", File.ReadAllText( Path.Combine( target, "first" ) ) );
		Assert.Equal( "2", File.ReadAllText( Path.Combine( target, "second" ) ) );
	}

	/// <summary>Backs up and replaces an existing destination.</summary>
	[Fact]
	public async Task BacksUpExistingDestination() {
		using var temporary = new TemporaryDirectory();
		var source = Path.Combine( temporary.Path, "source" );
		var destination = Path.Combine( temporary.Path, "destination" );
		File.WriteAllText( source, "new" ); File.WriteAllText( destination, "old" );
		var status = await LnCommand.RunAsync( new[] { "-b", source, destination }, new CommandContext( "ln", TextReader.Null, TextWriter.Null, new StringWriter() ) );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( "old", File.ReadAllText( string.Concat( destination, "~" ) ) );
		Assert.Equal( "new", File.ReadAllText( destination ) );
	}

	private static async ValueTask<bool> CanCreateSymbolicLinksAsync( string target, string directory ) {
		var probe = Path.Combine( directory, string.Concat( ".ln-symlink-probe-", Guid.NewGuid().ToString( "N" ) ) );
		var result = await SystemFileSystemMutationProvider.Instance.CreateSymbolicLinkAsync(
			probe,
			target,
			false,
			FileSystemMutationPrecondition.DestinationMustNotExist()
		);
		if ( result.Succeeded ) {
			File.Delete( probe );
			return true;
		}
		if (
			OperatingSystem.IsWindows()
			&& result.ErrorCode is FileSystemMutationErrorCode.PrivilegeRequired
				or FileSystemMutationErrorCode.AccessDenied
		) {
			return false;
		}
		Assert.True( result.Succeeded, result.Message ?? result.ErrorCode.ToString() );
		return false;
	}
	private sealed class TemporaryDirectory : IDisposable {
		public TemporaryDirectory() { Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Ln-", Guid.NewGuid().ToString( "N" ) ) ); Directory.CreateDirectory( Path ); }
		public string Path { get; }
		public void Dispose() { try { Directory.Delete( Path, true ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { } }
	}
}
