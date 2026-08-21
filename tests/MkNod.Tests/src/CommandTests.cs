namespace Icod.CoreUtils.MkNod.Tests;

using NodeCommand = Icod.CoreUtils.MkNod.Command;
using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using FileCreationMask = Icod.CommandFramework.FileSystem.Modes.FileCreationMask;
using IFileCreationMaskProvider = Icod.CommandFramework.FileSystem.Modes.IFileCreationMaskProvider;
using PosixFileMode = Icod.CommandFramework.FileSystem.Modes.PosixFileMode;
using Icod.CommandFramework.FileSystem.Mutation;
using Icod.CommandFramework.FileSystem.Traversal;
using Xunit;

/// <summary>Exercises GNU-compatible <c>mknod</c> behavior.</summary>
public sealed class CommandTests {
	private const string ProgramName = "mknod";

	/// <summary>Verifies that type <c>p</c> delegates to FIFO creation without device numbers.</summary>
	[Fact]
	public async Task CreatesFifoForPType() {
		var provider = new RecordingMutationProvider();
		var mask = new FileCreationMask( Convert.ToInt32( "022", 8 ) );
		var status = await NodeCommand.RunAsync(
			new[] { "pipe", "p" },
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( mask )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( provider.FifoRequests );
		Assert.Equal( "pipe", request.Path );
		Assert.Equal( Convert.ToInt32( "666", 8 ), request.Mode.Value );
		Assert.Equal( mask, request.CreationMask );
		Assert.Empty( provider.DeviceRequests );
	}

	/// <summary>Verifies block and character type aliases and their device numbers.</summary>
	[Theory]
	[InlineData( "b", FileSystemEntryKind.BlockDevice )]
	[InlineData( "c", FileSystemEntryKind.CharacterDevice )]
	[InlineData( "u", FileSystemEntryKind.CharacterDevice )]
	public async Task MapsDeviceTypes( string type, FileSystemEntryKind expectedKind ) {
		var provider = new RecordingMutationProvider();
		var status = await NodeCommand.RunAsync(
			new[] { "node", type, "12", "34" },
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( provider.DeviceRequests );
		Assert.Equal( expectedKind, request.Kind );
		Assert.Equal( 12u, request.DeviceNumber.Major );
		Assert.Equal( 34u, request.DeviceNumber.Minor );
		Assert.Empty( provider.FifoRequests );
	}

	/// <summary>Verifies GNU hexadecimal and octal device-number prefixes.</summary>
	[Fact]
	public async Task ParsesHexadecimalAndOctalDeviceNumbers() {
		var provider = new RecordingMutationProvider();
		var status = await NodeCommand.RunAsync(
			new[] { "node", "b", "0x1f", "077" },
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( provider.DeviceRequests );
		Assert.Equal( 31u, request.DeviceNumber.Major );
		Assert.Equal( 63u, request.DeviceNumber.Minor );
	}

	/// <summary>Verifies rejection of malformed and overflowing device numbers.</summary>
	[Theory]
	[InlineData( "08" )]
	[InlineData( "0x" )]
	[InlineData( "4294967296" )]
	public async Task RejectsInvalidDeviceNumbers( string major ) {
		var provider = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await NodeCommand.RunAsync(
			new[] { "node", "c", major, "1" },
			CreateContext( new StringWriter(), error ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "invalid major device number", error.ToString() );
		Assert.Empty( provider.DeviceRequests );
	}

	/// <summary>Gets malformed operand lists for exact-count validation.</summary>
	public static TheoryData<string[]> WrongOperandCases { get; } = new() {
		Array.Empty<string>(),
		new[] { "node" },
		new[] { "node", "b" },
		new[] { "node", "b", "1" },
		new[] { "node", "p", "1" },
		new[] { "node", "b", "1", "2", "3" }
	};

	/// <summary>Verifies exact operand requirements for FIFO and device forms.</summary>
	[Theory]
	[MemberData( nameof( WrongOperandCases ) )]
	public async Task RejectsWrongOperandCounts( string[] args ) {
		var provider = new RecordingMutationProvider();
		var status = await NodeCommand.RunAsync(
			args,
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Empty( provider.FifoRequests );
		Assert.Empty( provider.DeviceRequests );
	}

	/// <summary>Verifies rejection of unsupported type designators.</summary>
	[Fact]
	public async Task RejectsInvalidDeviceType() {
		var provider = new RecordingMutationProvider();
		var error = new StringWriter();
		var status = await NodeCommand.RunAsync(
			new[] { "node", "x", "1", "2" },
			CreateContext( new StringWriter(), error ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "invalid device type 'x'", error.ToString() );
		Assert.Empty( provider.DeviceRequests );
	}

	/// <summary>Verifies explicit mode handling and special-bit rejection.</summary>
	[Fact]
	public async Task AppliesPermissionModeAndRejectsSpecialBits() {
		var provider = new RecordingMutationProvider();
		var status = await NodeCommand.RunAsync(
			new[] { "--mode=640", "node", "c", "1", "2" },
			CreateContext( new StringWriter(), new StringWriter() ),
			provider,
			new FixedMaskProvider( new FileCreationMask( Convert.ToInt32( "077", 8 ) ) )
		);
		Assert.Equal( CommandExitCodes.Success, status );
		var request = Assert.Single( provider.DeviceRequests );
		Assert.Equal( Convert.ToInt32( "640", 8 ), request.Mode.Value );
		Assert.Equal( FileCreationMask.None, request.CreationMask );

		var secondProvider = new RecordingMutationProvider();
		var error = new StringWriter();
		status = await NodeCommand.RunAsync(
			new[] { "--mode=1777", "pipe", "p" },
			CreateContext( new StringWriter(), error ),
			secondProvider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "mode must specify only file permission bits", error.ToString() );
		Assert.Empty( secondProvider.FifoRequests );
	}

	/// <summary>Verifies that provider privilege failures become controlled command failures.</summary>
	[Fact]
	public async Task ReportsPrivilegeFailure() {
		var provider = new RecordingMutationProvider();
		provider.DeviceResults.Enqueue(
			FileSystemMutationResult.Failure(
				"node",
				FileSystemMutationErrorCode.PrivilegeRequired,
				"privilege required"
			)
		);
		var error = new StringWriter();
		var status = await NodeCommand.RunAsync(
			new[] { "node", "c", "1", "2" },
			CreateContext( new StringWriter(), error ),
			provider,
			new FixedMaskProvider( FileCreationMask.None )
		);
		Assert.Equal( CommandExitCodes.Failure, status );
		Assert.Contains( "Operation not permitted", error.ToString() );
	}

	/// <summary>Verifies controlled unsupported-platform behavior without ordinary-file emulation.</summary>
	[Fact]
	public async Task SystemProviderNeverEmulatesSpecialFileWithOrdinaryFile() {
		if ( !OperatingSystem.IsWindows() ) return;
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			string.Concat( "Icod-MkNod-", Guid.NewGuid().ToString( "N" ) )
		);
		try {
			var status = await NodeCommand.RunAsync(
				new[] { path, "p" },
				CreateContext( new StringWriter(), new StringWriter() )
			);
			Assert.Equal( CommandExitCodes.Failure, status );
			Assert.False( File.Exists( path ) );
			Assert.False( Directory.Exists( path ) );
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
