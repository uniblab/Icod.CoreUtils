namespace Icod.CoreUtils.ChGrp.Tests;

using ChGrpCommand = Icod.CoreUtils.Chgrp.Command;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Modes;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using System.IO;
using Xunit;

/// <summary>Exercises GNU-compatible <c>chgrp</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies a group name is resolved and applied without changing the user.</summary>
	[Fact]
	public async Task ResolvesGroupName() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2, "owner", "oldgroup" );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "staff", "file" },
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.Requests );
		Assert.Null( request.UserId );
		Assert.Equal( (uint?)2001, request.GroupId );
	}

	/// <summary>Verifies a leading plus forces a numeric group ID.</summary>
	[Fact]
	public async Task LeadingPlusForcesNumericGroup() {
		var identity = TestIdentityProvider.CreateDefault().AddGroup( new GroupIdentity( "3000", "42" ) );
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "+42", "file" }, metadata, mutation, identity );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( (uint?)42, Assert.Single( mutation.Requests ).GroupId );
	}

	/// <summary>Verifies an all-digit group name is resolved before numeric interpretation.</summary>
	[Fact]
	public async Task NumericGroupNameTakesPrecedence() {
		var identity = TestIdentityProvider.CreateDefault().AddGroup( new GroupIdentity( "3000", "42" ) );
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "42", "file" }, metadata, mutation, identity );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( (uint?)3000, Assert.Single( mutation.Requests ).GroupId );
	}

	/// <summary>Verifies a reference file supplies only its group.</summary>
	[Fact]
	public async Task AppliesReferenceGroup() {
		var metadata = new TestMetadataProvider()
			.Add( "reference", 44, 55, "refuser", "refgroup" )
			.Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "--reference=reference", "file" },
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.Requests );
		Assert.Null( request.UserId );
		Assert.Equal( (uint?)55, request.GroupId );
		Assert.Equal( PathDereferenceMode.FollowEligiblePathIndirection, metadata.GetLastDereferenceMode( "reference" ) );
	}

	/// <summary>Verifies chgrp accepts a chown-style owner/group filter.</summary>
	[Fact]
	public async Task FromFilterCanMatchOwnerAndGroup() {
		var metadata = new TestMetadataProvider().Add( "file", 1001, 2001, "alice", "staff" );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "--from=alice:staff", "wheel", "file" },
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.Requests );
		Assert.Equal( (uint?)2002, request.GroupId );
		Assert.Equal( (uint?)1001, request.Precondition?.ExpectedUserId );
		Assert.Equal( (uint?)2001, request.Precondition?.ExpectedGroupId );
	}

	/// <summary>Verifies changes-only output is emitted only for actual mutations.</summary>
	[Fact]
	public async Task ChangesReportsOnlyChangedGroups() {
		var metadata = new TestMetadataProvider()
			.Add( "changed", 1, 2, "owner", "old" )
			.Add( "retained", 1, 2001, "owner", "staff" );
		var mutation = new RecordingMutationProvider();
		var output = new StringWriter();
		var status = await ChGrpCommand.RunAsync(
			new[] { "--changes", "staff", "changed", "retained" },
			CreateContext( output, new StringWriter() ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Single( mutation.Requests );
		Assert.Contains( "changed group", output.ToString() );
		Assert.DoesNotContain( "retained", output.ToString() );
	}

	/// <summary>Verifies unknown group names fail before mutation.</summary>
	[Fact]
	public async Task RejectsUnknownGroup() {
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChGrpCommand.RunAsync(
			new[] { "missing-group", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			new TestMetadataProvider(),
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.Requests );
		Assert.Contains( "invalid group", error.ToString() );
	}

	private static ValueTask<int> RunAsync(
		string[] args,
		TestMetadataProvider metadata,
		RecordingMutationProvider mutation,
		TestIdentityProvider identity
	) => ChGrpCommand.RunAsync(
		args,
		CreateContext( new StringWriter(), new StringWriter() ),
		new EmptyReadOnlyProvider(),
		metadata,
		mutation,
		identity
	);

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( "chgrp", TextReader.Null, output, error );
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-ChGrp-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private sealed class EmptyReadOnlyProvider : IReadOnlyFileSystemProvider {
		/// <inheritdoc/>
		public ValueTask<ReadOnlyFileSystemEntry> ObserveAsync( string path, bool followSymbolicLink, CancellationToken cancellationToken = default ) =>
			ValueTask.FromException<ReadOnlyFileSystemEntry>( new FileNotFoundException( path ) );

		/// <inheritdoc/>
		public async IAsyncEnumerable<ReadOnlyDirectoryEntry> EnumerateDirectoryAsync(
			string directoryPath,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			await Task.CompletedTask;
			yield break;
		}
	}

	private sealed class TestMetadataProvider : IFileSystemMetadataProvider {
		private readonly Dictionary<string, OwnershipData> _data = new( StringComparer.Ordinal );
		private readonly Dictionary<string, PathDereferenceMode> _dereference = new( StringComparer.Ordinal );
		private readonly IReadOnlyFileSystemProvider? _observationProvider;

		/// <summary>Initializes a deterministic metadata provider.</summary>
		public TestMetadataProvider( IReadOnlyFileSystemProvider? observationProvider = null ) {
			_observationProvider = observationProvider;
		}

		/// <summary>Gets or sets the fallback user ID.</summary>
		public uint DefaultUserId { get; init; } = 1;
		/// <summary>Gets or sets the fallback group ID.</summary>
		public uint DefaultGroupId { get; init; } = 2;

		/// <summary>Adds one ownership observation.</summary>
		public TestMetadataProvider Add( string path, uint userId, uint groupId, string? user = null, string? group = null ) {
			_data[path] = new OwnershipData( userId, groupId, user, group );
			return this;
		}

		/// <summary>Gets the last dereference policy used for one path.</summary>
		public PathDereferenceMode GetLastDereferenceMode( string path ) => _dereference[path];

		/// <inheritdoc/>
		public async ValueTask<FileSystemMetadata> GetMetadataAsync( string path, bool followSymbolicLink, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			var mode = followSymbolicLink ? PathDereferenceMode.FollowEligiblePathIndirection : PathDereferenceMode.NoFollow;
			_dereference[path] = mode;
			ReadOnlyFileSystemEntry? observation = null;
			if ( _observationProvider is not null ) {
				observation = await _observationProvider.ObserveAsync( path, followSymbolicLink, cancellationToken ).ConfigureAwait( false );
			}
			var data = _data.GetValueOrDefault( path, new OwnershipData( DefaultUserId, DefaultGroupId, null, null ) );
			return new FileSystemMetadata(
				path,
				observation?.Kind ?? FileSystemEntryKind.File,
				observation?.IsSymbolicLink ?? false,
				observation?.WasDereferenced ?? false,
				observation?.EntryIdentity ?? new FileSystemEntryIdentity( "test", path ),
				observation?.FileSystemIdentity ?? new FileSystemIdentity( "test", "filesystem" ),
				observation?.Indirection
			) {
				UserId = FileSystemMetadataValue<uint>.Available( data.UserId ),
				GroupId = FileSystemMetadataValue<uint>.Available( data.GroupId ),
				OwnerName = data.UserName is null ? default : FileSystemMetadataValue<string>.Available( data.UserName ),
				GroupName = data.GroupName is null ? default : FileSystemMetadataValue<string>.Available( data.GroupName )
			};
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemInformation> GetFileSystemInformationAsync( string path, CancellationToken cancellationToken = default ) =>
			ValueTask.FromException<FileSystemInformation>( new NotSupportedException() );

		/// <inheritdoc/>
		public ValueTask<PlatformOperationResult> SetTimestampsAsync( string path, FileTimestampMutationRequest request, bool followSymbolicLink, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult( PlatformOperationResult.Unsupported( "not used" ) );
	}

	private sealed class TestIdentityProvider : IIdentityProvider {
		private readonly Dictionary<string, UserIdentity> _users = new( StringComparer.Ordinal );
		private readonly Dictionary<string, GroupIdentity> _groups = new( StringComparer.Ordinal );

		/// <summary>Creates a provider with common test identities.</summary>
		public static TestIdentityProvider CreateDefault() {
			var provider = new TestIdentityProvider();
			provider.AddGroup( new GroupIdentity( "2001", "staff" ) );
			provider.AddGroup( new GroupIdentity( "2002", "wheel" ) );
			provider.AddUser( new UserIdentity(
				"1001",
				"alice",
				new GroupIdentity( "2001", "staff" ),
				new[] { new GroupIdentity( "2001", "staff" ) }
			) );
			provider.AddUser( new UserIdentity(
				"0",
				"root",
				new GroupIdentity( "2002", "wheel" ),
				new[] { new GroupIdentity( "2002", "wheel" ) }
			) );
			return provider;
		}

		/// <summary>Adds one user.</summary>
		public TestIdentityProvider AddUser( UserIdentity user ) {
			_users[user.Name] = user;
			_users[user.Id] = user;
			return this;
		}

		/// <summary>Adds one group.</summary>
		public TestIdentityProvider AddGroup( GroupIdentity group ) {
			_groups[group.Name] = group;
			_groups[group.Id] = group;
			return this;
		}

		/// <inheritdoc/>
		public ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default ) =>
			ValueTask.FromException<ProcessIdentity>( new NotSupportedException() );
		/// <inheritdoc/>
		public ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult<UserIdentity?>( _users.GetValueOrDefault( userName ) );
		/// <inheritdoc/>
		public ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult<UserIdentity?>( _users.GetValueOrDefault( userId ) );
		/// <inheritdoc/>
		public ValueTask<GroupIdentity?> FindGroupAsync( string groupName, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult<GroupIdentity?>( _groups.GetValueOrDefault( groupName ) );
		/// <inheritdoc/>
		public ValueTask<GroupIdentity?> FindGroupByIdAsync( string groupId, CancellationToken cancellationToken = default ) =>
			ValueTask.FromResult<GroupIdentity?>( _groups.GetValueOrDefault( groupId ) );
		/// <inheritdoc/>
		public ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default ) => ValueTask.FromResult<string?>( null );
	}

	private sealed class RecordingMutationProvider : IFileSystemMutationProvider {
		private static readonly FileSystemMutationCapabilities AllCapabilities = new(
			true, true, true, true, true, true, true, true, true, true, true, true, true
		);

		/// <inheritdoc/>
		public FileSystemMutationCapabilities Capabilities => AllCapabilities;
		/// <summary>Gets recorded ownership requests.</summary>
		public List<OwnershipRequest> Requests { get; } = new();
		/// <summary>Gets planned ownership results.</summary>
		public Queue<FileSystemMutationResult> Results { get; } = new();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetOwnershipAsync( string path, uint? userId, uint? groupId, PathDereferenceMode dereferenceMode, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			Requests.Add( new OwnershipRequest( path, userId, groupId, dereferenceMode, precondition ) );
			return ValueTask.FromResult( Results.Count == 0 ? Success( path, dereferenceMode ) : Results.Dequeue() );
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDirectoryAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFileAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateHardLinkAsync( string path, string existingPath, PathDereferenceMode existingPathDereferenceMode, FileSystemMutationPrecondition? destinationPrecondition = null, FileSystemMutationPrecondition? existingPathPrecondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateSymbolicLinkAsync( string path, string target, bool targetIsDirectory, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateJunctionAsync( string path, string target, FileSystemMutationPrecondition? destinationPrecondition = null, FileSystemMutationPrecondition? targetPrecondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFifoAsync( string path, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDeviceNodeAsync( string path, FileSystemEntryKind kind, DeviceNumber deviceNumber, PosixFileMode mode, FileCreationMask creationMask, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveFileAsync( string path, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveDirectoryAsync( string path, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );
		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetModeAsync( string path, PosixFileMode mode, PathDereferenceMode dereferenceMode, FileSystemMutationPrecondition? precondition = null, CancellationToken cancellationToken = default ) => Unused( path );

		private static ValueTask<FileSystemMutationResult> Unused( string path ) =>
			ValueTask.FromResult( FileSystemMutationResult.Unsupported( path, "not used" ) );
	}

	private static FileSystemMutationResult Success( string path, PathDereferenceMode mode ) =>
		FileSystemMutationResult.Success( new FileSystemMutationOutcome(
			path,
			FileSystemMutationOperation.SetOwnership,
			FileSystemEntryKind.File,
			FileSystemEntryIdentity.Unavailable,
			null,
			mode == PathDereferenceMode.FollowEligiblePathIndirection
		) );

	private readonly record struct OwnershipData( uint UserId, uint GroupId, string? UserName, string? GroupName );
	private readonly record struct OwnershipRequest( string Path, uint? UserId, uint? GroupId, PathDereferenceMode DereferenceMode, FileSystemMutationPrecondition? Precondition );
}
