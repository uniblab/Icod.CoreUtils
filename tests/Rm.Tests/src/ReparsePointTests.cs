namespace Icod.CoreUtils.Rm.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using RmCommand = Icod.CoreUtils.Rm.Command;
using Xunit;

/// <summary>Exercises physical-object removal for symbolic links and Windows junctions.</summary>
public sealed class ReparsePointTests {
	/// <summary>Verifies recursive removal prunes a directory symbolic link and preserves its target tree.</summary>
	[Fact]
	public async Task RecursiveRemovalDoesNotTraverseDirectorySymbolicLink() {
		var root = CreateTemporaryDirectory();
		var removalRoot = Directory.CreateDirectory( Path.Combine( root, "remove" ) ).FullName;
		var target = Directory.CreateDirectory( Path.Combine( root, "target" ) ).FullName;
		var link = Path.Combine( removalRoot, "link" );
		await File.WriteAllTextAsync( Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateDirectorySymbolicLinkAsync( link, target ) ) return;
			var status = await RunAsync( new[] { "--recursive", removalRoot } );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( Directory.Exists( removalRoot ) );
			Assert.True( File.Exists( Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( link );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a directory symbolic link can be removed as a physical name without recursive mode.</summary>
	[Fact]
	public async Task RemovesDirectorySymbolicLinkWithoutFollowingTarget() {
		var root = CreateTemporaryDirectory();
		var target = Directory.CreateDirectory( Path.Combine( root, "target" ) ).FullName;
		var link = Path.Combine( root, "link" );
		await File.WriteAllTextAsync( Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateDirectorySymbolicLinkAsync( link, target ) ) return;
			var status = await RunAsync( new[] { link } );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( PathObjectExists( link ) );
			Assert.True( File.Exists( Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( link );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies a Windows junction can be removed as a physical name without recursive mode.</summary>
	[Fact]
	public async Task RemovesWindowsJunctionWithoutFollowingTarget() {
		if ( !OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		var target = Directory.CreateDirectory( Path.Combine( root, "target" ) ).FullName;
		var junction = Path.Combine( root, "junction" );
		await File.WriteAllTextAsync( Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateJunctionAsync( junction, target ) ) return;
			var status = await RunAsync( new[] { junction } );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( PathObjectExists( junction ) );
			Assert.True( File.Exists( Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( junction );
			DeleteTree( root );
		}
	}

	/// <summary>Verifies recursive removal treats a Windows junction as a leaf and preserves its target.</summary>
	[Fact]
	public async Task RecursiveRemovalDoesNotTraverseWindowsJunction() {
		if ( !OperatingSystem.IsWindows() ) return;
		var root = CreateTemporaryDirectory();
		var removalRoot = Directory.CreateDirectory( Path.Combine( root, "remove" ) ).FullName;
		var target = Directory.CreateDirectory( Path.Combine( root, "target" ) ).FullName;
		var junction = Path.Combine( removalRoot, "junction" );
		await File.WriteAllTextAsync( Path.Combine( target, "sentinel" ), "target" );
		try {
			if ( !await TryCreateJunctionAsync( junction, target ) ) return;
			var status = await RunAsync( new[] { "--recursive", removalRoot } );
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.False( Directory.Exists( removalRoot ) );
			Assert.True( File.Exists( Path.Combine( target, "sentinel" ) ) );
		} finally {
			RemovePhysicalReparsePoint( junction );
			DeleteTree( root );
		}
	}

	private static ValueTask<int> RunAsync( string[] args ) => RmCommand.RunAsync(
		args,
		new CommandContext(
			"rm",
			TextReader.Null,
			new StringWriter(),
			new StringWriter()
		)
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
		var path = Path.Combine( Path.GetTempPath(), string.Concat( "Icod-Rm-Reparse-", Guid.NewGuid().ToString( "N" ) ) );
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
