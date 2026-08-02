using System.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;
using SystemPath = System.IO.Path;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Platform.Windows;

/// <summary>Exercises Shared filesystem observations against native Windows link objects.</summary>
public sealed class WindowsFileSystemLinkIntegrationTests {
	private const uint WindowsMountPointReparseTag = 0xa0000003;
	private const uint WindowsSymbolicLinkReparseTag = 0xa000000c;

	/// <summary>Verifies two Windows hard-link pathnames have one stable object identity and are not reparse points.</summary>
	[Fact]
	public async Task ReportsWindowsHardLinkIdentityAndCountWhenSupported() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		var root = CreateTemporaryDirectory();
		try {
			var target = SystemPath.Combine( root, "target.txt" );
			var hardLink = SystemPath.Combine( root, "hard-link.txt" );
			await File.WriteAllTextAsync( target, "content" );
			if ( !TryCreateHardLink( hardLink, target ) ) {
				return;
			}

			var provider = SystemFileSystemMetadataProvider.Instance;
			var targetMetadata = await provider.GetMetadataAsync( target, false );
			var linkMetadata = await provider.GetMetadataAsync( hardLink, false );

			Assert.Equal( FileSystemEntryKind.File, targetMetadata.Kind );
			Assert.Equal( FileSystemEntryKind.File, linkMetadata.Kind );
			Assert.False( linkMetadata.IsSymbolicLink );
			Assert.False( linkMetadata.IsPathIndirection );
			Assert.False( linkMetadata.IsReparsePoint );
			Assert.True( targetMetadata.EntryIdentity.IsAvailable );
			Assert.Equal( targetMetadata.EntryIdentity, linkMetadata.EntryIdentity );
			Assert.True( targetMetadata.LinkCount.IsAvailable );
			Assert.True( linkMetadata.LinkCount.IsAvailable );
			Assert.True( 2UL <= targetMetadata.LinkCount.GetRequiredValue() );
			Assert.Equal(
				targetMetadata.LinkCount.GetRequiredValue(),
				linkMetadata.LinkCount.GetRequiredValue()
			);
			Assert.True( targetMetadata.InodeNumber.IsAvailable );
			Assert.True( linkMetadata.InodeNumber.IsAvailable );
			Assert.Equal(
				targetMetadata.InodeNumber.GetRequiredValue(),
				linkMetadata.InodeNumber.GetRequiredValue()
			);
			Assert.True( targetMetadata.DeviceIdentifier.IsAvailable );
			Assert.True( linkMetadata.DeviceIdentifier.IsAvailable );
			Assert.Equal(
				targetMetadata.DeviceIdentifier.GetRequiredValue(),
				linkMetadata.DeviceIdentifier.GetRequiredValue()
			);
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies Windows file symbolic-link metadata preserves both link-object and followed-target identities.</summary>
	[Fact]
	public async Task ReportsWindowsSymbolicLinkObjectAndFollowedTargetWhenSupported() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		var root = CreateTemporaryDirectory();
		try {
			var target = SystemPath.Combine( root, "target.txt" );
			var link = SystemPath.Combine( root, "link.txt" );
			await File.WriteAllTextAsync( target, "content" );
			try {
				_ = File.CreateSymbolicLink( link, "target.txt" );
			} catch ( Exception exception ) when ( IsUnsupportedLinkCreation( exception ) ) {
				return;
			}

			var provider = SystemFileSystemMetadataProvider.Instance;
			var targetMetadata = await provider.GetMetadataAsync( target, false );
			var physical = await provider.GetMetadataAsync( link, false );
			var followed = await provider.GetMetadataAsync(
				link,
				true
			);

			Assert.Equal( FileSystemEntryKind.SymbolicLink, physical.Kind );
			Assert.True( physical.IsSymbolicLink );
			Assert.True( physical.IsPathIndirection );
			Assert.True( physical.IsReparsePoint );
			Assert.True( physical.ReparseTag.IsAvailable );
			Assert.Equal( WindowsSymbolicLinkReparseTag, physical.ReparseTag.GetRequiredValue() );
			Assert.True( physical.LinkTarget.IsAvailable );
			Assert.True( physical.LinkIdentity.IsAvailable );
			Assert.Equal( physical.EntryIdentity, physical.LinkIdentity.GetRequiredValue() );

			Assert.Equal( FileSystemEntryKind.File, followed.Kind );
			Assert.True( followed.WasDereferenced );
			Assert.Equal( targetMetadata.EntryIdentity, followed.EntryIdentity );
			Assert.True( followed.LinkIdentity.IsAvailable );
			Assert.Equal( physical.EntryIdentity, followed.LinkIdentity.GetRequiredValue() );
		} finally {
			Directory.Delete( root, true );
		}
	}

