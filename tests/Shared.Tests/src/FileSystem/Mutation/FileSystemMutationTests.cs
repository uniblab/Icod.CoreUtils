using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Mutation;

/// <summary>
/// Exercises the Completion Gate E4 system mutation provider and its E3R preconditions.
/// </summary>
public sealed class FileSystemMutationTests {
	/// <summary>Verifies ordinary file and directory creation, mode application, and removal.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CreatesAndRemovesOrdinaryEntries() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var file = System.IO.Path.Combine( root, "sample.txt" );
			var directory = System.IO.Path.Combine( root, "sample-directory" );

			var fileCreation = await provider.CreateFileAsync(
				file,
				new PosixFileMode( 0x01b6 ),
				new FileCreationMask( 0x0012 )
			);
			var directoryCreation = await provider.CreateDirectoryAsync(
				directory,
				new PosixFileMode( 0x01ff ),
				new FileCreationMask( 0x0012 )
			);

			Assert.True( fileCreation.Supported, fileCreation.Message );
			Assert.True( fileCreation.Succeeded, fileCreation.Message );
			Assert.Equal( FileSystemEntryKind.File, fileCreation.Outcome!.Kind );
			Assert.True( File.Exists( file ) );
			Assert.True( directoryCreation.Supported, directoryCreation.Message );
			Assert.True( directoryCreation.Succeeded, directoryCreation.Message );
			Assert.Equal( FileSystemEntryKind.Directory, directoryCreation.Outcome!.Kind );
			Assert.True( Directory.Exists( directory ) );

			if ( !OperatingSystem.IsWindows() ) {
				Assert.Equal( 0x01a4, (int)File.GetUnixFileMode( file ) & 0x01ff );
				Assert.Equal( 0x01ed, (int)File.GetUnixFileMode( directory ) & 0x01ff );
			} else {
				Assert.False( fileCreation.Outcome.ModeApplied.GetValueOrDefault( true ) );
				Assert.False( directoryCreation.Outcome.ModeApplied.GetValueOrDefault( true ) );
			}

			var fileRemoval = await provider.RemoveFileAsync( file );
			var directoryRemoval = await provider.RemoveDirectoryAsync( directory );

