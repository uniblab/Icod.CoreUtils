using Path = global::System.IO.Path;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Modes;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Microsoft.Win32.SafeHandles;

namespace Icod.CoreUtils.Shared.FileSystem.Mutation;

/// <summary>
/// Implements E4 single-path mutation through the BCL and narrow host-native adapters.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Interoperability",
	"CA1416:Validate platform compatibility",
	Justification = "Unix-only calls are guarded by the provider capability boundary."
)]
public sealed class SystemFileSystemMutationProvider : IFileSystemMutationProvider {
	private const uint NativeCharacterDeviceType = 0x2000;
	private const uint NativeBlockDeviceType = 0x6000;
	private const uint PrivateFileCreationMode = 0x0180;
	private const uint PrivateDirectoryCreationMode = 0x01c0;
	private const int LinuxAtCurrentWorkingDirectory = -100;
	private const int DarwinAtCurrentWorkingDirectory = -2;
	private const int FreeBsdAtCurrentWorkingDirectory = -100;
	private const int LinuxAtSymbolicLinkFollow = 0x0400;
	private const int DarwinAtSymbolicLinkFollow = 0x0040;
	private const int FreeBsdAtSymbolicLinkFollow = 0x0400;
	private const uint WindowsGenericWrite = 0x40000000;
	private const uint WindowsFileShareRead = 0x00000001;
	private const uint WindowsFileShareWrite = 0x00000002;
	private const uint WindowsFileShareDelete = 0x00000004;
	private const uint WindowsOpenExisting = 3;
	private const uint WindowsFileFlagOpenReparsePoint = 0x00200000;
	private const uint WindowsFileFlagBackupSemantics = 0x02000000;
	private const uint WindowsFsctlSetReparsePoint = 0x000900a4;
	private const uint WindowsMountPointReparseTag = 0xa0000003;
	private const int WindowsMaximumReparseDataBufferSize = 16 * 1024;
	private readonly IFileSystemMetadataProvider metadataProvider;

	/// <summary>Gets the shared system provider.</summary>
	public static SystemFileSystemMutationProvider Instance { get; } = new(
		SystemFileSystemMetadataProvider.Instance
	);

