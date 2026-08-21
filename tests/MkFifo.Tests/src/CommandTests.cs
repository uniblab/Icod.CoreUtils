namespace Icod.CoreUtils.MkFifo.Tests;

using FifoCommand = Icod.CoreUtils.MkFifo.Command;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using FileCreationMask = Icod.CommandFramework.FileSystem.Modes.FileCreationMask;
using PosixFileMode = Icod.CommandFramework.FileSystem.Modes.PosixFileMode;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.Traversal;
using Xunit;

/// <summary>Exercises GNU-compatible <c>mkfifo</c> behavior.</summary>
public sealed class CommandTests {
	private const string ProgramName = "mkfifo";

	/// <summary>Verifies the GNU missing-operand diagnostic.</summary>
	[Fact]
	public async Task ReportsMissingOperand() {
		var error = new StringWriter();
		var status = await FifoCommand.RunAsync(
			Array.Empty<string>(),
			CreateContext( new StringWriter(), error ),
			new RecordingMutationProvider(),
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "mkfifo: missing operand", error.ToString() );
	}

	/// <summary>Verifies that every operand is created with the default mode and supplied umask.</summary>
	[Fact]
	public async Task PassesDefaultModeAndMaskToEveryOperand() {
		var provider = new RecordingMutationProvider();
		var mask = new FileCreationMask( Convert.ToInt32( "022", 8 ) );
		var status = await FifoCommand.RunAsync(
			new[] { "first", "second" },
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( mask )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( 2, provider.FifoRequests.Count );
		Assert.All( provider.FifoRequests, request => {
			Assert.Equal( Convert.ToInt32( "666", 8 ), request.Mode.Value );
			Assert.Equal( mask, request.CreationMask );
			Assert.Equal( FileSystemMutationExistence.MustNotExist, request.Precondition?.Existence );
		} );
		Assert.Empty( provider.DeviceRequests );
	}

	/// <summary>Verifies that an explicit mode is resolved before creation and bypasses the process umask.</summary>
	[Fact]
	public async Task ExplicitModeBypassesCreationMask() {
		var provider = new RecordingMutationProvider();
		var status = await FifoCommand.RunAsync(
			new[] { "--mode=600", "private" },
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( new FileCreationMask( Convert.ToInt32( "077", 8 ) ) )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( provider.FifoRequests );
		Assert.Equal( Convert.ToInt32( "600", 8 ), request.Mode.Value );
		Assert.Equal( FileCreationMask.None, request.CreationMask );
	}

	/// <summary>Verifies that special mode bits are rejected before filesystem mutation.</summary>
	[Fact]
	public async Task RejectsSpecialModeBits() {
		var provider = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await FifoCommand.RunAsync(
			new[] { "--mode=u+s", "fifo" },
			CreateContext( new StringWriter(), error ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "mode must specify only file permission bits", error.ToString() );
		Assert.Empty( provider.FifoRequests );
	}

	/// <summary>Verifies continuation after one per-operand creation failure.</summary>
	[Fact]
	public async Task ContinuesAfterPerOperandFailure() {
		var provider = new RecordingMutationProvider();
		provider.FifoResults.Enqueue(
			FileSystemMutationResult.Failure(
				"first",
				FileSystemMutationErrorCode.AlreadyExists,
				"destination exists"
			)
		);
		var error = new StringWriter();
		var status = await FifoCommand.RunAsync(
			new[] { "first", "second" },
			CreateContext( new StringWriter(), error ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Equal( 2, provider.FifoRequests.Count );
		Assert.Contains( "File exists", error.ToString() );
	}

	/// <summary>Verifies that the system provider creates an actual FIFO on capable Unix hosts.</summary>
	[Fact]
	public async Task CreatesActualFifoOnCapableUnixHost() {
		if ( OperatingSystem.IsWindows() || !SystemFileSystemMutationProvider.Instance.Capabilities.CanCreateFifos ) return;
		var directory = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod-MkFifo-", Guid.NewGuid().ToString( "N" ) )
		);
		Directory.CreateDirectory( directory );
		var path = System.IO.Path.Combine( directory, "pipe" );
		try {
			var status = await FifoCommand.RunAsync(
				new[] { path },
				CreateContext( new StringWriter(), new StringWriter() ),
				SystemFileSystemMutationProvider.Instance,
				new FixedMaskProvider( FileCreationMask.None )
			);
			Assert.Equal( CommandExitCodes.Success, status );
			var metadata = await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
				path,
				PathDereferenceMode.NoFollow
			);
			Assert.Equal( FileSystemEntryKind.Fifo, metadata.Kind );
		} finally {
			try { File.Delete( path ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
			try { Directory.Delete( directory ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <summary>Verifies controlled unsupported-platform behavior without ordinary-file emulation.</summary>
	[Fact]
	public async Task SystemProviderNeverEmulatesFifoWithOrdinaryFile() {
		if ( !OperatingSystem.IsWindows() ) return;
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod-MkFifo-", Guid.NewGuid().ToString( "N" ) )
		);
		try {
			var error = new StringWriter();
			var status = await FifoCommand.RunAsync(
				new[] { path },
				CreateContext( new StringWriter(), error )
			);
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.False( File.Exists( path ) );
			Assert.False( Directory.Exists( path ) );
			Assert.NotEqual( string.Empty, error.ToString() );
		} finally {
			if ( File.Exists( path ) ) File.Delete( path );
		}
	}

	private static CommandContext CreateContext( TextWriter output, TextWriter error ) {
		return new CommandContext( ProgramName, TextReader.Null, output, error );
	}

	private static FileSystemMutationResult Success(
		string path,
		FileSystemMutationOperation operation,
		FileSystemEntryKind kind
	) {
		return FileSystemMutationResult.Success(
			new FileSystemMutationOutcome(
				path,
				operation,
				kind,
				FileSystemEntryIdentity.Unavailable,
				modeApplied: true,
				wasDereferenced: false
			)
		);
	}

	private sealed class FixedMaskProvider : IFileCreationMaskProvider {
		private readonly FileCreationMask mask;

		/// <summary>Initializes a fixed creation-mask provider.</summary>
		public FixedMaskProvider( FileCreationMask mask ) {
			this.mask = mask;
		}

		/// <inheritdoc/>
		public FileCreationMask GetCurrentMask() => mask;
	}

	private sealed class RecordingMutationProvider : IFileSystemMutationProvider {
		/// <summary>Initializes an empty recording mutation provider.</summary>
		public RecordingMutationProvider() { }

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

		/// <inheritdoc/>
		public FileSystemMutationCapabilities Capabilities => AllCapabilities;
		/// <summary>Gets the recorded FIFO requests.</summary>
		public List<FifoRequest> FifoRequests { get; } = new();
		/// <summary>Gets the recorded device-node requests.</summary>
		public List<DeviceRequest> DeviceRequests { get; } = new();
		/// <summary>Gets planned FIFO results.</summary>
		public Queue<FileSystemMutationResult> FifoResults { get; } = new();
		/// <summary>Gets planned device-node results.</summary>
		public Queue<FileSystemMutationResult> DeviceResults { get; } = new();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFifoAsync(
			string path,
			PosixFileMode mode,
			FileCreationMask creationMask,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			FifoRequests.Add( new FifoRequest( path, mode, creationMask, precondition ) );
			return ValueTask.FromResult(
				FifoResults.Count == 0
					? Success( path, FileSystemMutationOperation.CreateFifo, FileSystemEntryKind.Fifo )
					: FifoResults.Dequeue()
			);
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDeviceNodeAsync(
			string path,
			FileSystemEntryKind kind,
			DeviceNumber deviceNumber,
			PosixFileMode mode,
			FileCreationMask creationMask,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			DeviceRequests.Add( new DeviceRequest( path, kind, deviceNumber, mode, creationMask, precondition ) );
			return ValueTask.FromResult(
				DeviceResults.Count == 0
					? Success( path, FileSystemMutationOperation.CreateDeviceNode, kind )
					: DeviceResults.Dequeue()
			);
		}

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateDirectoryAsync(
			string path,
			PosixFileMode mode,
			FileCreationMask creationMask,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateFileAsync(
			string path,
			PosixFileMode mode,
			FileCreationMask creationMask,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateHardLinkAsync(
			string path,
			string existingPath,
			PathDereferenceMode existingPathDereferenceMode,
			FileSystemMutationPrecondition? destinationPrecondition = null,
			FileSystemMutationPrecondition? existingPathPrecondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateSymbolicLinkAsync(
			string path,
			string target,
			bool targetIsDirectory,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> CreateJunctionAsync(
			string path,
			string target,
			FileSystemMutationPrecondition? destinationPrecondition = null,
			FileSystemMutationPrecondition? targetPrecondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveFileAsync(
			string path,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> RemoveDirectoryAsync(
			string path,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		/// <inheritdoc/>
		public ValueTask<FileSystemMutationResult> SetModeAsync(
			string path,
			PosixFileMode mode,
			PathDereferenceMode dereferenceMode,
			FileSystemMutationPrecondition? precondition = null,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();
	}

	private sealed class FifoRequest {
		/// <summary>Initializes a recorded FIFO request.</summary>
		public FifoRequest(
			string path,
			PosixFileMode mode,
			FileCreationMask creationMask,
			FileSystemMutationPrecondition? precondition
		) {
			Path = path;
			Mode = mode;
			CreationMask = creationMask;
			Precondition = precondition;
		}

		/// <summary>Gets the requested path.</summary>
		public string Path { get; }
		/// <summary>Gets the requested mode.</summary>
		public PosixFileMode Mode { get; }
		/// <summary>Gets the requested creation mask.</summary>
		public FileCreationMask CreationMask { get; }
		/// <summary>Gets the requested precondition.</summary>
		public FileSystemMutationPrecondition? Precondition { get; }
	}

	private sealed class DeviceRequest {
		/// <summary>Initializes a recorded device-node request.</summary>
		public DeviceRequest(
			string path,
			FileSystemEntryKind kind,
			DeviceNumber deviceNumber,
			PosixFileMode mode,
			FileCreationMask creationMask,
			FileSystemMutationPrecondition? precondition
		) {
			Path = path;
			Kind = kind;
			DeviceNumber = deviceNumber;
			Mode = mode;
			CreationMask = creationMask;
			Precondition = precondition;
		}

		/// <summary>Gets the requested path.</summary>
		public string Path { get; }
		/// <summary>Gets the requested entry kind.</summary>
		public FileSystemEntryKind Kind { get; }
		/// <summary>Gets the requested device number.</summary>
		public DeviceNumber DeviceNumber { get; }
		/// <summary>Gets the requested mode.</summary>
		public PosixFileMode Mode { get; }
		/// <summary>Gets the requested creation mask.</summary>
		public FileCreationMask CreationMask { get; }
		/// <summary>Gets the requested precondition.</summary>
		public FileSystemMutationPrecondition? Precondition { get; }
	}
}