			Assert.True( fileRemoval.Succeeded, fileRemoval.Message );
			Assert.True( directoryRemoval.Succeeded, directoryRemoval.Message );
			Assert.False( File.Exists( file ) );
			Assert.False( Directory.Exists( directory ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies exclusive creation never replaces an existing destination.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsExistingCreationDestinations() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var file = System.IO.Path.Combine( root, "existing.txt" );
			var directory = System.IO.Path.Combine( root, "existing-directory" );
			await File.WriteAllTextAsync( file, "original" );
			Directory.CreateDirectory( directory );

			var fileResult = await provider.CreateFileAsync(
				file,
				new PosixFileMode( 0x01b6 ),
				FileCreationMask.None
			);
			var directoryResult = await provider.CreateDirectoryAsync(
				directory,
				new PosixFileMode( 0x01ff ),
				FileCreationMask.None
			);

			Assert.False( fileResult.Succeeded );
			Assert.Equal( FileSystemMutationErrorCode.AlreadyExists, fileResult.ErrorCode );
			Assert.Equal( "original", await File.ReadAllTextAsync( file ) );
			Assert.False( directoryResult.Succeeded );
			Assert.Equal( FileSystemMutationErrorCode.AlreadyExists, directoryResult.ErrorCode );
			Assert.True( Directory.Exists( directory ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies hard-link creation preserves the source entry identity.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CreatesHardLinksToObservedEntries() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var source = System.IO.Path.Combine( root, "source.txt" );
			var link = System.IO.Path.Combine( root, "link.txt" );
			await File.WriteAllTextAsync( source, "content" );

			var sourceMetadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				source,
				PathDereferenceMode.NoFollow
			);
			var result = await provider.CreateHardLinkAsync(
				link,
				source,
				PathDereferenceMode.NoFollow,
				existingPathPrecondition: FileSystemMutationPrecondition.FromObservation(
					sourceMetadata.Kind,
					sourceMetadata.EntryIdentity,
					PathDereferenceMode.NoFollow
				)
			);

			Assert.True( result.Supported, result.Message );
			Assert.True( result.Succeeded, result.Message );
			Assert.Equal( FileSystemEntryKind.File, result.Outcome!.Kind );
			Assert.Equal( "content", await File.ReadAllTextAsync( link ) );
			var linkMetadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				link,
				PathDereferenceMode.NoFollow
			);
			if ( sourceMetadata.EntryIdentity.IsAvailable && linkMetadata.EntryIdentity.IsAvailable ) {
				Assert.Equal( sourceMetadata.EntryIdentity, linkMetadata.EntryIdentity );
			}
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies symbolic links are observed and removed without dereferencing their targets.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CreatesAndRemovesSymbolicLinkObjects() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var link = System.IO.Path.Combine( root, "dangling-link" );
			var result = await provider.CreateSymbolicLinkAsync( link, "missing-target", false );

			if ( !result.Succeeded ) {
				Assert.True(
					!result.Supported
						|| result.ErrorCode is FileSystemMutationErrorCode.AccessDenied
							or FileSystemMutationErrorCode.PrivilegeRequired,
					result.Message
				);
				return;
			}

			Assert.Equal( FileSystemEntryKind.SymbolicLink, result.Outcome!.Kind );
			var removal = await provider.RemoveFileAsync( link );
			Assert.True( removal.Succeeded, removal.Message );
			Assert.False( File.Exists( link ) );
			Assert.False( Directory.Exists( link ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies Windows junction creation, classification, traversal, and physical removal.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CreatesAndRemovesWindowsDirectoryJunctions() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var target = System.IO.Path.Combine( root, "junction-target" );
			var junction = System.IO.Path.Combine( root, "junction" );
			Directory.CreateDirectory( target );

			var result = await provider.CreateJunctionAsync( junction, target );
			if ( !OperatingSystem.IsWindows() ) {
				Assert.False( result.Supported );
				Assert.Equal( FileSystemMutationErrorCode.Unsupported, result.ErrorCode );
				return;
			}

			Assert.True( result.Supported, result.Message );
			Assert.True( result.Succeeded, result.Message );
			Assert.Equal( FileSystemMutationOperation.CreateJunction, result.Outcome!.Operation );
			var metadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				junction,
				PathDereferenceMode.NoFollow
			);
			Assert.True( metadata.IsJunction );
			Assert.False( metadata.WasDereferenced );

			var child = System.IO.Path.Combine( junction, "child.txt" );
			await File.WriteAllTextAsync( child, "through-junction" );
			Assert.Equal(
				"through-junction",
				await File.ReadAllTextAsync( System.IO.Path.Combine( target, "child.txt" ) )
			);

			var removal = await provider.RemoveFileAsync( junction );
			Assert.True( removal.Succeeded, removal.Message );
			Assert.False( Directory.Exists( junction ) );
			Assert.True( Directory.Exists( target ) );
			Assert.True( File.Exists( System.IO.Path.Combine( target, "child.txt" ) ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies FIFO creation is real on Unix and explicitly unsupported elsewhere.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task CreatesFifoOrReportsUnsupportedCapability() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var fifo = System.IO.Path.Combine( root, "channel" );
			var result = await provider.CreateFifoAsync(
				fifo,
				new PosixFileMode( 0x01b6 ),
				new FileCreationMask( 0x0012 )
			);

			if ( !provider.Capabilities.CanCreateFifos ) {
				Assert.False( result.Supported );
				Assert.Equal( FileSystemMutationErrorCode.Unsupported, result.ErrorCode );
				return;
			}

			Assert.True( result.Supported, result.Message );
			Assert.True( result.Succeeded, result.Message );
			Assert.Equal( FileSystemEntryKind.Fifo, result.Outcome!.Kind );
			var removal = await provider.RemoveFileAsync( fifo );
			Assert.True( removal.Succeeded, removal.Message );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies mode mutation follows an explicit terminal-indirection policy.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task SetsModesUnderExplicitDereferencePolicy() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var file = System.IO.Path.Combine( root, "mode.txt" );
			await File.WriteAllTextAsync( file, "mode" );
			var observed = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				file,
				PathDereferenceMode.NoFollow
			);
			var result = await provider.SetModeAsync(
				file,
				new PosixFileMode( 0x01a4 ),
				PathDereferenceMode.NoFollow,
				FileSystemMutationPrecondition.FromObservation(
					observed.Kind,
					observed.EntryIdentity,
					PathDereferenceMode.NoFollow
				)
			);

			Assert.True( result.Succeeded, result.Message );
			Assert.Equal( 0x01a4, (int)File.GetUnixFileMode( file ) & 0x01ff );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies stale E3 identities prevent mutation of a replacement pathname object.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsStaleIdentityBeforeRemoval() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var path = System.IO.Path.Combine( root, "target.txt" );
			var replacement = System.IO.Path.Combine( root, "replacement.txt" );
			await File.WriteAllTextAsync( path, "original" );
			await File.WriteAllTextAsync( replacement, "replacement" );

			var original = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				path,
				PathDereferenceMode.NoFollow
			);
			var replacementMetadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				replacement,
				PathDereferenceMode.NoFollow
			);
			if ( !original.EntryIdentity.IsAvailable || !replacementMetadata.EntryIdentity.IsAvailable ) {
				return;
			}
			Assert.NotEqual( original.EntryIdentity, replacementMetadata.EntryIdentity );

			File.Delete( path );
			File.Move( replacement, path );
			var result = await provider.RemoveFileAsync(
				path,
				FileSystemMutationPrecondition.FromObservation(
					original.Kind,
					original.EntryIdentity,
					PathDereferenceMode.NoFollow
				)
			);

			Assert.False( result.Succeeded );
			Assert.Equal( FileSystemMutationErrorCode.IdentityChanged, result.ErrorCode );
			Assert.True( File.Exists( path ) );
			Assert.Equal( "replacement", await File.ReadAllTextAsync( path ) );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies file-versus-directory mistakes and nonempty removal receive controlled results.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsControlledRemovalFailures() {
		var root = CreateTemporaryDirectory();
		try {
			var provider = SystemFileSystemMutationProvider.Instance;
			var file = System.IO.Path.Combine( root, "file.txt" );
			var directory = System.IO.Path.Combine( root, "directory" );
			await File.WriteAllTextAsync( file, "file" );
			Directory.CreateDirectory( directory );
			await File.WriteAllTextAsync( System.IO.Path.Combine( directory, "child.txt" ), "child" );

			var wrongKind = await provider.RemoveDirectoryAsync( file );
			var nonempty = await provider.RemoveDirectoryAsync( directory );

			Assert.False( wrongKind.Succeeded );
			Assert.Equal( FileSystemMutationErrorCode.WrongObjectKind, wrongKind.ErrorCode );
			Assert.False( nonempty.Succeeded );
			Assert.Equal( FileSystemMutationErrorCode.DirectoryNotEmpty, nonempty.ErrorCode );
		} finally {
			DeleteTree( root );
		}
	}

	/// <summary>Verifies platform capability reporting does not emulate special-file success.</summary>
	[Fact]
	public void ReportsPlatformCapabilities() {
		var capabilities = SystemFileSystemMutationProvider.Instance.Capabilities;

		Assert.True( capabilities.CanCreateDirectories );
		Assert.True( capabilities.CanCreateFiles );
		Assert.True( capabilities.CanCreateHardLinks );
		Assert.True( capabilities.CanCreateSymbolicLinks );
		Assert.Equal( OperatingSystem.IsWindows(), capabilities.CanCreateJunctions );
		Assert.True( capabilities.CanRemoveFiles );
		Assert.True( capabilities.CanRemoveDirectories );
		if ( OperatingSystem.IsWindows() ) {
			Assert.False( capabilities.CanCreateFifos );
			Assert.False( capabilities.CanCreateDeviceNodes );
			Assert.False( capabilities.CanSetModes );
		} else {
			Assert.True( capabilities.CanCreateFifos );
			Assert.True( capabilities.CanCreateDeviceNodes );
			Assert.True( capabilities.CanSetModes );
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod.CoreUtils.E4.", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}

	private static void DeleteTree( string path ) {
		try {
			if ( Directory.Exists( path ) ) {
				Directory.Delete( path, true );
			}
		} catch {
			// Preserve the primary test failure.
		}
	}
}
