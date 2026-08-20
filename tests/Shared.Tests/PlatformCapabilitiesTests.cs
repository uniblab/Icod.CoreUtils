namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.Platform;
using Xunit;

public sealed class PlatformCapabilitiesTests {

	[Fact]
	public void OwnershipAndSecurityContextsFailCleanly() {
		var ownership = PlatformCapabilities.TrySetOwnership(
			"path",
			"owner",
			"group"
		);
		var context = PlatformCapabilities.TrySetSecurityContext(
			"path",
			"context"
		);

		Assert.False( ownership.Supported );
		Assert.False( ownership.Succeeded );
		Assert.False( context.Supported );
		Assert.False( context.Succeeded );
		Assert.False( PlatformCapabilities.IsSupported( PlatformFeature.FileOwnership ) );
		Assert.False( PlatformCapabilities.IsSupported( PlatformFeature.SecurityContexts ) );
	}

	[Fact]
	public void UnixModeCapabilityMatchesOperatingSystem() {
		Assert.Equal(
			!OperatingSystem.IsWindows(),
			PlatformCapabilities.IsSupported(
				PlatformFeature.UnixFileModes
			)
		);
	}

	[Fact]
	public void HardLinkOperationReturnsControlledResult() {
		var directory = Directory.CreateTempSubdirectory(
			"icod-hardlink-"
		);
		try {
			var target = System.IO.Path.Combine(
				directory.FullName,
				"target.txt"
			);
			var link = System.IO.Path.Combine(
				directory.FullName,
				"link.txt"
			);
			File.WriteAllText(
				target,
				"payload"
			);

			var result = PlatformCapabilities.TryCreateHardLink(
				link,
				target
			);

			if ( result.Succeeded ) {
				Assert.True( result.Supported );
				Assert.Equal( "payload", File.ReadAllText( link ) );
			} else {
				Assert.NotNull( result.Message );
			}
		} finally {
			directory.Delete(
				recursive: true
			);
		}
	}

	[Fact]
	public void LinkTargetInspectionIsControlledForOrdinaryFiles() {
		var path = System.IO.Path.GetTempFileName();
		try {
			var result = PlatformCapabilities.TryGetLinkTarget(
				path,
				isDirectory: false
			);

			Assert.True( result.Supported );
			Assert.True( result.Succeeded );
			Assert.Null( result.Value );
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public void UnixModeReadIsSupportedOrFailsCleanly() {
		var path = System.IO.Path.GetTempFileName();
		try {
			var result = PlatformCapabilities.TryGetUnixFileMode(
				path
			);

			if ( OperatingSystem.IsWindows() ) {
				Assert.False( result.Supported );
			} else {
				Assert.True( result.Supported );
				if ( !result.Succeeded ) {
					Assert.NotNull( result.Message );
				}
			}
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public void UnixModeOperationIsSupportedOrFailsCleanly() {
		var path = System.IO.Path.GetTempFileName();
		try {
			var result = PlatformCapabilities.TrySetUnixFileMode(
				path,
				UnixFileMode.UserRead | UnixFileMode.UserWrite
			);

			if ( OperatingSystem.IsWindows() ) {
				Assert.False( result.Supported );
			} else {
				Assert.True( result.Supported );
				if ( !result.Succeeded ) {
					Assert.NotNull( result.Message );
				}
			}
		} finally {
			File.Delete( path );
		}
	}

}
