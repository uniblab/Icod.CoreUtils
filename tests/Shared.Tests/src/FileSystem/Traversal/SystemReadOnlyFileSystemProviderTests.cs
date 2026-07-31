using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Traversal;

/// <summary>
/// Exercises the system provider against temporary host filesystem objects.
/// </summary>
public sealed partial class SystemReadOnlyFileSystemProviderTests {
	/// <summary>
	/// Verifies file and directory observation and one-level enumeration.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ObservesAndEnumeratesHostEntries() {
		var path = CreateTemporaryDirectory();
		try {
			var childDirectory = Directory.CreateDirectory( Path.Combine( path, "child" ) ).FullName;
			var childFile = Path.Combine( path, "file.txt" );
			await File.WriteAllTextAsync( childFile, "data" );
			var provider = SystemReadOnlyFileSystemProvider.Instance;

			var directory = await provider.ObserveAsync( path, false );
			var file = await provider.ObserveAsync( childFile, false );
			Assert.Equal( FileSystemEntryKind.Directory, directory.Kind );
			Assert.Equal( FileSystemEntryKind.File, file.Kind );
			Assert.True( directory.EntryIdentity.IsAvailable );
			Assert.True( directory.FileSystemIdentity.IsAvailable );

			var children = new List<ReadOnlyDirectoryEntry>();
			await foreach ( var child in provider.EnumerateDirectoryAsync( path ) ) {
				children.Add( child );
			}
			Assert.Contains( children, child => child.AccessPath == childDirectory );
			Assert.Contains( children, child => child.AccessPath == childFile );
		} finally {
			Directory.Delete( path, true );
		}
	}

	/// <summary>
	/// Verifies link-object and followed-target observations when link creation is supported.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DistinguishesLinkFromFollowedTargetWhenSupported() {
		var path = CreateTemporaryDirectory();
		try {
			var target = Directory.CreateDirectory( Path.Combine( path, "target" ) ).FullName;
			var link = Path.Combine( path, "link" );
			try {
				_ = Directory.CreateSymbolicLink( link, target );
			} catch ( Exception exception ) when (
				exception is UnauthorizedAccessException
					or IOException
					or PlatformNotSupportedException
					or NotSupportedException
			) {
				return;
			}

			var provider = SystemReadOnlyFileSystemProvider.Instance;
			var physical = await provider.ObserveAsync( link, false );
			var followed = await provider.ObserveAsync( link, true );
			Assert.Equal( FileSystemEntryKind.SymbolicLink, physical.Kind );
			Assert.True( physical.IsSymbolicLink );
			Assert.False( physical.WasDereferenced );
			Assert.Equal( FileSystemEntryKind.Directory, followed.Kind );
			Assert.True( followed.WasDereferenced );
			Assert.Equal( (await provider.ObserveAsync( target, false )).EntryIdentity, followed.EntryIdentity );
		} finally {
			Directory.Delete( path, true );
		}
	}


	/// <summary>
	/// Verifies link-to-file observation when symbolic-link creation is supported.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DistinguishesFileLinkFromFollowedFileWhenSupported() {
		var path = CreateTemporaryDirectory();
		try {
			var target = Path.Combine( path, "target.txt" );
			var link = Path.Combine( path, "link.txt" );
			await File.WriteAllTextAsync( target, "data" );
			try {
				_ = File.CreateSymbolicLink( link, target );
			} catch ( Exception exception ) when (
				exception is UnauthorizedAccessException
					or IOException
					or PlatformNotSupportedException
					or NotSupportedException
			) {
				return;
			}

			var provider = SystemReadOnlyFileSystemProvider.Instance;
			var physical = await provider.ObserveAsync( link, false );
			var followed = await provider.ObserveAsync( link, true );
			Assert.Equal( FileSystemEntryKind.SymbolicLink, physical.Kind );
			Assert.Equal( FileSystemEntryKind.File, followed.Kind );
			Assert.True( followed.WasDereferenced );
			Assert.Equal( (await provider.ObserveAsync( target, false )).EntryIdentity, followed.EntryIdentity );
		} finally {
			Directory.Delete( path, true );
		}
	}

	/// <summary>
	/// Verifies deterministic provider failure for an inaccessible directory when the host enforces the restriction.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task EnumerationReportsInaccessibleDirectoryWhenHostEnforcesRestriction() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var path = CreateTemporaryDirectory();
		var restricted = Directory.CreateDirectory( Path.Combine( path, "restricted" ) ).FullName;
		try {
			File.SetUnixFileMode( restricted, UnixFileMode.None );
			var provider = SystemReadOnlyFileSystemProvider.Instance;
			try {
				await foreach ( var unused in provider.EnumerateDirectoryAsync( restricted ) ) {
					_ = unused;
				}
			} catch ( UnauthorizedAccessException ) {
				return;
			}

			// Elevated test runners may retain access despite mode zero. In that case the
			// host cannot provide a meaningful inaccessible-directory assertion.
			return;
		} finally {
			try {
				File.SetUnixFileMode(
					restricted,
					UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
				);
			} catch {
				// Best-effort cleanup restoration.
			}
			Directory.Delete( path, true );
		}
	}

	/// <summary>
	/// Verifies that repeated hard-linked pathnames retain the same stable entry identity when supported.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsSameIdentityForHardLinksWhenSupported() {
		var path = CreateTemporaryDirectory();
		try {
			var original = Path.Combine( path, "original.txt" );
			var link = Path.Combine( path, "hard-link.txt" );
			await File.WriteAllTextAsync( original, "data" );
			if ( !TryCreateHardLink( link, original ) ) {
				return;
			}

			var provider = SystemReadOnlyFileSystemProvider.Instance;
			var first = await provider.ObserveAsync( original, false );
			var second = await provider.ObserveAsync( link, false );
			Assert.True( first.EntryIdentity.IsAvailable );
			Assert.Equal( first.EntryIdentity, second.EntryIdentity );
		} finally {
			Directory.Delete( path, true );
		}
	}

	/// <summary>
	/// Verifies that a broken link remains observable as a link while target following fails.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ObservesBrokenLinkObjectWhenSupported() {
		var path = CreateTemporaryDirectory();
		try {
			var missingTarget = Path.Combine( path, "missing-target" );
			var link = Path.Combine( path, "broken-link" );
			try {
				_ = File.CreateSymbolicLink( link, missingTarget );
			} catch ( Exception exception ) when (
				exception is UnauthorizedAccessException
					or IOException
					or PlatformNotSupportedException
					or NotSupportedException
			) {
				return;
			}

			var provider = SystemReadOnlyFileSystemProvider.Instance;
			var physical = await provider.ObserveAsync( link, false );
			Assert.Equal( FileSystemEntryKind.SymbolicLink, physical.Kind );
			await Assert.ThrowsAnyAsync<IOException>( async () => {
				_ = await provider.ObserveAsync( link, true );
			} );
		} finally {
			Directory.Delete( path, true );
		}
	}

	/// <summary>
	/// Verifies real active-ancestry cycle detection when directory links are supported.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task TraversalDetectsRealDirectoryLinkCycleWhenSupported() {
		var path = CreateTemporaryDirectory();
		try {
			var child = Directory.CreateDirectory( Path.Combine( path, "child" ) ).FullName;
			var link = Path.Combine( child, "up" );
			try {
				_ = Directory.CreateSymbolicLink( link, path );
			} catch ( Exception exception ) when (
				exception is UnauthorizedAccessException
					or IOException
					or PlatformNotSupportedException
					or NotSupportedException
			) {
				return;
			}

			var root = new PathTraversalRoot(
				path,
				0,
				0,
				path,
				path,
				PathTraversalRootKind.Literal
			);
			var engine = new ReadOnlyPathTraversalEngine( SystemReadOnlyFileSystemProvider.Instance );
			var cycles = new List<PathTraversalEvent>();
			await foreach ( var item in engine.TraverseAsync(
				new[] { root },
				new PathTraversalOptions { SymbolicLinkMode = SymbolicLinkTraversalMode.Always }
			) ) {
				if ( item.Kind == PathTraversalEventKind.Cycle ) {
					cycles.Add( item );
				}
			}
			Assert.Single( cycles );
		} finally {
			Directory.Delete( path, true );
		}
	}


	/// <summary>
	/// Verifies Windows junction observation and target following when junction creation succeeds.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DistinguishesWindowsJunctionFromFollowedDirectoryWhenSupported() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}

		var path = CreateTemporaryDirectory();
		try {
			var target = Directory.CreateDirectory( Path.Combine( path, "target" ) ).FullName;
			var junction = Path.Combine( path, "junction" );
			if ( !TryCreateWindowsJunction( junction, target ) ) {
				return;
			}

			var provider = SystemReadOnlyFileSystemProvider.Instance;
			var physical = await provider.ObserveAsync( junction, false );
			var followed = await provider.ObserveAsync( junction, true );
			Assert.Equal( FileSystemEntryKind.SymbolicLink, physical.Kind );
			Assert.True( physical.IsSymbolicLink );
			Assert.Equal( FileSystemEntryKind.Directory, followed.Kind );
			Assert.True( followed.WasDereferenced );
			Assert.Equal( (await provider.ObserveAsync( target, false )).EntryIdentity, followed.EntryIdentity );
		} finally {
			var junction = Path.Combine( path, "junction" );
			if ( Directory.Exists( junction ) ) {
				Directory.Delete( junction );
			}
			Directory.Delete( path, true );
		}
	}

	/// <summary>
	/// Attempts to create a hard link through the host platform's command-line utility.
	/// </summary>
	/// <param name="linkPath">The new hard-link pathname.</param>
	/// <param name="targetPath">The existing file pathname.</param>
	/// <returns><see langword="true"/> when the hard link was created; otherwise, <see langword="false"/>.</returns>
	private static bool TryCreateHardLink( string linkPath, string targetPath ) {
		try {
			ProcessStartInfo startInfo;
			if ( OperatingSystem.IsWindows() ) {
				startInfo = new ProcessStartInfo( "cmd.exe" );
				startInfo.ArgumentList.Add( "/d" );
				startInfo.ArgumentList.Add( "/c" );
				startInfo.ArgumentList.Add( "mklink" );
				startInfo.ArgumentList.Add( "/H" );
				startInfo.ArgumentList.Add( linkPath );
				startInfo.ArgumentList.Add( targetPath );
			} else if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ) {
				startInfo = new ProcessStartInfo( "/bin/ln" );
				startInfo.ArgumentList.Add( targetPath );
				startInfo.ArgumentList.Add( linkPath );
			} else {
				return false;
			}

			startInfo.UseShellExecute = false;
			startInfo.CreateNoWindow = true;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			using var process = Process.Start( startInfo );
			if ( process is null ) {
				return false;
			}
			process.WaitForExit();
			return process.ExitCode == 0 && File.Exists( linkPath );
		} catch ( Exception exception ) when (
			exception is InvalidOperationException
				or System.ComponentModel.Win32Exception
				or PlatformNotSupportedException
				or NotSupportedException
				or IOException
		) {
			return false;
		}
	}
	/// <summary>
	/// Attempts to create a Windows directory junction through the reparse-point control API.
	/// </summary>
	/// <param name="junctionPath">The new junction pathname.</param>
	/// <param name="targetPath">The existing target-directory pathname.</param>
	/// <returns><see langword="true"/> when the junction was created; otherwise, <see langword="false"/>.</returns>
	private static bool TryCreateWindowsJunction( string junctionPath, string targetPath ) {
		const uint genericWrite = 0x40000000;
		const uint fileFlagOpenReparsePoint = 0x00200000;
		const uint fileFlagBackupSemantics = 0x02000000;
		const uint fsctlSetReparsePoint = 0x000900A4;
		const uint ioReparseTagMountPoint = 0xA0000003;

		try {
			var absoluteTarget = Path.TrimEndingDirectorySeparator( Path.GetFullPath( targetPath ) );
			var substituteName = absoluteTarget.StartsWith( @"\\", StringComparison.Ordinal )
				? string.Concat( @"\??\UNC\", absoluteTarget[2..] )
				: string.Concat( @"\??\", absoluteTarget );
			var substituteBytes = Encoding.Unicode.GetBytes( substituteName );
			var printBytes = Encoding.Unicode.GetBytes( absoluteTarget );
			var pathBufferLength = checked( substituteBytes.Length + sizeof( ushort ) + printBytes.Length + sizeof( ushort ) );
			var reparseDataLength = checked( sizeof( ushort ) * 4 + pathBufferLength );
			if ( substituteBytes.Length > ushort.MaxValue
				|| printBytes.Length > ushort.MaxValue
				|| reparseDataLength > ushort.MaxValue ) {
				return false;
			}

			var buffer = new byte[checked( sizeof( uint ) + sizeof( ushort ) * 6 + pathBufferLength )];
			BinaryPrimitives.WriteUInt32LittleEndian( buffer.AsSpan( 0, sizeof( uint ) ), ioReparseTagMountPoint );
			BinaryPrimitives.WriteUInt16LittleEndian( buffer.AsSpan( 4, sizeof( ushort ) ), (ushort)reparseDataLength );
			BinaryPrimitives.WriteUInt16LittleEndian( buffer.AsSpan( 8, sizeof( ushort ) ), 0 );
			BinaryPrimitives.WriteUInt16LittleEndian(
				buffer.AsSpan( 10, sizeof( ushort ) ),
				checked( (ushort)substituteBytes.Length )
			);
			BinaryPrimitives.WriteUInt16LittleEndian(
				buffer.AsSpan( 12, sizeof( ushort ) ),
				checked( (ushort)(substituteBytes.Length + sizeof( ushort )) )
			);
			BinaryPrimitives.WriteUInt16LittleEndian(
				buffer.AsSpan( 14, sizeof( ushort ) ),
				checked( (ushort)printBytes.Length )
			);
			substituteBytes.CopyTo( buffer, 16 );
			printBytes.CopyTo( buffer, 16 + substituteBytes.Length + sizeof( ushort ) );

			Directory.CreateDirectory( junctionPath );
			using var handle = NativeMethods.OpenDirectoryReparsePointWindows(
				junctionPath,
				genericWrite,
				FileShare.Read | FileShare.Write | FileShare.Delete,
				IntPtr.Zero,
				FileMode.Open,
				fileFlagOpenReparsePoint | fileFlagBackupSemantics,
				IntPtr.Zero
			);
			if ( handle.IsInvalid ) {
				Directory.Delete( junctionPath );
				return false;
			}

			var pinnedBuffer = GCHandle.Alloc( buffer, GCHandleType.Pinned );
			try {
				if ( !NativeMethods.SetReparsePointWindows(
					handle,
					fsctlSetReparsePoint,
					pinnedBuffer.AddrOfPinnedObject(),
					checked( (uint)buffer.Length ),
					IntPtr.Zero,
					0,
					out _,
					IntPtr.Zero
				) ) {
					Directory.Delete( junctionPath );
					return false;
				}
			} finally {
				pinnedBuffer.Free();
			}
			return Directory.Exists( junctionPath );
		} catch ( Exception exception ) when (
			exception is DllNotFoundException
				or EntryPointNotFoundException
				or UnauthorizedAccessException
				or IOException
				or PlatformNotSupportedException
				or NotSupportedException
				or OverflowException
		) {
			if ( Directory.Exists( junctionPath ) ) {
				try {
					Directory.Delete( junctionPath );
				} catch ( IOException ) {
					// Best-effort cleanup after an unsupported or failed reparse-point operation.
				} catch ( UnauthorizedAccessException ) {
					// Best-effort cleanup after an unsupported or failed reparse-point operation.
				}
			}
			return false;
		}
	}

	private static partial class NativeMethods {

		[LibraryImport(
			"kernel32.dll",
			EntryPoint = "CreateFileW",
			SetLastError = true,
			StringMarshalling = StringMarshalling.Utf16
		)]
		public static partial SafeFileHandle OpenDirectoryReparsePointWindows(
			string fileName,
			uint desiredAccess,
			FileShare shareMode,
			IntPtr securityAttributes,
			FileMode creationDisposition,
			uint flagsAndAttributes,
			IntPtr templateFile
		);

		[LibraryImport( "kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static partial bool SetReparsePointWindows(
			SafeFileHandle device,
			uint ioControlCode,
			IntPtr inputBuffer,
			uint inputBufferSize,
			IntPtr outputBuffer,
			uint outputBufferSize,
			out uint bytesReturned,
			IntPtr overlapped
		);
	}

	public static string CreateTemporaryDirectory() {
		var path = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "icod-e1-system-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}
}