	/// <summary>
	/// Initializes a system provider over an injectable E3 metadata provider.
	/// </summary>
	/// <param name="metadataProvider">The authoritative metadata and identity provider.</param>
	public SystemFileSystemMutationProvider( IFileSystemMetadataProvider metadataProvider ) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		Capabilities = GetCapabilities();
	}

	/// <inheritdoc/>
	public FileSystemMutationCapabilities Capabilities { get; }

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateDirectoryAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanCreateDirectories ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Directory creation is not implemented for this platform."
			);
		}
		var condition = precondition ?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		if ( validation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var effectiveMode = creationMask.Apply( mode );
		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( IsUnixLike ) {
				if ( NativeCreateDirectory( normalized, PrivateDirectoryCreationMode ) != 0 ) {
					return FromNativeFailure( normalized, "create directory" );
				}
				created = true;
				File.SetUnixFileMode( normalized, effectiveMode.ToUnixFileMode() );
			} else if ( OperatingSystem.IsWindows() ) {
				if ( !NativeCreateDirectoryWindows( normalized, IntPtr.Zero ) ) {
					return FromWindowsFailure( normalized, "create directory" );
				}
				created = true;
			} else {
				return FileSystemMutationResult.Unsupported(
					normalized,
					"Directory creation is not implemented for this platform."
				);
			}
			return await ObserveOutcomeAsync(
				normalized,
				FileSystemMutationOperation.CreateDirectory,
				IsUnixLike,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedDirectory( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedDirectory( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateFileAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanCreateFiles ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Ordinary-file creation is not implemented for this platform."
			);
		}
		var condition = precondition ?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		if ( validation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var effectiveMode = creationMask.Apply( mode );
		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var options = new FileStreamOptions {
				Mode = FileMode.CreateNew,
				Access = FileAccess.ReadWrite,
				Share = FileShare.None,
				Options = FileOptions.Asynchronous
			};
			if ( IsUnixLike ) {
				options.UnixCreateMode = (UnixFileMode)PrivateFileCreationMode;
			}
			await using ( var stream = new FileStream( normalized, options ) ) {
				created = true;
				if ( IsUnixLike ) {
					File.SetUnixFileMode( stream.SafeFileHandle, effectiveMode.ToUnixFileMode() );
				}
				await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
			}
			return await ObserveOutcomeAsync(
				normalized,
				FileSystemMutationOperation.CreateFile,
				IsUnixLike,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateHardLinkAsync(
		string path,
		string existingPath,
		PathDereferenceMode existingPathDereferenceMode,
		FileSystemMutationPrecondition? destinationPrecondition = null,
		FileSystemMutationPrecondition? existingPathPrecondition = null,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), existingPathDereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( existingPathDereferenceMode ) );
		}
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		var normalizedExisting = TryNormalizePath( existingPath, out var existingPathError );
		if ( normalizedExisting is null ) {
			return existingPathError!;
		}
		if ( !Capabilities.CanCreateHardLinks ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Hard-link creation is not implemented for this platform."
			);
		}

		var destinationCondition = destinationPrecondition
			?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var destinationValidation = await ValidateAsync(
			normalized,
			destinationCondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( destinationValidation.Error is not null ) {
			return destinationValidation.Error;
		}
		if ( destinationValidation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var sourceCondition = existingPathPrecondition ?? new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			existingPathDereferenceMode
		);
		if ( sourceCondition.DereferenceMode != existingPathDereferenceMode ) {
			return FileSystemMutationResult.Failure(
				normalizedExisting,
				FileSystemMutationErrorCode.UnsafePathIndirection,
				"The source precondition and hard-link dereference policies differ."
			);
		}
		var sourceValidation = await ValidateAsync(
			normalizedExisting,
			sourceCondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( sourceValidation.Error is not null ) {
			return sourceValidation.Error;
		}
		var sourceMetadata = sourceValidation.Effective;
		if ( sourceMetadata is null ) {
			return NotFound( normalizedExisting );
		}
		if ( sourceMetadata.Kind == FileSystemEntryKind.Directory ) {
			return WrongKind( normalizedExisting, "A hard-link source must not be a directory." );
		}

		var sourceRevalidation = await RevalidateAsync(
			normalizedExisting,
			sourceValidation,
			existingPathDereferenceMode,
			cancellationToken
		).ConfigureAwait( false );
		if ( sourceRevalidation is not null ) {
			return sourceRevalidation;
		}

		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( OperatingSystem.IsLinux() ) {
				var flags = existingPathDereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection
					? LinuxAtSymbolicLinkFollow
					: 0;
				if (
					NativeCreateHardLinkLinux(
						LinuxAtCurrentWorkingDirectory,
						normalizedExisting,
						LinuxAtCurrentWorkingDirectory,
						normalized,
						flags
					) != 0
				) {
					return FromNativeFailure( normalized, "create hard link" );
				}
				created = true;
			} else if ( OperatingSystem.IsMacOS() ) {
				var flags = existingPathDereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection
					? DarwinAtSymbolicLinkFollow
					: 0;
				if (
					NativeCreateHardLinkMacOS(
						DarwinAtCurrentWorkingDirectory,
						normalizedExisting,
						DarwinAtCurrentWorkingDirectory,
						normalized,
						flags
					) != 0
				) {
					return FromNativeFailure( normalized, "create hard link" );
				}
				created = true;
			} else if ( OperatingSystem.IsFreeBSD() ) {
				var flags = existingPathDereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection
					? FreeBsdAtSymbolicLinkFollow
					: 0;
				if (
					NativeCreateHardLinkFreeBsd(
						FreeBsdAtCurrentWorkingDirectory,
						normalizedExisting,
						FreeBsdAtCurrentWorkingDirectory,
						normalized,
						flags
					) != 0
				) {
					return FromNativeFailure( normalized, "create hard link" );
				}
				created = true;
			} else if ( OperatingSystem.IsWindows() ) {
				var sourcePath = normalizedExisting;
				if ( sourceValidation.Physical?.IsPathIndirection == true ) {
					if ( existingPathDereferenceMode == PathDereferenceMode.NoFollow ) {
						return FileSystemMutationResult.Unsupported(
							normalized,
							"Windows does not expose a safe hard-link-to-link-object primitive."
						);
					}
					var resolved = File.ResolveLinkTarget( normalizedExisting, true );
					if ( resolved is null ) {
						return NotFound( normalizedExisting );
					}
					sourcePath = resolved.FullName;
				}
				if ( !NativeCreateHardLinkWindows( normalized, sourcePath, IntPtr.Zero ) ) {
					return FromWindowsFailure( normalized, "create hard link" );
				}
				created = true;
			} else {
				return FileSystemMutationResult.Unsupported(
					normalized,
					"Hard-link creation is not implemented for this platform."
				);
			}
			var linked = await TryObserveAsync(
				normalized,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( linked is null ) {
				TryDeleteCreatedFile( normalized );
				return IdentityChanged( normalized );
			}
			if ( IdentitiesConflict( sourceMetadata.EntryIdentity, linked.EntryIdentity ) ) {
				TryDeleteCreatedFile( normalized );
				return IdentityChanged( normalizedExisting );
			}
			return FileSystemMutationResult.Success(
				new FileSystemMutationOutcome(
					normalized,
					FileSystemMutationOperation.CreateHardLink,
					linked.Kind,
					linked.EntryIdentity,
					null,
					false
				)
			);
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateSymbolicLinkAsync(
		string path,
		string target,
		bool targetIsDirectory,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( target );
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanCreateSymbolicLinks ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Symbolic-link creation is not implemented for this platform."
			);
		}
		var condition = precondition ?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		if ( validation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( targetIsDirectory ) {
				Directory.CreateSymbolicLink( normalized, target );
			} else {
				File.CreateSymbolicLink( normalized, target );
			}
			created = true;
			return await ObserveOutcomeAsync(
				normalized,
				FileSystemMutationOperation.CreateSymbolicLink,
				null,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateJunctionAsync(
		string path,
		string target,
		FileSystemMutationPrecondition? destinationPrecondition = null,
		FileSystemMutationPrecondition? targetPrecondition = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( target );
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanCreateJunctions ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Directory-junction creation is supported only on Windows."
			);
		}
		var normalizedTarget = TryNormalizePath( target, out var targetPathError );
		if ( normalizedTarget is null ) {
			return targetPathError!;
		}
		if ( IsRemoteWindowsPath( normalizedTarget ) ) {
			return FileSystemMutationResult.Failure(
				normalizedTarget,
				FileSystemMutationErrorCode.InvalidPath,
				"A Windows directory junction must target a local pathname."
			);
		}
		if ( IsExactWindowsVolumeGuidPath( normalizedTarget ) ) {
			return FileSystemMutationResult.Failure(
				normalizedTarget,
				FileSystemMutationErrorCode.InvalidPath,
				"An exact volume GUID target represents a mounted volume rather than a directory junction."
			);
		}

		var destinationCondition = destinationPrecondition
			?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var destinationValidation = await ValidateAsync(
			normalized,
			destinationCondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( destinationValidation.Error is not null ) {
			return destinationValidation.Error;
		}
		if ( destinationValidation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var targetCondition = targetPrecondition ?? new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			PathDereferenceMode.FollowEligiblePathIndirection,
			FileSystemEntryKind.Directory
		);
		if ( targetCondition.DereferenceMode != PathDereferenceMode.FollowEligiblePathIndirection ) {
			return FileSystemMutationResult.Failure(
				normalizedTarget,
				FileSystemMutationErrorCode.UnsafePathIndirection,
				"A junction target precondition must use the follow-eligible-path-indirection policy."
			);
		}
		var targetValidation = await ValidateAsync(
			normalizedTarget,
			targetCondition,
			cancellationToken
		).ConfigureAwait( false );
		if ( targetValidation.Error is not null ) {
			return targetValidation.Error;
		}
		if ( targetValidation.Effective is null ) {
			return NotFound( normalizedTarget );
		}
		if ( targetValidation.Effective.Kind != FileSystemEntryKind.Directory ) {
			return WrongKind( normalizedTarget, "A directory junction must target a directory." );
		}

		var targetRevalidation = await RevalidateAsync(
			normalizedTarget,
			targetValidation,
			PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		).ConfigureAwait( false );
		if ( targetRevalidation is not null ) {
			return targetRevalidation;
		}

		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( !NativeCreateDirectoryWindows( normalized, IntPtr.Zero ) ) {
				return FromWindowsFailure( normalized, "create junction directory" );
			}
			created = true;

			using var handle = NativeOpenReparsePointWindows(
				normalized,
				WindowsGenericWrite,
				WindowsFileShareRead | WindowsFileShareWrite | WindowsFileShareDelete,
				IntPtr.Zero,
				WindowsOpenExisting,
				WindowsFileFlagOpenReparsePoint | WindowsFileFlagBackupSemantics,
				IntPtr.Zero
			);
			if ( handle.IsInvalid ) {
				var failure = FromWindowsFailure( normalized, "open junction directory" );
				handle.Dispose();
				TryDeleteCreatedDirectory( normalized );
				created = false;
				return failure;
			}

			var reparseData = CreateJunctionReparseData( normalizedTarget );
			if (
				!NativeSetReparsePointWindows(
					handle,
					WindowsFsctlSetReparsePoint,
					reparseData,
					checked( (uint)reparseData.Length ),
					IntPtr.Zero,
					0,
					out _,
					IntPtr.Zero
				)
			) {
				var failure = FromWindowsFailure( normalized, "set junction reparse point" );
				handle.Dispose();
				TryDeleteCreatedDirectory( normalized );
				created = false;
				return failure;
			}
			handle.Dispose();

			var junction = await TryObserveAsync(
				normalized,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( junction is null || !junction.IsJunction ) {
				TryDeleteCreatedDirectory( normalized );
				created = false;
				return FileSystemMutationResult.Failure(
					normalized,
					FileSystemMutationErrorCode.IoFailure,
					"The created reparse point was not recognized as a Windows directory junction."
				);
			}

			return FileSystemMutationResult.Success(
				new FileSystemMutationOutcome(
					normalized,
					FileSystemMutationOperation.CreateJunction,
					junction.Kind,
					junction.EntryIdentity,
					null,
					false
				)
			);
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedDirectory( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedDirectory( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateFifoAsync(
		string path,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanCreateFifos ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"FIFO creation is not supported on this platform."
			);
		}
		var condition = precondition ?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		if ( validation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var effectiveMode = creationMask.Apply( mode );
		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( NativeCreateFifo( normalized, PrivateFileCreationMode ) != 0 ) {
				return FromNativeFailure( normalized, "create FIFO" );
			}
			created = true;
			File.SetUnixFileMode( normalized, effectiveMode.ToUnixFileMode() );
			return await ObserveOutcomeAsync(
				normalized,
				FileSystemMutationOperation.CreateFifo,
				true,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> CreateDeviceNodeAsync(
		string path,
		FileSystemEntryKind kind,
		DeviceNumber deviceNumber,
		PosixFileMode mode,
		FileCreationMask creationMask,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		if ( kind is not (FileSystemEntryKind.BlockDevice or FileSystemEntryKind.CharacterDevice) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanCreateDeviceNodes ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Device-node creation is not supported on this platform."
			);
		}
		if ( !TryEncodeDeviceNumber( deviceNumber, out var encodedDevice ) ) {
			return FileSystemMutationResult.Failure(
				normalized,
				FileSystemMutationErrorCode.InvalidDeviceNumber,
				"The major or minor number cannot be represented by this platform."
			);
		}
		var condition = precondition ?? FileSystemMutationPrecondition.DestinationMustNotExist();
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		if ( validation.Effective is not null ) {
			return AlreadyExists( normalized );
		}

		var effectiveMode = creationMask.Apply( mode );
		var nativeType = kind == FileSystemEntryKind.BlockDevice
			? NativeBlockDeviceType
			: NativeCharacterDeviceType;
		var created = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			if (
				NativeCreateDeviceNode(
					normalized,
					nativeType | PrivateFileCreationMode,
					encodedDevice
				) != 0
			) {
				return FromNativeFailure( normalized, "create device node" );
			}
			created = true;
			File.SetUnixFileMode( normalized, effectiveMode.ToUnixFileMode() );
			return await ObserveOutcomeAsync(
				normalized,
				FileSystemMutationOperation.CreateDeviceNode,
				true,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( OperationCanceledException exception ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			if ( created ) {
				TryDeleteCreatedFile( normalized );
			}
			return FromManagedFailure( normalized, exception, creation: true );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> RemoveFileAsync(
		string path,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanRemoveFiles ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Name removal is not implemented for this platform."
			);
		}
		var condition = precondition ?? new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			PathDereferenceMode.NoFollow
		);
		if ( condition.DereferenceMode != PathDereferenceMode.NoFollow ) {
			return FileSystemMutationResult.Failure(
				normalized,
				FileSystemMutationErrorCode.UnsafePathIndirection,
				"Name removal operates on the physical terminal object and cannot follow it."
			);
		}
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		var physical = validation.Physical;
		if ( physical is null ) {
			return NotFound( normalized );
		}
		if (
			physical.Kind == FileSystemEntryKind.Directory
				&& !physical.IsPathIndirection
				&& !physical.IsReparsePoint
		) {
			return WrongKind( normalized, "The pathname names a physical directory." );
		}
		var revalidation = await RevalidateAsync(
			normalized,
			validation,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		if ( revalidation is not null ) {
			return revalidation;
		}

		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( IsUnixLike ) {
				if ( NativeRemoveFile( normalized ) != 0 ) {
					return FromNativeFailure( normalized, "remove pathname" );
				}
			} else if ( OperatingSystem.IsWindows() ) {
				var attributes = File.GetAttributes( normalized );
				if ( (attributes & FileAttributes.Directory) != 0 ) {
					if ( physical.IsVolumeMountPoint ) {
						var mountPath = System.IO.Path.EndsInDirectorySeparator( normalized )
							? normalized
							: string.Concat( normalized, System.IO.Path.DirectorySeparatorChar );
						if ( !NativeDeleteVolumeMountPointWindows( mountPath ) ) {
							return FromWindowsFailure( normalized, "remove volume mount point" );
						}
					}
					if ( !NativeRemoveDirectoryWindows( normalized ) ) {
						return FromWindowsFailure( normalized, "remove directory link" );
					}
				} else {
					File.Delete( normalized );
				}
			} else {
				return FileSystemMutationResult.Unsupported(
					normalized,
					"Name removal is not implemented for this platform."
				);
			}
			return FileSystemMutationResult.Success(
				new FileSystemMutationOutcome(
					normalized,
					FileSystemMutationOperation.RemoveFile,
					physical.Kind,
					physical.EntryIdentity,
					null,
					false
				)
			);
		} catch ( OperationCanceledException exception ) {
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return FromManagedFailure( normalized, exception, creation: false );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> RemoveDirectoryAsync(
		string path,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanRemoveDirectories ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Directory removal is not implemented for this platform."
			);
		}
		var condition = precondition ?? new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			PathDereferenceMode.NoFollow,
			FileSystemEntryKind.Directory
		);
		if ( condition.DereferenceMode != PathDereferenceMode.NoFollow ) {
			return FileSystemMutationResult.Failure(
				normalized,
				FileSystemMutationErrorCode.UnsafePathIndirection,
				"Directory removal cannot follow terminal pathname indirection."
			);
		}
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		var physical = validation.Physical;
		if ( physical is null ) {
			return NotFound( normalized );
		}
		if ( physical.Kind != FileSystemEntryKind.Directory || physical.IsPathIndirection ) {
			return WrongKind( normalized, "The pathname does not name a physical directory." );
		}
		var revalidation = await RevalidateAsync(
			normalized,
			validation,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		if ( revalidation is not null ) {
			return revalidation;
		}

		try {
			cancellationToken.ThrowIfCancellationRequested();
			if ( IsUnixLike ) {
				if ( NativeRemoveDirectory( normalized ) != 0 ) {
					return FromNativeFailure( normalized, "remove directory" );
				}
			} else if ( OperatingSystem.IsWindows() ) {
				if ( !NativeRemoveDirectoryWindows( normalized ) ) {
					return FromWindowsFailure( normalized, "remove directory" );
				}
			} else {
				return FileSystemMutationResult.Unsupported(
					normalized,
					"Directory removal is not implemented for this platform."
				);
			}
			return FileSystemMutationResult.Success(
				new FileSystemMutationOutcome(
					normalized,
					FileSystemMutationOperation.RemoveDirectory,
					FileSystemEntryKind.Directory,
					physical.EntryIdentity,
					null,
					false
				)
			);
		} catch ( OperationCanceledException exception ) {
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return FromManagedFailure( normalized, exception, creation: false );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> SetModeAsync(
		string path,
		PosixFileMode mode,
		PathDereferenceMode dereferenceMode,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanSetModes ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"POSIX mode mutation is not supported on this platform."
			);
		}
		var condition = precondition ?? new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			dereferenceMode
		);
		if ( condition.DereferenceMode != dereferenceMode ) {
			return FileSystemMutationResult.Failure(
				normalized,
				FileSystemMutationErrorCode.UnsafePathIndirection,
				"The mode request and precondition dereference policies differ."
			);
		}
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		var effective = validation.Effective;
		if ( effective is null ) {
			return NotFound( normalized );
		}
		if (
			dereferenceMode == PathDereferenceMode.NoFollow
				&& validation.Physical?.IsPathIndirection == true
		) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"This platform does not provide portable mode bits for a terminal link object."
			);
		}
		var revalidation = await RevalidateAsync(
			normalized,
			validation,
			dereferenceMode,
			cancellationToken
		).ConfigureAwait( false );
		if ( revalidation is not null ) {
			return revalidation;
		}

		try {
			cancellationToken.ThrowIfCancellationRequested();
			File.SetUnixFileMode( normalized, mode.ToUnixFileMode() );
			var post = await TryObserveAsync(
				normalized,
				dereferenceMode,
				CancellationToken.None
			).ConfigureAwait( false );
			if ( post is null ) {
				return IdentityChanged( normalized );
			}
			if ( IdentitiesConflict( effective.EntryIdentity, post.EntryIdentity ) ) {
				return IdentityChanged( normalized );
			}
			return FileSystemMutationResult.Success(
				new FileSystemMutationOutcome(
					normalized,
					FileSystemMutationOperation.SetMode,
					post.Kind,
					post.EntryIdentity,
					true,
					post.WasDereferenced
				)
			);
		} catch ( OperationCanceledException exception ) {
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return FromManagedFailure( normalized, exception, creation: false );
		}
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMutationResult> SetOwnershipAsync(
		string path,
		uint? userId,
		uint? groupId,
		PathDereferenceMode dereferenceMode,
		FileSystemMutationPrecondition? precondition = null,
		CancellationToken cancellationToken = default
	) {
		if ( !userId.HasValue && !groupId.HasValue ) {
			throw new ArgumentException( "At least one owner or group identifier must be supplied." );
		}
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		var normalized = TryNormalizePath( path, out var pathError );
		if ( normalized is null ) {
			return pathError!;
		}
		if ( !Capabilities.CanSetOwnership ) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"POSIX ownership mutation is not supported on this platform."
			);
		}
		if (
			dereferenceMode == PathDereferenceMode.NoFollow
				&& !Capabilities.CanSetOwnershipWithoutFollowingPathIndirection
		) {
			return FileSystemMutationResult.Unsupported(
				normalized,
				"Ownership mutation without following terminal pathname indirection is not supported on this platform."
			);
		}
		var condition = precondition ?? new FileSystemMutationPrecondition(
			FileSystemMutationExistence.MustExist,
			dereferenceMode
		);
		if ( condition.DereferenceMode != dereferenceMode ) {
			return FileSystemMutationResult.Failure(
				normalized,
				FileSystemMutationErrorCode.UnsafePathIndirection,
				"The ownership request and precondition dereference policies differ."
			);
		}
		var validation = await ValidateAsync( normalized, condition, cancellationToken ).ConfigureAwait( false );
		if ( validation.Error is not null ) {
			return validation.Error;
		}
		var effective = validation.Effective;
		if ( effective is null ) {
			return NotFound( normalized );
		}
		var revalidation = await RevalidateAsync(
			normalized,
			validation,
			dereferenceMode,
			cancellationToken,
			condition
		).ConfigureAwait( false );
		if ( revalidation is not null ) {
			return revalidation;
		}

		try {
			cancellationToken.ThrowIfCancellationRequested();
			var nativeUserId = userId ?? uint.MaxValue;
			var nativeGroupId = groupId ?? uint.MaxValue;
			var result = dereferenceMode == PathDereferenceMode.NoFollow
				? NativeChangeLinkOwnership( normalized, nativeUserId, nativeGroupId )
				: NativeChangeOwnership( normalized, nativeUserId, nativeGroupId );
			if ( result != 0 ) {
				return FromNativeFailure( normalized, "change ownership" );
			}
			var post = await TryObserveAsync(
				normalized,
				dereferenceMode,
				CancellationToken.None
			).ConfigureAwait( false );
			if ( post is null || IdentitiesConflict( effective.EntryIdentity, post.EntryIdentity ) ) {
				return IdentityChanged( normalized );
			}
			return FileSystemMutationResult.Success(
				new FileSystemMutationOutcome(
					normalized,
					FileSystemMutationOperation.SetOwnership,
					post.Kind,
					post.EntryIdentity,
					null,
					post.WasDereferenced
				)
			);
		} catch ( OperationCanceledException exception ) {
			return Cancelled( normalized, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return FromManagedFailure( normalized, exception, creation: false );
		}
	}

	private async ValueTask<Validation> ValidateAsync(
		string path,
		FileSystemMutationPrecondition precondition,
		CancellationToken cancellationToken
	) {
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var physical = await TryObserveAsync(
				path,
				PathDereferenceMode.NoFollow,
				cancellationToken
			).ConfigureAwait( false );
			if ( physical is null ) {
				if ( precondition.Existence == FileSystemMutationExistence.MustExist ) {
					return new Validation( null, null, NotFound( path ) );
				}
				return new Validation( null, null, null );
			}
			if ( precondition.Existence == FileSystemMutationExistence.MustNotExist ) {
				return new Validation( physical, physical, AlreadyExists( path ) );
			}
			if (
				precondition.RejectUncharacterizedIndirection
					&& physical.IsReparsePoint
					&& !physical.Indirection.CanResolveAsPath
			) {
				return new Validation(
					physical,
					physical,
					FileSystemMutationResult.Failure(
						path,
						FileSystemMutationErrorCode.UnsafePathIndirection,
						"The terminal reparse point is not characterized as safe pathname indirection."
					)
				);
			}

			var effective = precondition.DereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection
				&& physical.IsPathIndirection
					? await TryObserveAsync(
						path,
						PathDereferenceMode.FollowEligiblePathIndirection,
						cancellationToken
					).ConfigureAwait( false )
					: physical;
			if ( effective is null ) {
				return new Validation( physical, null, NotFound( path ) );
			}
			if ( precondition.ExpectedKind.HasValue && effective.Kind != precondition.ExpectedKind.Value ) {
				return new Validation(
					physical,
					effective,
					WrongKind(
						path,
						string.Concat(
							"The pathname kind changed; expected ",
							precondition.ExpectedKind.Value,
							" but observed ",
							effective.Kind,
							"."
						)
					)
				);
			}
			if (
				precondition.ExpectedIdentity.HasValue
					&& IdentitiesConflict( precondition.ExpectedIdentity.Value, effective.EntryIdentity )
			) {
				return new Validation( physical, effective, IdentityChanged( path ) );
			}
			if ( OwnershipConflicts( precondition, effective ) ) {
				return new Validation( physical, effective, IdentityChanged( path ) );
			}
			return new Validation( physical, effective, null );
		} catch ( OperationCanceledException exception ) {
			return new Validation( null, null, Cancelled( path, exception ) );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return new Validation( null, null, FromManagedFailure( path, exception, creation: false ) );
		}
	}

	private async ValueTask<FileSystemMutationResult?> RevalidateAsync(
		string path,
		Validation validation,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken,
		FileSystemMutationPrecondition? precondition = null
	) {
		try {
			var expected = validation.Effective;
			if ( expected is null ) {
				return NotFound( path );
			}
			var current = await TryObserveAsync( path, dereferenceMode, cancellationToken ).ConfigureAwait( false );
			if ( current is null ) {
				return IdentityChanged( path );
			}
			if (
				IdentitiesConflict( expected.EntryIdentity, current.EntryIdentity )
					|| expected.Kind != current.Kind
					|| (precondition is not null && OwnershipConflicts( precondition, current ))
			) {
				return IdentityChanged( path );
			}
			return null;
		} catch ( OperationCanceledException exception ) {
			return Cancelled( path, exception );
		} catch ( Exception exception ) when ( IsControlledException( exception ) ) {
			return FromManagedFailure( path, exception, creation: false );
		}
	}

	private static bool OwnershipConflicts(
		FileSystemMutationPrecondition precondition,
		FileSystemMetadata metadata
	) {
		return precondition.ExpectedUserId.HasValue
			&& (
				!metadata.UserId.IsAvailable
					|| metadata.UserId.GetRequiredValue() != precondition.ExpectedUserId.Value
			)
			|| precondition.ExpectedGroupId.HasValue
				&& (
					!metadata.GroupId.IsAvailable
						|| metadata.GroupId.GetRequiredValue() != precondition.ExpectedGroupId.Value
				);
	}

	private async ValueTask<FileSystemMetadata?> TryObserveAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken
	) {
		try {
			return await metadataProvider.GetMetadataAsync(
				path,
				dereferenceMode,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( FileNotFoundException ) {
			return null;
		} catch ( DirectoryNotFoundException ) {
			return null;
		}
	}

	private async ValueTask<FileSystemMutationResult> ObserveOutcomeAsync(
		string path,
		FileSystemMutationOperation operation,
		bool? modeApplied,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken
	) {
		var metadata = await TryObserveAsync( path, dereferenceMode, cancellationToken ).ConfigureAwait( false );
		if ( metadata is null ) {
			return IdentityChanged( path );
		}
		var message = modeApplied == false
			? "The object was created, but this platform does not expose POSIX mode mutation."
			: null;
		return FileSystemMutationResult.Success(
			new FileSystemMutationOutcome(
				path,
				operation,
				metadata.Kind,
				metadata.EntryIdentity,
				modeApplied,
				metadata.WasDereferenced
			),
			message
		);
	}

	private static string? TryNormalizePath(
		string path,
		out FileSystemMutationResult? error
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		try {
			error = null;
			return System.IO.Path.GetFullPath( path );
		} catch ( Exception exception ) when (
			exception is ArgumentException or NotSupportedException or PathTooLongException
		) {
			error = FileSystemMutationResult.Failure(
				path,
				FileSystemMutationErrorCode.InvalidPath,
				exception.Message,
				exception
			);
			return null;
		}
	}

	private static FileSystemMutationCapabilities GetCapabilities() {
		var unix = IsUnixLike;
		var known = unix || OperatingSystem.IsWindows();
		return new FileSystemMutationCapabilities(
			known,
			known,
			known,
			known,
			unix,
			unix,
			known,
			known,
			unix,
			false,
			OperatingSystem.IsWindows(),
			unix,
			unix
		);
	}

	private static bool IsUnixLike =>
		OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD();

	private static bool IsRemoteWindowsPath( string path ) {
		return path.StartsWith( @"\\?\UNC\", StringComparison.OrdinalIgnoreCase )
			|| path.StartsWith( @"\??\UNC\", StringComparison.OrdinalIgnoreCase )
			|| (
				path.StartsWith( @"\\", StringComparison.Ordinal )
					&& !path.StartsWith( @"\\?\", StringComparison.Ordinal )
			);
	}

	private static bool IsExactWindowsVolumeGuidPath( string path ) {
		const string extendedPrefix = @"\\?\Volume{";
		const string nativePrefix = @"\??\Volume{";
		var prefixLength = path.StartsWith( extendedPrefix, StringComparison.OrdinalIgnoreCase )
			? extendedPrefix.Length
			: path.StartsWith( nativePrefix, StringComparison.OrdinalIgnoreCase )
				? nativePrefix.Length
				: 0;
		if ( prefixLength == 0 ) {
			return false;
		}
		var closingBrace = path.IndexOf( '}', prefixLength );
		return closingBrace >= prefixLength
			&& (
				closingBrace == path.Length - 1
					|| (
						closingBrace == path.Length - 2
							&& System.IO.Path.EndsInDirectorySeparator( path )
					)
			);
	}

	private static byte[] CreateJunctionReparseData( string target ) {
		var substituteName = ToNativeWindowsPath( target );
		var substituteNameBytes = Encoding.Unicode.GetBytes( substituteName );
		var printNameBytes = Encoding.Unicode.GetBytes( target );
		var printNameOffset = checked( substituteNameBytes.Length + sizeof( char ) );
		var pathBufferLength = checked( printNameOffset + printNameBytes.Length + sizeof( char ) );
		var reparseDataLength = checked( 8 + pathBufferLength );
		var totalLength = checked( 8 + reparseDataLength );
		if (
			totalLength > WindowsMaximumReparseDataBufferSize
				|| substituteNameBytes.Length > ushort.MaxValue
				|| printNameBytes.Length > ushort.MaxValue
				|| printNameOffset > ushort.MaxValue
				|| reparseDataLength > ushort.MaxValue
		) {
			throw new PathTooLongException( "The junction target is too long for a Windows reparse-point buffer." );
		}

		var buffer = new byte[totalLength];
		BinaryPrimitives.WriteUInt32LittleEndian( buffer.AsSpan( 0, 4 ), WindowsMountPointReparseTag );
		BinaryPrimitives.WriteUInt16LittleEndian( buffer.AsSpan( 4, 2 ), checked( (ushort)reparseDataLength ) );
		BinaryPrimitives.WriteUInt16LittleEndian( buffer.AsSpan( 6, 2 ), 0 );
		BinaryPrimitives.WriteUInt16LittleEndian( buffer.AsSpan( 8, 2 ), 0 );
		BinaryPrimitives.WriteUInt16LittleEndian(
			buffer.AsSpan( 10, 2 ),
			checked( (ushort)substituteNameBytes.Length )
		);
		BinaryPrimitives.WriteUInt16LittleEndian(
			buffer.AsSpan( 12, 2 ),
			checked( (ushort)printNameOffset )
		);
		BinaryPrimitives.WriteUInt16LittleEndian(
			buffer.AsSpan( 14, 2 ),
			checked( (ushort)printNameBytes.Length )
		);
		substituteNameBytes.CopyTo( buffer, 16 );
		printNameBytes.CopyTo( buffer, checked( 16 + printNameOffset ) );
		return buffer;
	}

	private static string ToNativeWindowsPath( string path ) {
		if ( path.StartsWith( @"\\?\", StringComparison.Ordinal ) ) {
			return string.Concat( @"\??\", path[4..] );
		}
		if ( path.StartsWith( @"\??\", StringComparison.Ordinal ) ) {
			return path;
		}
		return string.Concat( @"\??\", path );
	}

	private static bool TryEncodeDeviceNumber( DeviceNumber number, out ulong value ) {
		if ( OperatingSystem.IsLinux() ) {
			var major = (ulong)number.Major;
			var minor = (ulong)number.Minor;
			value = (minor & 0xffUL)
				| ((major & 0xfffUL) << 8)
				| ((minor & ~0xffUL) << 12)
				| ((major & ~0xfffUL) << 32);
			return true;
		}
		if ( OperatingSystem.IsMacOS() ) {
			if ( number.Major > 0xff || number.Minor > 0x00ff_ffff ) {
				value = 0;
				return false;
			}
			value = ((ulong)number.Major << 24) | number.Minor;
			return true;
		}
		if ( OperatingSystem.IsFreeBSD() ) {
			var major = (ulong)number.Major;
			var minor = (ulong)number.Minor;
			value = ((major & 0xffff_ff00UL) << 32)
				| ((major & 0x0000_00ffUL) << 8)
				| ((minor & 0x0000_ff00UL) << 24)
				| (minor & 0xffff_00ffUL);
			return true;
		}
		value = 0;
		return false;
	}

	private static bool IdentitiesConflict(
		FileSystemEntryIdentity expected,
		FileSystemEntryIdentity actual
	) {
		if ( !expected.IsAvailable ) {
			return false;
		}
		return !actual.IsAvailable || expected != actual;
	}

	private static FileSystemMutationResult AlreadyExists( string path ) {
		return FileSystemMutationResult.Failure(
			path,
			FileSystemMutationErrorCode.AlreadyExists,
			"The pathname already exists."
		);
	}

	private static FileSystemMutationResult NotFound( string path ) {
		return FileSystemMutationResult.Failure(
			path,
			FileSystemMutationErrorCode.NotFound,
			"The pathname does not exist."
		);
	}

	private static FileSystemMutationResult WrongKind( string path, string message ) {
		return FileSystemMutationResult.Failure(
			path,
			FileSystemMutationErrorCode.WrongObjectKind,
			message
		);
	}

	private static FileSystemMutationResult IdentityChanged( string path ) {
		return FileSystemMutationResult.Failure(
			path,
			FileSystemMutationErrorCode.IdentityChanged,
			"The pathname identity changed before the requested mutation could be completed."
		);
	}

	private static FileSystemMutationResult Cancelled(
		string path,
		OperationCanceledException exception
	) {
		return FileSystemMutationResult.Failure(
			path,
			FileSystemMutationErrorCode.Cancelled,
			exception.Message,
			exception
		);
	}

	private static FileSystemMutationResult FromNativeFailure( string path, string operation ) {
		var error = Marshal.GetLastPInvokeError();
		var exception = new Win32Exception( error );
		var message = string.Concat( "Unable to ", operation, ": ", exception.Message );
		var errorCode = error == 22 && operation == "create device node"
			? FileSystemMutationErrorCode.InvalidDeviceNumber
			: MapNativeError( error );
		return errorCode == FileSystemMutationErrorCode.Unsupported
			? FileSystemMutationResult.Unsupported( path, message )
			: FileSystemMutationResult.Failure( path, errorCode, message, exception );
	}

	private static FileSystemMutationResult FromWindowsFailure( string path, string operation ) {
		var error = Marshal.GetLastPInvokeError();
		var exception = new Win32Exception( error );
		var message = string.Concat( "Unable to ", operation, ": ", exception.Message );
		var errorCode = MapWindowsError( error );
		return errorCode == FileSystemMutationErrorCode.Unsupported
			? FileSystemMutationResult.Unsupported( path, message )
			: FileSystemMutationResult.Failure( path, errorCode, message, exception );
	}

	private static FileSystemMutationErrorCode MapWindowsError( int error ) {
		return error switch {
			1 or 50 or 120 => FileSystemMutationErrorCode.Unsupported,
			2 => FileSystemMutationErrorCode.NotFound,
			3 => FileSystemMutationErrorCode.ParentNotFound,
			5 => FileSystemMutationErrorCode.AccessDenied,
			17 => FileSystemMutationErrorCode.CrossDevice,
			80 or 183 => FileSystemMutationErrorCode.AlreadyExists,
			87 or 123 or 206 => FileSystemMutationErrorCode.InvalidPath,
			145 => FileSystemMutationErrorCode.DirectoryNotEmpty,
			267 or 4390 or 4394 => FileSystemMutationErrorCode.WrongObjectKind,
			1314 => FileSystemMutationErrorCode.PrivilegeRequired,
			4392 or 4393 => FileSystemMutationErrorCode.InvalidPath,
			_ => FileSystemMutationErrorCode.IoFailure
		};
	}

	private static bool TryMapWindowsException(
		Exception exception,
		out FileSystemMutationErrorCode errorCode
	) {
		var hresult = unchecked( (uint)exception.HResult );
		if ( (hresult & 0xffff0000U) != 0x80070000U ) {
			errorCode = FileSystemMutationErrorCode.IoFailure;
			return false;
		}
		errorCode = MapWindowsError( checked( (int)(hresult & 0x0000ffffU) ) );
		return true;
	}

	private static FileSystemMutationErrorCode MapNativeError( int error ) {
		return error switch {
			1 => FileSystemMutationErrorCode.PrivilegeRequired,
			2 => FileSystemMutationErrorCode.NotFound,
			13 => FileSystemMutationErrorCode.AccessDenied,
			17 => FileSystemMutationErrorCode.AlreadyExists,
			18 => FileSystemMutationErrorCode.CrossDevice,
			20 => FileSystemMutationErrorCode.ParentNotFound,
			21 => FileSystemMutationErrorCode.WrongObjectKind,
			30 => FileSystemMutationErrorCode.AccessDenied,
			39 or 66 => FileSystemMutationErrorCode.DirectoryNotEmpty,
			45 or 95 or 38 => FileSystemMutationErrorCode.Unsupported,
			_ => FileSystemMutationErrorCode.IoFailure
		};
	}

	private static FileSystemMutationResult FromManagedFailure(
		string path,
		Exception exception,
		bool creation
	) {
		FileSystemMutationErrorCode errorCode;
		if ( OperatingSystem.IsWindows() && TryMapWindowsException( exception, out errorCode ) ) {
			return errorCode == FileSystemMutationErrorCode.Unsupported
				? FileSystemMutationResult.Unsupported( path, exception.Message )
				: FileSystemMutationResult.Failure( path, errorCode, exception.Message, exception );
		}
		errorCode = exception switch {
			UnauthorizedAccessException or System.Security.SecurityException => FileSystemMutationErrorCode.AccessDenied,
			FileNotFoundException => FileSystemMutationErrorCode.NotFound,
			DirectoryNotFoundException => FileSystemMutationErrorCode.ParentNotFound,
			PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException
				or BadImageFormatException => FileSystemMutationErrorCode.Unsupported,
			PathTooLongException or ArgumentException or NotSupportedException => FileSystemMutationErrorCode.InvalidPath,
			IOException when creation && PathObjectExists( path ) => FileSystemMutationErrorCode.AlreadyExists,
			IOException when !creation && IsNonemptyDirectory( path ) => FileSystemMutationErrorCode.DirectoryNotEmpty,
			IOException => FileSystemMutationErrorCode.IoFailure,
			_ => FileSystemMutationErrorCode.IoFailure
		};
		return errorCode == FileSystemMutationErrorCode.Unsupported
			? FileSystemMutationResult.Unsupported( path, exception.Message )
			: FileSystemMutationResult.Failure( path, errorCode, exception.Message, exception );
	}


	private static bool PathObjectExists( string path ) {
		if ( File.Exists( path ) || Directory.Exists( path ) ) {
			return true;
		}
		try {
			_ = File.GetAttributes( path );
			return true;
		} catch ( FileNotFoundException ) {
			return false;
		} catch ( DirectoryNotFoundException ) {
			return false;
		} catch {
			return false;
		}
	}

	private static bool IsNonemptyDirectory( string path ) {
		try {
			return Directory.Exists( path ) && Directory.EnumerateFileSystemEntries( path ).Any();
		} catch {
			return false;
		}
	}

	private static bool IsControlledException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or System.Security.SecurityException
			or ArgumentException
			or NotSupportedException
			or DllNotFoundException
			or EntryPointNotFoundException
			or BadImageFormatException;
	}

	private static void TryDeleteCreatedFile( string path ) {
		try {
			if ( IsUnixLike ) {
				_ = NativeRemoveFile( path );
			} else if ( OperatingSystem.IsWindows() ) {
				var attributes = File.GetAttributes( path );
				if ( (attributes & FileAttributes.Directory) != 0 ) {
					Directory.Delete( path, false );
				} else {
					File.Delete( path );
				}
			}
		} catch {
			// Preserve the primary failure.
		}
	}

	private static void TryDeleteCreatedDirectory( string path ) {
		try {
			if ( IsUnixLike ) {
				_ = NativeRemoveDirectory( path );
			} else if ( OperatingSystem.IsWindows() ) {
				_ = NativeRemoveDirectoryWindows( path );
			}
		} catch {
			// Preserve the primary failure.
		}
	}

	[DllImport( "kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, CharSet = CharSet.Unicode )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeCreateDirectoryWindows( string path, IntPtr securityAttributes );

	[DllImport( "kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode )]
	private static extern SafeFileHandle NativeOpenReparsePointWindows(
		string path,
		uint desiredAccess,
		uint shareMode,
		IntPtr securityAttributes,
		uint creationDisposition,
		uint flagsAndAttributes,
		IntPtr templateFile
	);

	[DllImport( "kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeSetReparsePointWindows(
		SafeFileHandle device,
		uint controlCode,
		byte[] inputBuffer,
		uint inputBufferSize,
		IntPtr outputBuffer,
		uint outputBufferSize,
		out uint bytesReturned,
		IntPtr overlapped
	);

	[DllImport( "kernel32.dll", EntryPoint = "DeleteVolumeMountPointW", SetLastError = true, CharSet = CharSet.Unicode )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeDeleteVolumeMountPointWindows( string volumeMountPoint );

	[DllImport( "kernel32.dll", EntryPoint = "RemoveDirectoryW", SetLastError = true, CharSet = CharSet.Unicode )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeRemoveDirectoryWindows( string path );

	[DllImport( "kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, CharSet = CharSet.Unicode )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeCreateHardLinkWindows(
		string path,
		string existingPath,
		IntPtr securityAttributes
	);

	[DllImport( "libc", EntryPoint = "mkdir", SetLastError = true )]
	private static extern int NativeCreateDirectory( string path, uint mode );

	[DllImport( "libc", EntryPoint = "mkfifo", SetLastError = true )]
	private static extern int NativeCreateFifo( string path, uint mode );

	[DllImport( "libc", EntryPoint = "mknod", SetLastError = true )]
	private static extern int NativeCreateDeviceNode( string path, uint mode, ulong device );

	[DllImport( "libc", EntryPoint = "linkat", SetLastError = true )]
	private static extern int NativeCreateHardLinkLinux(
		int oldDirectoryFileDescriptor,
		string oldPath,
		int newDirectoryFileDescriptor,
		string newPath,
		int flags
	);

	[DllImport( "libSystem.dylib", EntryPoint = "linkat", SetLastError = true )]
	private static extern int NativeCreateHardLinkMacOS(
		int oldDirectoryFileDescriptor,
		string oldPath,
		int newDirectoryFileDescriptor,
		string newPath,
		int flags
	);

	[DllImport( "libc", EntryPoint = "linkat", SetLastError = true )]
	private static extern int NativeCreateHardLinkFreeBsd(
		int oldDirectoryFileDescriptor,
		string oldPath,
		int newDirectoryFileDescriptor,
		string newPath,
		int flags
	);

	[DllImport( "libc", EntryPoint = "chown", SetLastError = true )]
	private static extern int NativeChangeOwnership( string path, uint userId, uint groupId );

	[DllImport( "libc", EntryPoint = "lchown", SetLastError = true )]
	private static extern int NativeChangeLinkOwnership( string path, uint userId, uint groupId );

	[DllImport( "libc", EntryPoint = "unlink", SetLastError = true )]
	private static extern int NativeRemoveFile( string path );

	[DllImport( "libc", EntryPoint = "rmdir", SetLastError = true )]
	private static extern int NativeRemoveDirectory( string path );

	/// <summary>Captures one pre-mutation observation and any controlled validation failure.</summary>
	private sealed class Validation {
		/// <summary>Initializes a validation snapshot.</summary>
		/// <param name="physical">The physical terminal object, when present.</param>
		/// <param name="effective">The object selected by the dereference policy, when present.</param>
		/// <param name="error">A controlled validation failure, when validation failed.</param>
		public Validation(
			FileSystemMetadata? physical,
			FileSystemMetadata? effective,
			FileSystemMutationResult? error
		) {
			Physical = physical;
			Effective = effective;
			Error = error;
		}

		/// <summary>Gets the physical terminal object, when present.</summary>
		public FileSystemMetadata? Physical { get; }

		/// <summary>Gets the object selected by the dereference policy, when present.</summary>
		public FileSystemMetadata? Effective { get; }

		/// <summary>Gets the controlled validation failure, when validation failed.</summary>
		public FileSystemMutationResult? Error { get; }
	}
}
