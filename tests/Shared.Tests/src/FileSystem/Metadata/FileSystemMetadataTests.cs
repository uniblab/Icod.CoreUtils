using System.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Metadata;

/// <summary>
/// Exercises the Completion Gate E3 metadata contract and system adapter.
/// </summary>
public sealed class FileSystemMetadataTests {
	/// <summary>
	/// Verifies that unavailable, unsupported, not-applicable, and available values remain distinct.
	/// </summary>
	[Fact]
	public void AvailabilityStatesRemainExplicit() {
		var available = FileSystemMetadataValue<int>.Available( 0 );
		var unavailable = FileSystemMetadataValue<int>.Unavailable( "missing" );
		var unsupported = FileSystemMetadataValue<int>.Unsupported( "unsupported" );
		var notApplicable = FileSystemMetadataValue<int>.NotApplicable();

		Assert.True( available.IsAvailable );
		Assert.Equal( 0, available.GetRequiredValue() );
		Assert.Equal( FileSystemMetadataAvailability.Unavailable, unavailable.Availability );
		Assert.Equal( FileSystemMetadataAvailability.Unsupported, unsupported.Availability );
		Assert.Equal( FileSystemMetadataAvailability.NotApplicable, notApplicable.Availability );
		Assert.Throws<InvalidOperationException>( () => unavailable.GetRequiredValue() );
	}

	/// <summary>
	/// Verifies the timestamp request model preserves current, explicit, and unchanged decisions.
	/// </summary>
	[Fact]
	public void TimestampRequestRetainsIndependentChanges() {
		var instant = new DateTimeOffset( 2041, 2, 3, 4, 5, 6, TimeSpan.Zero );
		var request = new FileTimestampMutationRequest {
			AccessTime = FileTimestampChange.CurrentTime,
			ModificationTime = FileTimestampChange.At( instant )
		};

		Assert.True( request.HasChanges );
		Assert.Equal( FileTimestampChangeKind.CurrentTime, request.AccessTime.Kind );
		Assert.Equal( FileTimestampChangeKind.Explicit, request.ModificationTime.Kind );
		Assert.Equal( instant, request.ModificationTime.Value );
		Assert.Equal( FileTimestampChangeKind.Unchanged, request.BirthTime.Kind );
	}

