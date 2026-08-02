extern alias IcodPath;

using System.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using PathIndirectionKind = IcodPath::Icod.Path.PathIndirectionKind;
using WindowsReparseTags = IcodPath::Icod.Path.WindowsReparseTags;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Metadata;

/// <summary>Exercises metadata characterization for Windows reparse objects.</summary>
public sealed class ReparsePointMetadataTests {
	/// <summary>Verifies physical junction metadata remains distinct from symbolic-link metadata.</summary>
	[Fact]
	public async Task ReportsWindowsJunctionWithoutCallingItSymbolicLinkWhenSupported() {
		if ( !OperatingSystem.IsWindows() ) {
			return;
		}
		var root = CreateTemporaryDirectory();
		var junction = Path.Combine( root, "junction" );
		try {
			var target = Directory.CreateDirectory( Path.Combine( root, "target" ) ).FullName;
			if ( !TryCreateJunction( junction, target ) ) {
				return;
			}

			var provider = SystemFileSystemMetadataProvider.Instance;
			var physical = await provider.GetMetadataAsync( junction, false );
			var followed = await provider.GetMetadataAsync( junction, true );

			Assert.Equal( FileSystemEntryKind.NameSurrogate, physical.Kind );
			Assert.False( physical.IsSymbolicLink );
			Assert.True( physical.IsPathIndirection );
			Assert.True( physical.IsReparsePoint );
			Assert.True( physical.IsJunction );
			Assert.False( physical.IsVolumeMountPoint );
			Assert.Equal( PathIndirectionKind.WindowsJunction, physical.Indirection.Kind );
			Assert.True( physical.ReparseTag.IsAvailable );
			Assert.Equal( WindowsReparseTags.MountPoint, physical.ReparseTag.GetRequiredValue() );
			Assert.Equal( FileSystemEntryKind.Directory, followed.Kind );
			Assert.True( followed.WasDereferenced );
		} finally {
			if ( Directory.Exists( junction ) ) {
				Directory.Delete( junction );
			}
			Directory.Delete( root, true );
		}
	}

	private static bool TryCreateJunction( string junctionPath, string targetPath ) {
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
			startInfo.ArgumentList.Add( "/J" );
			startInfo.ArgumentList.Add( junctionPath );
			startInfo.ArgumentList.Add( targetPath );
			using var process = Process.Start( startInfo );
			if ( process is null ) {
				return false;
			}
			process.WaitForExit();
			return process.ExitCode == 0 && Directory.Exists( junctionPath );
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

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "Icod.Metadata.Reparse.Tests.", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}
}
