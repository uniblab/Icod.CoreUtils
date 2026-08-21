namespace Icod.CoreUtils.Shared.Tests.FileSystem.Mutation;

using System.Globalization;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using Xunit;

/// <summary>Exercises host-backed E4 ownership mutation behavior.</summary>
public sealed class SystemOwnershipMutationTests {
	/// <summary>Verifies Unix hosts can assign a new file to the process's effective group.</summary>
	[Fact]
	public async Task ChangesGroupToCurrentEffectiveGroupOnUnix() {
		if ( !IsUnixLike ) return;
		var directory = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( directory, "file" );
		await File.WriteAllTextAsync( path, "content" );
		try {
			var identity = await SystemIdentityProvider.Instance.GetCurrentAsync();
			Assert.True( uint.TryParse(
				identity.EffectiveGroup.Id,
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var groupId
			) );
			var before = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				path,
				PathDereferenceMode.NoFollow
			);
			var precondition = FileSystemMutationPrecondition.FromObservation(
				before.Kind,
				before.EntryIdentity,
				PathDereferenceMode.NoFollow
			);
			var result = await SystemFileSystemMutationProvider.Instance.SetOwnershipAsync(
				path,
				null,
				groupId,
				PathDereferenceMode.NoFollow,
				precondition
			);
			Assert.True( result.Succeeded, result.Message );
			var after = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				path,
				PathDereferenceMode.NoFollow
			);
			Assert.True( after.GroupId.IsAvailable );
			Assert.Equal( groupId, after.GroupId.GetRequiredValue() );
		} finally {
			try { File.Delete( path ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			try { Directory.Delete( directory ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies ownership-aware preconditions reject a changed current owner before native mutation.</summary>
	[Fact]
	public async Task RejectsChangedOwnershipPreconditionOnUnix() {
		if ( !IsUnixLike ) return;
		var identity = new FileSystemEntryIdentity( "test", "entry" );
		var metadata = new FixedOwnershipMetadataProvider( identity, 2, 4 );
		var provider = new SystemFileSystemMutationProvider( metadata );
		var precondition = FileSystemMutationPrecondition.FromOwnershipObservation(
			FileSystemEntryKind.File,
			identity,
			PathDereferenceMode.NoFollow,
			1,
			null
		);
		var result = await provider.SetOwnershipAsync(
			"virtual-entry",
			null,
			3,
			PathDereferenceMode.NoFollow,
			precondition
		);
		Assert.False( result.Succeeded );
		Assert.Equal( FileSystemMutationErrorCode.IdentityChanged, result.ErrorCode );
	}

	/// <summary>Verifies native Windows advertises no POSIX ownership-mutation capability.</summary>
	[Fact]
	public void WindowsDoesNotAdvertisePosixOwnershipMutation() {
		if ( !OperatingSystem.IsWindows() ) return;
		Assert.False( SystemFileSystemMutationProvider.Instance.Capabilities.CanSetOwnership );
		Assert.False(
			SystemFileSystemMutationProvider.Instance.Capabilities.CanSetOwnershipWithoutFollowingPathIndirection
		);
	}

	private sealed class FixedOwnershipMetadataProvider : IFileSystemMetadataProvider {
		private readonly FileSystemEntryIdentity _identity;
		private readonly uint _userId;
		private readonly uint _groupId;

		/// <summary>Initializes one fixed ownership observation.</summary>
		public FixedOwnershipMetadataProvider(
			FileSystemEntryIdentity identity,
			uint userId,
			uint groupId
		) {
			_identity = identity;
			_userId = userId;
			_groupId = groupId;
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemMetadata> GetMetadataAsync(
			string path,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( new FileSystemMetadata(
				path,
				FileSystemEntryKind.File,
				false,
				false,
				_identity,
				new FileSystemIdentity( "test", "filesystem" ),
				default
			) {
				UserId = FileSystemMetadataValue<uint>.Available( _userId ),
				GroupId = FileSystemMetadataValue<uint>.Available( _groupId )
			} );
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemInformation> GetFileSystemInformationAsync(
			string path,
			CancellationToken cancellationToken = default
		) => ValueTask.FromException<FileSystemInformation>( new NotSupportedException() );

		/// <inheritdoc/>
		public ValueTask<PlatformOperationResult> SetTimestampsAsync(
			string path,
			FileTimestampMutationRequest request,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) => ValueTask.FromResult( PlatformOperationResult.Unsupported( "not used" ) );
	}

	private static bool IsUnixLike =>
		OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD();

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod-OwnershipMutation-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( path );
		return path;
	}
}
