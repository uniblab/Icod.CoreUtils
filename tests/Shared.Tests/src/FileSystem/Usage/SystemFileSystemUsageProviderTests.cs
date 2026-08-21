namespace Icod.CoreUtils.Shared.Tests.FileSystem.Usage;

using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using Icod.CoreUtils.Shared.FileSystem.Usage;
using Xunit;

/// <summary>Verifies the Coreutils filesystem-usage adapter over framework metadata.</summary>
public sealed class SystemFileSystemUsageProviderTests {
	/// <summary>Verifies inode-pool observations are forwarded from framework filesystem information.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReusesFrameworkInodePoolObservation() {
		var path = System.IO.Path.GetTempPath();
		var root = System.IO.Path.GetPathRoot(
			System.IO.Path.GetFullPath( path )
		);
		Assert.False(
			string.IsNullOrWhiteSpace( root )
		);
		var information = new FileSystemInformation(
			path,
			new FileSystemIdentity( "test", "filesystem" )
		) {
			MountPoint = FileSystemMetadataValue<string>.Available( root! ),
			VolumeName = FileSystemMetadataValue<string>.Available( "test-volume" ),
			TotalInodes = FileSystemMetadataValue<ulong>.Available( 1200 ),
			FreeInodes = FileSystemMetadataValue<ulong>.Available( 700 ),
			AvailableInodes = FileSystemMetadataValue<ulong>.Available( 650 )
		};
		var provider = new SystemFileSystemUsageProvider(
			new FixedMetadataProvider( information )
		);

		var snapshots = await provider.GetFileSystemsAsync(
			[ path ],
			includeUnavailable: false
		);
		var snapshot = Assert.Single(
			snapshots
		);

		Assert.Equal(
			information.TotalInodes,
			snapshot.TotalInodes
		);
		Assert.Equal(
			information.FreeInodes,
			snapshot.FreeInodes
		);
		Assert.Equal(
			information.AvailableInodes,
			snapshot.AvailableInodes
		);
	}

	private sealed class FixedMetadataProvider(
		FileSystemInformation information
	) : IFileSystemMetadataProvider {
		/// <inheritdoc />
		public ValueTask<FileSystemInformation> GetFileSystemInformationAsync(
			string path,
			CancellationToken cancellationToken = default
		) {
			ArgumentException.ThrowIfNullOrWhiteSpace(
				path
			);
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				information
			);
		}

		/// <inheritdoc />
		public ValueTask<FileSystemMetadata> GetMetadataAsync(
			string path,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) {
			ArgumentException.ThrowIfNullOrWhiteSpace(
				path
			);
			cancellationToken.ThrowIfCancellationRequested();
			throw new NotSupportedException(
				"Entry metadata is not used by this test provider."
			);
		}

		/// <inheritdoc />
		public ValueTask<PlatformOperationResult> SetTimestampsAsync(
			string path,
			FileTimestampMutationRequest request,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) {
			ArgumentException.ThrowIfNullOrWhiteSpace(
				path
			);
			ArgumentNullException.ThrowIfNull(
				request
			);
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					"Timestamp mutation is not used by this test provider."
				)
			);
		}
	}
}
