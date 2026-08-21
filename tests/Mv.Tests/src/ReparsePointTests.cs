namespace Icod.CoreUtils.Mv.Tests;

using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using MvCommand = Icod.CoreUtils.Mv.Command;
using Xunit;

/// <summary>Exercises physical reparse-point moves through copy-and-remove fallback.</summary>
public sealed class ReparsePointTests {
	/// <summary>Verifies fallback move preserves a symbolic-link object and does not remove its target.</summary>
	[Fact]
	public async Task FallbackMovePreservesDirectorySymbolicLink() {
		var root = CreateTemporaryDirectory();
		var target = Directory.CreateDirectory( System.IO.Path.Combine( root, "target" ) ).FullName;
		var source = System.IO.Path.Combine( root, "source" );
		var destination = System.IO.Path.Combine( root, "destination" );
		await File.WriteAllTextAsync( System.IO.Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateDirectorySymbolicLinkAsync( source, target ) ) return;
			var status = await RunAsync( new[] { "--backup=simple", source, destination } );
			Assert.Equal( 0, status );
			Assert.False( PathObjectExists( source ) );
			var observation = await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync(
				destination,
				PathDereferenceMode.NoFollow
			);
			Assert.True( observation.IsSymbolicLink );
			Assert.True( File.Exists( System.IO.Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( source );
			RemovePhysicalReparsePoint( destination );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies fallback move preserves a Windows junction and removes only the source junction.</summary>
	[Fact]
	public async Task FallbackMovePreservesWindowsJunction() {
		if ( !OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		var target = Directory.CreateDirectory( System.IO.Path.Combine( root, "target" ) ).FullName;
		var source = System.IO.Path.Combine( root, "source" );
		var destination = System.IO.Path.Combine( root, "destination" );
		await File.WriteAllTextAsync( System.IO.Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateJunctionAsync( source, target ) ) return;
			var status = await RunAsync( new[] { "--backup=simple", source, destination } );
			Assert.Equal( 0, status );
			Assert.False( PathObjectExists( source ) );
			var observation = await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync(
				destination,
				PathDereferenceMode.NoFollow
			);
			Assert.True( observation.IsJunction );
			Assert.True( File.Exists( System.IO.Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( source );
			RemovePhysicalReparsePoint( destination );
			DeleteTree( root );
		}
	}

	private static ValueTask<int> RunAsync( string[] args ) => MvCommand.RunAsync(
		args,
		TextReader.Null,
		new StringWriter(),
		new StringWriter()
	);

	private static async ValueTask<bool> TryCreateDirectorySymbolicLinkAsync( string path, string target ) {
		var result = await SystemFileSystemMutationProvider.Instance.CreateSymbolicLinkAsync(
			path,
			target,
			targetIsDirectory: true,
			precondition: FileSystemMutationPrecondition.DestinationMustNotExist()
		);
		return result.Succeeded;
	}

	private static async ValueTask<bool> TryCreateJunctionAsync( string path, string target ) {
		var result = await SystemFileSystemMutationProvider.Instance.CreateJunctionAsync(
			path,
			target,
			FileSystemMutationPrecondition.DestinationMustNotExist()
		);
		return result.Succeeded;
	}

	private static bool PathObjectExists( string path ) {
		try {
			_ = File.GetAttributes( path );
			return true;
		} catch ( FileNotFoundException ) {
			return false;
		} catch ( DirectoryNotFoundException ) {
			return false;
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Mv-Reparse-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static void RemovePhysicalReparsePoint( string path ) {
		try {
			var attributes = File.GetAttributes( path );
			if ( (attributes & FileAttributes.ReparsePoint) == 0 ) return;
			if ( (attributes & FileAttributes.Directory) != 0 ) Directory.Delete( path, recursive: false );
			else File.Delete( path );
		} catch ( FileNotFoundException ) {
		} catch ( DirectoryNotFoundException ) {
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}

	private static void DeleteTree( string path ) {
		try {
			if ( Directory.Exists( path ) ) Directory.Delete( path, recursive: true );
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
	}
}
