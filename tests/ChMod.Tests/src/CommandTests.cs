namespace Icod.CoreUtils.ChMod.Tests;

using ChModCommand = Icod.CoreUtils.Chmod.Command;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using FileCreationMask = Icod.CommandFramework.FileSystem.Modes.FileCreationMask;
using PosixFileMode = Icod.CommandFramework.FileSystem.Modes.PosixFileMode;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using System.IO;
using Xunit;

/// <summary>Exercises GNU-compatible <c>chmod</c> behavior.</summary>
public sealed class CommandTests {
	private const string ProgramName = "chmod";

	/// <summary>Verifies that numeric modes are interpreted as octal values.</summary>
	[Fact]
	public async Task AppliesNumericModeAsOctal() {
		var metadata = new TestMetadataProvider().Add( "file", 0x01a4 );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "755", "file" }, metadata, mutation );
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( mutation.ModeRequests );
		Assert.Equal( Convert.ToInt32( "755", 8 ), request.Mode.Value );
		Assert.Equal( PathDereferenceMode.FollowEligiblePathIndirection, request.DereferenceMode );
	}

	/// <summary>Verifies symbolic modes beginning with a dash are not mistaken for command options.</summary>
	[Fact]
	public async Task AcceptsDashPrefixedSymbolicModeAndAppliesUmask() {
		var metadata = new TestMetadataProvider().Add( "file", Convert.ToInt32( "666", 8 ) );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "-w", "file" },
			metadata,
			mutation,
			new FileCreationMask( Convert.ToInt32( "022", 8 ) )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( Convert.ToInt32( "466", 8 ), Assert.Single( mutation.ModeRequests ).Mode.Value );
	}

	/// <summary>Verifies that reference mode is observed once and applied to every target.</summary>
	[Fact]
	public async Task AppliesReferenceMode() {
		var metadata = new TestMetadataProvider()
			.Add( "reference", Convert.ToInt32( "2750", 8 ) )
			.Add( "first", Convert.ToInt32( "644", 8 ) )
			.Add( "second", Convert.ToInt32( "600", 8 ) );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync(
			new[] { "--reference=reference", "first", "second" },
			metadata,
			mutation
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( 2, mutation.ModeRequests.Count );
		Assert.All( mutation.ModeRequests, request => Assert.Equal( Convert.ToInt32( "2750", 8 ), request.Mode.Value ) );
		Assert.Equal( 1, metadata.GetObservationCount( "reference" ) );
	}

	/// <summary>Verifies verbose and changes-only reporting.</summary>
	[Fact]
	public async Task ReportsChangedAndRetainedModes() {
		var metadata = new TestMetadataProvider()
			.Add( "changed", Convert.ToInt32( "600", 8 ) )
			.Add( "retained", Convert.ToInt32( "644", 8 ) );
		var mutation = new RecordingMutationProvider();
		var output = new StringWriter();
		var status = await ChModCommand.RunAsync(
			new[] { "--verbose", "644", "changed", "retained" },
			CreateContext( output, new StringWriter() ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Contains( "changed from 0600 to 0644", output.ToString() );
		Assert.Contains( "retained as 0644", output.ToString() );
	}

	/// <summary>Verifies that the last reporting option controls retained-mode output.</summary>
	[Fact]
	public async Task LastReportingOptionWins() {
		var metadata = new TestMetadataProvider().Add( "file", Convert.ToInt32( "644", 8 ) );
		var mutation = new RecordingMutationProvider();
		var output = new StringWriter();
		var status = await ChModCommand.RunAsync(
			new[] { "--verbose", "--changes", "644", "file" },
			CreateContext( output, new StringWriter() ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( string.Empty, output.ToString() );
	}

	/// <summary>Verifies recursive traversal mutates a root and its descendants with identity-bearing preconditions.</summary>
	[Fact]
	public async Task RecursivelyChangesDirectoryTree() {
		var root = CreateTemporaryDirectory();
		var child = System.IO.Path.Combine( root, "child" );
		await File.WriteAllTextAsync( child, "content" );
		try {
			var metadata = new TestMetadataProvider( SystemReadOnlyFileSystemProvider.Instance ) {
				DefaultMode = Convert.ToInt32( "644", 8 )
			};
			var mutation = new RecordingMutationProvider();
			var status = await ChModCommand.RunAsync(
				new[] { "--recursive", "700", root },
				CreateContext( new StringWriter(), new StringWriter() ),
				SystemReadOnlyFileSystemProvider.Instance,
				metadata,
				mutation,
				new FixedMaskProvider( FileCreationMask.None )
			);
			Assert.Equal( CommandExitCodes.Success, status );
			Assert.Equal( 2, mutation.ModeRequests.Count );
			Assert.Equal( child, mutation.ModeRequests[0].Path );
			Assert.Equal( root, mutation.ModeRequests[1].Path );
			Assert.All( mutation.ModeRequests, request => {
				Assert.Equal( Convert.ToInt32( "700", 8 ), request.Mode.Value );
				Assert.Equal( FileSystemMutationExistence.MustExist, request.Precondition?.Existence );
				Assert.NotNull( request.Precondition?.ExpectedIdentity );
			} );
		} finally {
			try { File.Delete( child ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			try { Directory.Delete( root ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies preserve-root fails in E5 preflight before mutation.</summary>
	[Fact]
	public async Task PreserveRootRefusesFileSystemRoot() {
		var root = System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( "." ) )!;
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChModCommand.RunAsync(
			new[] { "--recursive", "--preserve-root", "700", root },
			CreateContext( new StringWriter(), error ),
			SystemReadOnlyFileSystemProvider.Instance,
			new TestMetadataProvider(),
			mutation,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.ModeRequests );
		Assert.Contains( "filesystem root", error.ToString() );
	}

	/// <summary>Verifies GNU rejects recursive dereferencing with physical traversal.</summary>
	[Fact]
	public async Task RecursiveDereferenceRequiresHOrL() {
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChModCommand.RunAsync(
			new[] { "--recursive", "-P", "--dereference", "700", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			new TestMetadataProvider(),
			mutation,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( mutation.ModeRequests );
		Assert.Contains( "requires either -H or -L", error.ToString() );
	}

	/// <summary>Verifies that no-dereference controls mutation independently of traversal.</summary>
	[Fact]
	public async Task NoDereferenceUsesNoFollowMutation() {
		var metadata = new TestMetadataProvider().Add( "file", Convert.ToInt32( "644", 8 ) );
		var mutation = new RecordingMutationProvider();
		var status = await RunAsync( new[] { "--no-dereference", "600", "file" }, metadata, mutation );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( PathDereferenceMode.NoFollow, Assert.Single( mutation.ModeRequests ).DereferenceMode );
	}

	/// <summary>Verifies quiet mode suppresses mutation diagnostics while preserving failure status.</summary>
	[Fact]
	public async Task QuietSuppressesMutationFailure() {
		var metadata = new TestMetadataProvider().Add( "file", Convert.ToInt32( "644", 8 ) );
		var mutation = new RecordingMutationProvider();
		mutation.ModeResults.Enqueue( FileSystemMutationResult.Unsupported( "file", "not supported" ) );
		var error = new StringWriter();
		var status = await ChModCommand.RunAsync(
			new[] { "--quiet", "600", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies native Windows does not emulate POSIX modes with the read-only attribute.</summary>
	[Fact]
	public async Task WindowsDoesNotMapModeToReadOnlyAttribute() {
		if ( !OperatingSystem.IsWindows() ) return;
		var directory = CreateTemporaryDirectory();
		var path = System.IO.Path.Combine( directory, "file" );
		await File.WriteAllTextAsync( path, "content" );
		try {
			File.SetAttributes( path, FileAttributes.Normal );
			var error = new StringWriter();
			var status = await ChModCommand.RunAsync(
				new[] { "444", path },
				CreateContext( new StringWriter(), error )
			);
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.Equal( (FileAttributes)0, File.GetAttributes( path ) & FileAttributes.ReadOnly );
			Assert.Contains( "POSIX mode", error.ToString() );
		} finally {
			File.SetAttributes( path, FileAttributes.Normal );
			File.Delete( path );
			Directory.Delete( directory );
		}
	}

	/// <summary>Verifies invalid modes are rejected before metadata observation.</summary>
	[Fact]
	public async Task RejectsInvalidMode() {
		var metadata = new TestMetadataProvider();
		var mutation = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await ChModCommand.RunAsync(
			new[] { "888", "file" },
			CreateContext( new StringWriter(), error ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "invalid mode", error.ToString() );
		Assert.Empty( mutation.ModeRequests );
	}

	private static ValueTask<int> RunAsync(
		string[] args,
		TestMetadataProvider metadata,
		RecordingMutationProvider mutation,
		FileCreationMask? creationMask = null
	) {
		return ChModCommand.RunAsync(
			args,
			CreateContext( new StringWriter(), new StringWriter() ),
			new EmptyReadOnlyProvider(),
			metadata,
			mutation,
			new FixedMaskProvider( creationMask ?? FileCreationMask.None )
		);
	}

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( ProgramName, TextReader.Null, output, error );
	}

	private static string CreateTemporaryDirectory() {
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-ChMod-", Guid.NewGuid().ToString( "N" ) ) );
		Directory.CreateDirectory( path );
		return path;
	}

	private static FileSystemMutationResult Success( string path, PosixFileMode mode, PathDereferenceMode dereferenceMode ) {
		return FileSystemMutationResult.Success(
			new FileSystemMutationOutcome(
				path,
				FileSystemMutationOperation.SetMode,
				FileSystemEntryKind.File,
				FileSystemEntryIdentity.Unavailable,
				modeApplied: true,
				wasDereferenced: dereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection
			)
		);
	}

	private sealed class FixedMaskProvider : IFileCreationMaskProvider {
		private readonly FileCreationMask _mask;

		/// <summary>Initializes a fixed creation-mask provider.</summary>
		public FixedMaskProvider( FileCreationMask mask ) {
			_mask = mask;
		}

		/// <inheritdoc/>
		public FileCreationMask GetCurrentMask() => _mask;
	}

	private sealed class EmptyReadOnlyProvider : IReadOnlyFileSystemProvider {
		/// <summary>Initializes an empty provider.</summary>
		public EmptyReadOnlyProvider() { }

		/// <inheritdoc/>
		public ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
			string path,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) => ValueTask.FromException<ReadOnlyFileSystemEntry>( new FileNotFoundException( path ) );

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
		private readonly Dictionary<string, int> _modes = new( StringComparer.Ordinal );
		private readonly Dictionary<string, int> _observations = new( StringComparer.Ordinal );
		private readonly IReadOnlyFileSystemProvider? _observationProvider;

		/// <summary>Initializes a deterministic metadata provider.</summary>
		public TestMetadataProvider( IReadOnlyFileSystemProvider? observationProvider = null ) {
			_observationProvider = observationProvider;
		}

		/// <summary>Gets or sets the fallback mode for paths not explicitly configured.</summary>
		public int DefaultMode { get; init; } = Convert.ToInt32( "644", 8 );

		/// <summary>Adds one path mode and returns this provider.</summary>
		public TestMetadataProvider Add( string path, int mode ) {
			_modes[path] = mode;
			return this;
		}

		/// <summary>Gets the number of observations for one path.</summary>
		public int GetObservationCount( string path ) => _observations.GetValueOrDefault( path );

		/// <inheritdoc/>
		public async ValueTask<FileSystemMetadata> GetMetadataAsync(
			string path,
			bool followSymbolicLink,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			_observations[path] = GetObservationCount( path ) + 1;
			ReadOnlyFileSystemEntry? observation = null;
			if ( _observationProvider is not null ) {
				observation = await _observationProvider.ObserveAsync(
					path,
					followSymbolicLink,
					cancellationToken
				).ConfigureAwait( false );
			}
			var mode = _modes.GetValueOrDefault( path, DefaultMode );
			return new FileSystemMetadata(
				path,
				observation?.Kind ?? FileSystemEntryKind.File,
				observation?.IsSymbolicLink ?? false,
				observation?.WasDereferenced ?? false,
				observation?.EntryIdentity ?? new FileSystemEntryIdentity( "test", path ),
				observation?.FileSystemIdentity ?? new FileSystemIdentity( "test", "filesystem" ),
				observation?.Indirection
			) {
				Mode = FileSystemMetadataValue<uint>.Available( checked( (uint)mode ) )
			};
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

	private sealed class RecordingMutationProvider : IFileSystemMutationProvider {
		private static readonly FileSystemMutationCapabilities AllCapabilities = new(
			canCreateDirectories: true,
			canCreateFiles: true,
			canCreateHardLinks: true,
			canCreateSymbolicLinks: true,
			canCreateFifos: true,
			canCreateDeviceNodes: true,
			canRemoveFiles: true,
			canRemoveDirectories: true,
			canSetModes: true,
			canSetModeWithoutFollowingPathIndirection: true,
			canCreateJunctions: true
		);

		/// <summary>Initializes an empty recording provider.</summary>
		public RecordingMutationProvider() { }

		/// <inheritdoc/>
		public FileSystemMutationCapabilities Capabilities => AllCapabilities;

		/// <summary>Gets recorded mode requests.</summary>
		public List<ModeRequest> ModeRequests { get; } = new();

		/// <summary>Gets planned mode results.</summary>
		public Queue<FileSystemMutationResult> ModeResults { get; } = new();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetModeAsync(
			string path,
			PosixFileMode mode,
			PathDereferenceMode dereferenceMode,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			ModeRequests.Add( new ModeRequest( path, mode, dereferenceMode, precondition ) );
			return ValueTask.FromResult(
				ModeResults.Count == 0 ? Success( path, mode, dereferenceMode ) : ModeResults.Dequeue()
			);
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

		private static ValueTask<FileSystemMutationResult> Unused( string path ) {
			return ValueTask.FromResult( FileSystemMutationResult.Unsupported( path, "not used" ) );
		}
	}

	private readonly record struct ModeRequest(
		string Path,
		PosixFileMode Mode,
		PathDereferenceMode DereferenceMode,
		FileSystemMutationPrecondition? Precondition
	);
}