	/// <summary>
	/// Verifies authoritative host metadata for an ordinary file and reuse of the E1 identities.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ObservesFileAndReusesTraversalIdentities() {
		var directory = CreateTemporaryDirectory();
		try {
			var path = Path.Combine( directory, "sample.txt" );
			await File.WriteAllTextAsync( path, "data" );
			var metadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync( path, false );
			var observation = await SystemReadOnlyFileSystemProvider.Instance.ObserveAsync( path, false );

			Assert.Equal( FileSystemEntryKind.File, metadata.Kind );
			Assert.Equal<ulong>( 4, metadata.Size.GetRequiredValue() );
			Assert.True( metadata.LinkCount.IsAvailable );
			Assert.True( metadata.AccessTime.IsAvailable );
			Assert.True( metadata.ModificationTime.IsAvailable );
			Assert.True( metadata.ChangeTime.IsAvailable );
			Assert.True( metadata.DeviceIdentifier.IsAvailable );
			Assert.True( metadata.InodeNumber.IsAvailable );
			Assert.True( metadata.AllocatedBlocks.IsAvailable );
			Assert.True( metadata.AllocationBlockSize.IsAvailable );
			Assert.True( metadata.AllocatedBytes.IsAvailable );
			Assert.True( metadata.TimestampMutationCapabilities.IsAvailable );
			if ( OperatingSystem.IsWindows() ) {
				Assert.Equal( FileSystemMetadataAvailability.Unsupported, metadata.Mode.Availability );
				Assert.True( metadata.OwnerName.IsAvailable );
				Assert.True( metadata.GroupName.IsAvailable );
				Assert.True( metadata.BirthTime.IsAvailable );
			} else if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ) {
				Assert.True( metadata.Mode.IsAvailable );
				Assert.True( metadata.UserId.IsAvailable );
				Assert.True( metadata.GroupId.IsAvailable );
			}
			Assert.Equal( observation.EntryIdentity, metadata.EntryIdentity );
			Assert.Equal( observation.FileSystemIdentity, metadata.FileSystemIdentity );
			Assert.Equal( FileSystemMetadataAvailability.NotApplicable, metadata.LinkIdentity.Availability );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>
	/// Verifies containing-filesystem capacity and allocation information.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ReportsContainingFileSystemInformation() {
		var directory = CreateTemporaryDirectory();
		try {
			var information = await SystemFileSystemMetadataProvider.Instance.GetFileSystemInformationAsync( directory );

			Assert.True( information.Identity.IsAvailable );
			Assert.True( information.MountPoint.IsAvailable );
			Assert.True( information.TotalBytes.IsAvailable );
			Assert.True( information.FreeBytes.IsAvailable );
			Assert.True( information.AvailableBytes.IsAvailable );
			Assert.True( information.BlockSize.IsAvailable );
			Assert.True( information.FragmentSize.IsAvailable );
			Assert.True( information.MaximumNameLength.IsAvailable );
			Assert.True( information.IsReadOnly.IsAvailable );
			Assert.True( information.TotalBytes.GetRequiredValue() >= information.FreeBytes.GetRequiredValue() );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>
	/// Verifies selective access and modification timestamp mutation, including post-2038 values.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task MutatesSelectedPost2038Timestamps() {
		var directory = CreateTemporaryDirectory();
		try {
			var path = Path.Combine( directory, "future.txt" );
			await File.WriteAllTextAsync( path, "future" );
			var access = new DateTimeOffset( 2040, 1, 2, 3, 4, 5, TimeSpan.Zero );
			var modification = new DateTimeOffset( 2041, 6, 7, 8, 9, 10, TimeSpan.Zero );
			var result = await SystemFileSystemMetadataProvider.Instance.SetTimestampsAsync(
				path,
				new FileTimestampMutationRequest {
					AccessTime = FileTimestampChange.At( access ),
					ModificationTime = FileTimestampChange.At( modification )
				},
				true
			);

			Assert.True( result.Supported, result.Message );
			Assert.True( result.Succeeded, result.Message );
			var metadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync( path, true );
			AssertTimestampNear( access, metadata.AccessTime.GetRequiredValue() );
			AssertTimestampNear( modification, metadata.ModificationTime.GetRequiredValue() );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>
	/// Verifies that an unsupported birth-time request is rejected before another timestamp changes.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task RejectsUnsupportedMixedTimestampRequestBeforeMutation() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}
		var directory = CreateTemporaryDirectory();
		try {
			var path = Path.Combine( directory, "preflight.txt" );
			await File.WriteAllTextAsync( path, "preflight" );
			var original = new DateTimeOffset( 2020, 2, 3, 4, 5, 6, TimeSpan.Zero );
			File.SetLastWriteTimeUtc( path, original.UtcDateTime );
			var result = await SystemFileSystemMetadataProvider.Instance.SetTimestampsAsync(
				path,
				new FileTimestampMutationRequest {
					ModificationTime = FileTimestampChange.At(
						new DateTimeOffset( 2040, 2, 3, 4, 5, 6, TimeSpan.Zero )
					),
					BirthTime = FileTimestampChange.At(
						new DateTimeOffset( 2041, 2, 3, 4, 5, 6, TimeSpan.Zero )
					)
				},
				true
			);

			Assert.False( result.Supported );
			Assert.False( result.Succeeded );
			AssertTimestampNear( original, new DateTimeOffset( File.GetLastWriteTimeUtc( path ), TimeSpan.Zero ) );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>
	/// Verifies detailed FIFO classification when the host utility can create one.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ClassifiesFifoWhenSupported() {
		if ( !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() ) {
			return;
		}
		var executable = File.Exists( "/usr/bin/mkfifo" )
			? "/usr/bin/mkfifo"
			: File.Exists( "/bin/mkfifo" )
				? "/bin/mkfifo"
				: null;
		if ( executable is null ) {
			return;
		}
		var directory = CreateTemporaryDirectory();
		try {
			var path = Path.Combine( directory, "pipe" );
			var startInfo = new ProcessStartInfo( executable ) {
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			};
			startInfo.ArgumentList.Add( path );
			using var process = Process.Start( startInfo );
			if ( process is null ) {
				return;
			}
			await process.WaitForExitAsync();
			if ( process.ExitCode != 0 ) {
				return;
			}

			var metadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync( path, false );
			Assert.Equal( FileSystemEntryKind.Fifo, metadata.Kind );
			Assert.True( metadata.Mode.IsAvailable );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	/// <summary>
	/// Verifies native character-device classification on Unix hosts.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task ClassifiesCharacterDeviceWhenAvailable() {
		if ( (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) || !File.Exists( "/dev/null" ) ) {
			return;
		}

		var metadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync( "/dev/null", false );

		Assert.Equal( FileSystemEntryKind.CharacterDevice, metadata.Kind );
		Assert.True( metadata.SpecialDeviceIdentifier.IsAvailable );
	}

	/// <summary>
	/// Verifies separate link-object and followed-target identities when symbolic links are supported.
	/// </summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task DistinguishesLinkIdentityFromFollowedTargetWhenSupported() {
		var directory = CreateTemporaryDirectory();
		try {
			var target = Path.Combine( directory, "target.txt" );
			var link = Path.Combine( directory, "link.txt" );
			await File.WriteAllTextAsync( target, "target" );
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

			var provider = SystemFileSystemMetadataProvider.Instance;
			var physical = await provider.GetMetadataAsync( link, false );
			var followed = await provider.GetMetadataAsync( link, true );
			var targetMetadata = await provider.GetMetadataAsync( target, false );

			Assert.Equal( FileSystemEntryKind.SymbolicLink, physical.Kind );
			Assert.True( physical.LinkIdentity.IsAvailable );
			Assert.Equal( physical.EntryIdentity, physical.LinkIdentity.GetRequiredValue() );
			Assert.True( followed.WasDereferenced );
			Assert.Equal( physical.EntryIdentity, followed.LinkIdentity.GetRequiredValue() );
			Assert.Equal( targetMetadata.EntryIdentity, followed.EntryIdentity );
		} finally {
			Directory.Delete( directory, true );
		}
	}

	private static void AssertTimestampNear( DateTimeOffset expected, DateTimeOffset actual ) {
		Assert.InRange( Math.Abs( (actual - expected).TotalSeconds ), 0, 3 );
	}

	private static string CreateTemporaryDirectory() {
		var path = Path.Combine(
			Path.GetTempPath(),
			string.Concat( "icod-e3-metadata-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}
}