	/// <summary>Verifies Windows junction metadata remains distinct and deleting the junction does not delete its target.</summary>
	[Fact]
	public async Task ReportsWindowsJunctionAndPreservesTargetWhenRemoved() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		var root = CreateTemporaryDirectory();
		var junction = SystemPath.Combine( root, "junction" );
		try {
			var target = Directory.CreateDirectory( SystemPath.Combine( root, "target" ) ).FullName;
			var child = SystemPath.Combine( target, "child.txt" );
			await File.WriteAllTextAsync( child, "content" );
			if ( !TryCreateJunction( junction, target ) ) {
				return;
			}

			var provider = SystemFileSystemMetadataProvider.Instance;
			var targetMetadata = await provider.GetMetadataAsync( target, false );
			var physical = await provider.GetMetadataAsync( junction, false );
			var followed = await provider.GetMetadataAsync(
				junction,
				true
			);

			Assert.Equal( FileSystemEntryKind.NameSurrogate, physical.Kind );
			Assert.False( physical.IsSymbolicLink );
			Assert.True( physical.IsPathIndirection );
			Assert.True( physical.IsJunction );
			Assert.True( physical.IsReparsePoint );
			Assert.True( physical.ReparseTag.IsAvailable );
			Assert.Equal( WindowsMountPointReparseTag, physical.ReparseTag.GetRequiredValue() );
			Assert.Equal( FileSystemEntryKind.Directory, followed.Kind );
			Assert.True( followed.WasDereferenced );
			Assert.Equal( targetMetadata.EntryIdentity, followed.EntryIdentity );

			Directory.Delete( junction );
			Assert.False( Directory.Exists( junction ) );
			Assert.True( Directory.Exists( target ) );
			Assert.True( File.Exists( child ) );
		} finally {
			TryDeleteDirectoryLink( junction );
			Directory.Delete( root, true );
		}
	}

	private static bool TryCreateHardLink( string linkPath, string targetPath ) => RunMklink(
		"/H",
		linkPath,
		targetPath,
		() => File.Exists( linkPath )
	);

	private static bool TryCreateJunction( string junctionPath, string targetPath ) => RunMklink(
		"/J",
		junctionPath,
		targetPath,
		() => Directory.Exists( junctionPath )
	);

	private static bool RunMklink(
		string option,
		string linkPath,
		string targetPath,
		Func<bool> created
	) {
		try {
			var startInfo = new ProcessStartInfo( "cmd.exe" ) {
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			startInfo.ArgumentList.Add( "/d" );
			startInfo.ArgumentList.Add( "/c" );
			startInfo.ArgumentList.Add( "mklink" );
			startInfo.ArgumentList.Add( option );
			startInfo.ArgumentList.Add( linkPath );
			startInfo.ArgumentList.Add( targetPath );
			using var process = Process.Start( startInfo );
			if ( process is null ) {
				return false;
			}
			process.WaitForExit();
			return 0 == process.ExitCode && created();
		} catch ( Exception exception ) when ( IsUnsupportedLinkCreation( exception ) ) {
			return false;
		}
	}

	private static bool IsUnsupportedLinkCreation( Exception exception ) => exception is
		UnauthorizedAccessException
		or IOException
		or InvalidOperationException
		or System.ComponentModel.Win32Exception
		or PlatformNotSupportedException
		or NotSupportedException;

	private static void TryDeleteDirectoryLink( string path ) {
		try {
			if ( Directory.Exists( path ) ) {
				Directory.Delete( path );
			}
		} catch ( IOException ) {
			// Best-effort cleanup after a capability-gated test.
		} catch ( UnauthorizedAccessException ) {
			// Best-effort cleanup after a capability-gated test.
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = SystemPath.Combine(
			SystemPath.GetTempPath(),
			string.Concat( "Icod.Shared.Windows.Links.", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}
}
