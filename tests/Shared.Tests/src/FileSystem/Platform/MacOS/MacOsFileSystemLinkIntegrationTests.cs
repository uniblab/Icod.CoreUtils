using System.Diagnostics;
using System.Net.Sockets;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;
using SystemPath = System.IO.Path;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Platform.MacOS;

/// <summary>Exercises Shared filesystem observations against native macOS filesystem objects.</summary>
public sealed class MacOsFileSystemLinkIntegrationTests {
	/// <summary>Verifies macOS hard links share inode identity and link count without becoming pathname indirection.</summary>
	[Fact]
	public async Task ReportsMacOsHardLinkIdentityAndCountWhenSupported() {
		if ( !OperatingSystem.IsMacOS() ) {
			return;
		}

		var root = CreateTemporaryDirectory();
		try {
			var target = SystemPath.Combine( root, "target.txt" );
			var hardLink = SystemPath.Combine( root, "hard-link.txt" );
			await File.WriteAllTextAsync( target, "content" );
			if ( !TryRun( "/bin/ln", target, hardLink ) || !File.Exists( hardLink ) ) {
				return;
			}

			var provider = SystemFileSystemMetadataProvider.Instance;
			var targetMetadata = await provider.GetMetadataAsync( target, false );
			var linkMetadata = await provider.GetMetadataAsync( hardLink, false );

			Assert.Equal( FileSystemEntryKind.File, linkMetadata.Kind );
			Assert.False( linkMetadata.IsSymbolicLink );
			Assert.False( linkMetadata.IsPathIndirection );
			Assert.False( linkMetadata.IsReparsePoint );
			Assert.True( targetMetadata.EntryIdentity.IsAvailable );
			Assert.Equal( targetMetadata.EntryIdentity, linkMetadata.EntryIdentity );
			Assert.True( targetMetadata.InodeNumber.IsAvailable );
			Assert.True( linkMetadata.InodeNumber.IsAvailable );
			Assert.Equal(
				targetMetadata.InodeNumber.GetRequiredValue(),
				linkMetadata.InodeNumber.GetRequiredValue()
			);
			Assert.True( targetMetadata.LinkCount.IsAvailable );
			Assert.True( 2UL <= targetMetadata.LinkCount.GetRequiredValue() );
			Assert.Equal(
				targetMetadata.LinkCount.GetRequiredValue(),
				linkMetadata.LinkCount.GetRequiredValue()
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

	/// <summary>Verifies macOS symbolic-link metadata preserves the physical link identity while following the target.</summary>
	[Fact]
	public async Task ReportsMacOsSymbolicLinkObjectAndFollowedTargetWhenSupported() {
		if ( !OperatingSystem.IsMacOS() ) {
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
			Assert.False( physical.IsReparsePoint );
			Assert.True( physical.LinkTarget.IsAvailable );
			Assert.Equal( "target.txt", physical.LinkTarget.GetRequiredValue() );
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

	/// <summary>Verifies native macOS FIFOs and Unix-domain sockets remain distinguishable path objects.</summary>
	[Fact]
	public async Task ClassifiesMacOsFifoAndUnixDomainSocketWhenSupported() {
		if ( !OperatingSystem.IsMacOS() ) {
			return;
		}

		var root = CreateTemporaryDirectory();
		var fifo = SystemPath.Combine( root, "fifo" );
		var socketPath = SystemPath.Combine( root, "socket" );
		try {
			var mkfifo = File.Exists( "/usr/bin/mkfifo" ) ? "/usr/bin/mkfifo" : "/bin/mkfifo";
			if ( !TryRun( mkfifo, fifo ) ) {
				return;
			}

			Socket? socket = null;
			try {
				socket = new Socket(
					AddressFamily.Unix,
					SocketType.Stream,
					ProtocolType.Unspecified
				);
				socket.Bind( new UnixDomainSocketEndPoint( socketPath ) );
			} catch ( Exception exception ) when ( IsUnsupportedSocketCreation( exception ) ) {
				socket?.Dispose();
				return;
			}

			using ( socket ) {
				var provider = SystemFileSystemMetadataProvider.Instance;
				var fifoMetadata = await provider.GetMetadataAsync( fifo, false );
				var socketMetadata = await provider.GetMetadataAsync(
					socketPath,
					false
				);

				Assert.Equal( FileSystemEntryKind.Fifo, fifoMetadata.Kind );
				Assert.False( fifoMetadata.IsPathIndirection );
				Assert.Equal( FileSystemEntryKind.Socket, socketMetadata.Kind );
				Assert.False( socketMetadata.IsPathIndirection );
			}
		} finally {
			TryDeleteFileSystemObject( socketPath );
			TryDeleteFileSystemObject( fifo );
			Directory.Delete( root, true );
		}
	}

	private static bool TryRun( string fileName, params string[] arguments ) {
		try {
			var startInfo = new ProcessStartInfo( fileName ) {
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			foreach ( var argument in arguments ) {
				startInfo.ArgumentList.Add( argument );
			}
			using var process = Process.Start( startInfo );
			if ( process is null ) {
				return false;
			}
			process.WaitForExit();
			return 0 == process.ExitCode;
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

	private static bool IsUnsupportedSocketCreation( Exception exception ) => exception is
		SocketException
		or IOException
		or PlatformNotSupportedException
		or NotSupportedException;

	private static void TryDeleteFileSystemObject( string path ) {
		try {
			File.Delete( path );
		} catch ( IOException ) {
			// Best-effort cleanup after a capability-gated test.
		} catch ( UnauthorizedAccessException ) {
			// Best-effort cleanup after a capability-gated test.
		}
	}

	private static string CreateTemporaryDirectory() {
		var path = SystemPath.Combine(
			"/private/tmp",
			string.Concat( "icod-macos-links-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}
}
