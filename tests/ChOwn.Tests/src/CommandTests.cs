namespace Icod.CoreUtils.ChOwn.Tests;

using ChOwnCommand = Icod.CoreUtils.Chown.Command;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;
using System.IO;
using Xunit;

/// <summary>Exercises GNU-compatible <c>chown</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies owner and group names are resolved and mutated together.</summary>
	[Fact]
	public async Task ResolvesOwnerAndGroupNames() {
		var identity = TestIdentityProvider.CreateDefault();
		var metadata = new TestMetadataProvider().Add( "file", 1, 2, "old", "oldgroup" );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "alice:staff", "file" }, metadata, mutation, identity );
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.Requests );
		Assert.Equal( (uint?)1001, request.UserId );
		Assert.Equal( (uint?)2001, request.GroupId );
		Assert.Equal( PathDereferenceMode.FollowEligiblePathIndirection, request.DereferenceMode );
		Assert.NotNull( request.Precondition?.ExpectedIdentity );
	}

	/// <summary>Verifies an empty group selects the owner's primary login group.</summary>
	[Fact]
	public async Task OwnerColonUsesPrimaryGroup() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "alice:", "file" },
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.Requests );
		Assert.Equal( (uint?)1001, request.UserId );
		Assert.Equal( (uint?)2001, request.GroupId );
	}

	/// <summary>Verifies a leading plus forces numeric interpretation instead of a numeric account name.</summary>
	[Fact]
	public async Task LeadingPlusForcesNumericIdentifier() {
		var identity = TestIdentityProvider.CreateDefault()
			.AddUser( new UserIdentity( "1000", "42", new GroupIdentity( "2000", "users" ), Array.Empty<GroupIdentity>() ) );
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "+42", "file" }, metadata, mutation, identity );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( (uint?)42, Assert.Single( mutation.Requests ).UserId );
	}

	/// <summary>Verifies an all-digit login name is resolved before numeric interpretation.</summary>
	[Fact]
	public async Task NumericLoginNameTakesPrecedence() {
		var identity = TestIdentityProvider.CreateDefault()
			.AddUser( new UserIdentity( "1000", "42", new GroupIdentity( "2000", "users" ), Array.Empty<GroupIdentity>() ) );
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "42", "file" }, metadata, mutation, identity );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( (uint?)1000, Assert.Single( mutation.Requests ).UserId );
	}

	/// <summary>Verifies the legacy unambiguous owner.group form remains compatible and warns.</summary>
	[Fact]
	public async Task LegacyDotSeparatorChangesBothAndWarns() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "alice.staff", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.Requests );
		Assert.Equal( (uint?)1001, request.UserId );
		Assert.Equal( (uint?)2001, request.GroupId );
		Assert.Contains( "legacy owner.group", error.ToString() );
	}

	/// <summary>Verifies a colon-only specification processes the operand without changing ownership.</summary>
	[Fact]
	public async Task ColonOnlyRetainsOwnership() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2, "one", "two" );
		var mutation = new RecordingMutationProvider();
		var output = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "--verbose", ":", "file" },
			CreateContext( output, new StringWriter() ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Empty( mutation.Requests );
		Assert.Contains( "retained", output.ToString() );
	}

	/// <summary>Verifies a reference file supplies both owner and group and is dereferenced.</summary>
	[Fact]
	public async Task AppliesReferenceOwnership() {
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
		Assert.Equal( (uint?)44, request.UserId );
		Assert.Equal( (uint?)55, request.GroupId );
		Assert.Equal( PathDereferenceMode.FollowEligiblePathIndirection, metadata.GetLastDereferenceMode( "reference" ) );
	}

	/// <summary>Verifies <c>--from</c> prevents a mutation when either requested current identity differs.</summary>
	[Fact]
	public async Task FromFilterSkipsNonmatchingEntry() {
		var metadata = new TestMetadataProvider().Add( "file", 7, 8, "seven", "eight" );
		var mutation = new RecordingMutationProvider();
		var output = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "--verbose", "--from=alice:staff", "root:wheel", "file" },
			CreateContext( output, new StringWriter() ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Empty( mutation.Requests );
		Assert.Contains( "retained", output.ToString() );
	}

	/// <summary>Verifies recursive traversal mutates the root and descendants with no-follow policy by default.</summary>
	[Fact]
	public async Task RecursivelyChangesTreeWithIdentityPreconditions() {
		var root = CreateTemporaryDirectory();
		var child = System.IO.Path.Combine( root, "child" );
		await File.WriteAllTextAsync( child, "content" );
		try {
			var metadata = new TestMetadataProvider( SystemReadOnlyFileSystemProvider.Instance ) {
				DefaultUserId = 1,
				DefaultGroupId = 2
			};
			var mutation = new RecordingMutationProvider();
			var status = await ChOwnCommand.RunAsync(
				new[] { "--recursive", "+7:+8", root },
				CreateContext( new StringWriter(), new StringWriter() ),
				SystemReadOnlyFileSystemProvider.Instance,
				metadata,
				mutation,
				TestIdentityProvider.CreateDefault()
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( 2, mutation.Requests.Count );
			Assert.Equal( child, mutation.Requests[0].Path );
			Assert.Equal( root, mutation.Requests[1].Path );
			Assert.All( mutation.Requests, request => {
				Assert.Equal( PathDereferenceMode.NoFollow, request.DereferenceMode );
				Assert.NotNull( request.Precondition?.ExpectedIdentity );
			} );
		} finally {
			try { File.Delete( child ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			try { Directory.Delete( root ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies <c>-H</c> retains referent mutation for descendants unless <c>-h</c> is supplied.</summary>
	[Fact]
	public async Task CommandLineTraversalDefaultsToDereferencingAllMutations() {
		var root = CreateTemporaryDirectory();
		var child = System.IO.Path.Combine( root, "child" );
		await File.WriteAllTextAsync( child, "content" );
		try {
			var metadata = new TestMetadataProvider( SystemReadOnlyFileSystemProvider.Instance ) {
				DefaultUserId = 1,
				DefaultGroupId = 2
			};
			var mutation = new RecordingMutationProvider();
			var status = await ChOwnCommand.RunAsync(
				new[] { "--recursive", "-H", "+7", root },
				CreateContext( new StringWriter(), new StringWriter() ),
				SystemReadOnlyFileSystemProvider.Instance,
				metadata,
				mutation,
				TestIdentityProvider.CreateDefault()
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( 2, mutation.Requests.Count );
			Assert.All( mutation.Requests, request =>
				Assert.Equal( PathDereferenceMode.FollowEligiblePathIndirection, request.DereferenceMode )
			);
		} finally {
			try { File.Delete( child ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			try { Directory.Delete( root ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies preserve-root fails in E5 preflight before ownership mutation.</summary>
	[Fact]
	public async Task PreserveRootRefusesFileSystemRoot() {
		var root = System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( "." ) )!;
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "--recursive", "--preserve-root", "+1", root },
			CreateContext( new StringWriter(), error ),
			SystemReadOnlyFileSystemProvider.Instance,
			new TestMetadataProvider(),
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.Requests );
		Assert.Contains( "filesystem root", error.ToString() );
	}

	/// <summary>Verifies recursive dereferencing is rejected with physical traversal.</summary>
	[Fact]
	public async Task RecursiveDereferenceRequiresHOrL() {
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "--recursive", "-P", "--dereference", "+1", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			new TestMetadataProvider(),
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.Requests );
		Assert.Contains( "requires either -H or -L", error.ToString() );
	}

	/// <summary>Verifies no-dereference selects link-object ownership mutation.</summary>
	[Fact]
	public async Task NoDereferenceUsesNoFollowMutation() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "--no-dereference", "+7", "file" },
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( PathDereferenceMode.NoFollow, Assert.Single( mutation.Requests ).DereferenceMode );
	}

	/// <summary>Verifies quiet mode suppresses controlled mutation diagnostics.</summary>
	[Fact]
	public async Task QuietSuppressesMutationFailure() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2 );
		var mutation = new RecordingMutationProvider();
		mutation.Results.Enqueue( FileSystemMutationResult.Unsupported( "file", "not supported" ) );
		var error = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "--quiet", "+7", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies verbose failure reporting remains active when quiet mode suppresses standard error.</summary>
	[Fact]
	public async Task VerboseReportsFailedMutationWhenQuiet() {
		var metadata = new TestMetadataProvider().Add( "file", 1, 2, "one", "two" );
		var mutation = new RecordingMutationProvider();
		mutation.Results.Enqueue( FileSystemMutationResult.Unsupported( "file", "not supported" ) );
		var output = new StringWriter();
		var error = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "--quiet", "--verbose", "+7", "file" },
			CreateContext( output, error ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Equal( string.Empty, error.ToString() );
		Assert.Contains( "failed to change ownership", output.ToString() );
	}

	/// <summary>Verifies native Windows reports unsupported POSIX ownership instead of emulating ACL changes.</summary>
	[Fact]
	public async Task WindowsDoesNotEmulatePosixOwnership() {
		if ( !OperatingSystem.IsWindows() ) return;
		var directory = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( directory, "file" );
		await File.WriteAllTextAsync( path, "content" );
		try {
			var error = new StringWriter();
			var status = await ChOwnCommand.RunAsync(
				new[] { "+0", path },
				CreateContext( new StringWriter(), error )
			);
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.Contains( "POSIX ownership", error.ToString() );
		} finally {
			File.Delete( path );
			Directory.Delete( directory );
		}
	}

	/// <summary>Verifies invalid user names fail before filesystem mutation.</summary>
	[Fact]
	public async Task RejectsUnknownUser() {
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChOwnCommand.RunAsync(
			new[] { "missing-user", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			new TestMetadataProvider(),
			mutation,
			TestIdentityProvider.CreateDefault()
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.Requests );
		Assert.Contains( "invalid user", error.ToString() );
	}

	private static ValueTask<int> RunAsync(
		string[] args,
		TestMetadataProvider metadata,
		RecordingMutationProvider mutation,
		TestIdentityProvider identity
	) => ChOwnCommand.RunAsync(
		args,
		CreateContext( new StringWriter(), new StringWriter() ),
		new EmptyReadOnlyProvider(),
		metadata,
		mutation,
		identity
	);

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( "chown", TextReader.Null, output, error );
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-ChOwn-", Guid.NewGuid().ToString( "N" ) ) );
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
