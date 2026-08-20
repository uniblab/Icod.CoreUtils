using Path = global::System.IO.Path;
using PathIndirectionKind = Icod.Path.PathIndirectionKind;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;
using Microsoft.Win32.SafeHandles;

namespace Icod.CoreUtils.Shared.FileSystem.Metadata;

/// <summary>
/// Provides authoritative metadata through the host BCL and narrow native adapters.
/// </summary>
public sealed class SystemFileSystemMetadataProvider : IFileSystemMetadataProvider {
	private const int LinuxAtFileDescriptorCurrentWorkingDirectory = -100;
	private const int LinuxAtSymbolicLinkNoFollow = 0x100;
	private const long LinuxUnixTimeNow = 1_073_741_823;
	private const long LinuxUnixTimeOmit = 1_073_741_822;
	private const int DarwinAtFileDescriptorCurrentWorkingDirectory = -2;
	private const int DarwinAtSymbolicLinkNoFollow = 0x0020;
	private const long DarwinUnixTimeNow = -1;
	private const long DarwinUnixTimeOmit = -2;
	private const uint StatxType = 0x00000001;
	private const uint StatxMode = 0x00000002;
	private const uint StatxLinkCount = 0x00000004;
	private const uint StatxUserIdentifier = 0x00000008;
	private const uint StatxGroupIdentifier = 0x00000010;
	private const uint StatxAccessTime = 0x00000020;
	private const uint StatxModificationTime = 0x00000040;
	private const uint StatxChangeTime = 0x00000080;
	private const uint StatxInode = 0x00000100;
	private const uint StatxSize = 0x00000200;
	private const uint StatxBlocks = 0x00000400;
	private const uint StatxBasicStatistics = 0x000007ff;
	private const uint StatxBirthTime = 0x00000800;
	private const uint StatxMountIdentifier = 0x00001000;
	private const uint FileFlagBackupSemantics = 0x02000000;
	private const uint FileFlagOpenReparsePoint = 0x00200000;
	private const uint FileWriteAttributes = 0x00000100;
	private const uint ReadControl = 0x00020000;
	private const uint OwnerSecurityInformation = 0x00000001;
	private const uint GroupSecurityInformation = 0x00000002;
	private const int ErrorInsufficientBuffer = 122;
	private const uint OpenExisting = 3;
	private const uint FileReadOnlyVolume = 0x00080000;
	private const ulong PosixReadOnlyFileSystem = 0x00000001;

	private readonly IReadOnlyFileSystemProvider readOnlyProvider;

	/// <summary>Gets the shared system provider.</summary>
	public static SystemFileSystemMetadataProvider Instance { get; } = new(
		SystemReadOnlyFileSystemProvider.Instance
	);

	/// <summary>
	/// Initializes a provider over an injectable E1 read-only provider.
	/// </summary>
	/// <param name="readOnlyProvider">The provider that supplies link state and stable E1 identities.</param>
	public SystemFileSystemMetadataProvider( IReadOnlyFileSystemProvider readOnlyProvider ) {
		this.readOnlyProvider = readOnlyProvider ?? throw new ArgumentNullException( nameof( readOnlyProvider ) );
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemMetadata> GetMetadataAsync(
		string path,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		cancellationToken.ThrowIfCancellationRequested();

		var physical = await readOnlyProvider.ObserveAsync(
			path,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		var effective = followSymbolicLink && physical.Indirection.CanResolveAsPath
			? await readOnlyProvider.ObserveAsync(
				path,
				PathDereferenceMode.FollowEligiblePathIndirection,
				cancellationToken
			).ConfigureAwait( false )
			: physical;
		cancellationToken.ThrowIfCancellationRequested();

		try {
			if ( OperatingSystem.IsWindows() ) {
				var windows = TryGetWindowsMetadata( path, effective.WasDereferenced, physical, effective );
				if ( windows is not null ) {
					return windows;
				}
			} else if ( OperatingSystem.IsLinux() ) {
				var linux = TryGetLinuxMetadata( path, effective.WasDereferenced, physical, effective );
				if ( linux is not null ) {
					return linux;
				}
			} else if ( OperatingSystem.IsMacOS() ) {
				var darwin = TryGetDarwinMetadata( path, effective.WasDereferenced, physical, effective );
				if ( darwin is not null ) {
					return darwin;
				}
			}
		} catch ( Exception exception ) when (
			exception is DllNotFoundException
				or EntryPointNotFoundException
				or BadImageFormatException
		) {
			// Fall through to the managed observation with explicit unavailable native fields.
		}

		return GetManagedMetadata( path, effective.WasDereferenced, physical, effective );
	}

	/// <inheritdoc/>
	public ValueTask<FileSystemMetadata> GetMetadataAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		return GetMetadataAsync(
			path,
			dereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public async ValueTask<FileSystemInformation> GetFileSystemInformationAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		cancellationToken.ThrowIfCancellationRequested();

		var metadata = await GetMetadataAsync( path, true, cancellationToken ).ConfigureAwait( false );
		var fullPath = Path.GetFullPath( path );
		var drive = FindContainingDrive( fullPath );
		var native = TryGetNativeFileSystemDetails( fullPath );

		if ( drive is null ) {
			return new FileSystemInformation( path, metadata.FileSystemIdentity ) {
				MountPoint = native.MountPoint,
				FileSystemType = native.FileSystemType,
				VolumeName = native.VolumeName,
				TotalBytes = native.TotalBytes,
				FreeBytes = native.FreeBytes,
				AvailableBytes = native.AvailableBytes,
				BlockSize = native.BlockSize,
				FragmentSize = native.FragmentSize,
				MaximumNameLength = native.MaximumNameLength,
				IsReadOnly = native.IsReadOnly
			};
		}

		return new FileSystemInformation( path, metadata.FileSystemIdentity ) {
			MountPoint = Prefer( native.MountPoint, Capture( () => drive.RootDirectory.FullName ) ),
			FileSystemType = Prefer( native.FileSystemType, Capture( () => drive.DriveFormat ) ),
			VolumeName = Prefer( native.VolumeName, Capture( () => drive.VolumeLabel ) ),
			TotalBytes = Prefer( native.TotalBytes, CaptureUnsigned( () => drive.TotalSize ) ),
			FreeBytes = Prefer( native.FreeBytes, CaptureUnsigned( () => drive.TotalFreeSpace ) ),
			AvailableBytes = Prefer( native.AvailableBytes, CaptureUnsigned( () => drive.AvailableFreeSpace ) ),
			BlockSize = native.BlockSize,
			FragmentSize = native.FragmentSize,
			MaximumNameLength = native.MaximumNameLength,
			IsReadOnly = native.IsReadOnly
		};
	}

	/// <inheritdoc/>
	public async ValueTask<PlatformOperationResult> SetTimestampsAsync(
		string path,
		FileTimestampMutationRequest request,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		ArgumentNullException.ThrowIfNull( request );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !request.HasChanges ) {
			return PlatformOperationResult.Success();
		}

		var physical = await readOnlyProvider.ObserveAsync(
			path,
			PathDereferenceMode.NoFollow,
			cancellationToken
		).ConfigureAwait( false );
		var capabilities = GetTimestampCapabilities();
		var unsupported = GetUnsupportedTimestampRequest( request, physical, followSymbolicLink, capabilities );
		if ( unsupported is not null ) {
			return PlatformOperationResult.Unsupported( unsupported );
		}
		var shouldDereference = followSymbolicLink && physical.Indirection.CanResolveAsPath;
		cancellationToken.ThrowIfCancellationRequested();

		try {
			if ( OperatingSystem.IsWindows() ) {
				return SetWindowsTimestamps( path, request, shouldDereference );
			}
			if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ) {
				return SetUnixTimestamps( path, request, shouldDereference );
			}
			return SetManagedTimestamps( path, request, physical, shouldDereference );
		} catch ( Exception exception ) when (
			exception is IOException
				or UnauthorizedAccessException
				or System.Security.SecurityException
				or ArgumentException
				or NotSupportedException
				or DllNotFoundException
				or EntryPointNotFoundException
				or BadImageFormatException
		) {
			return PlatformOperationResult.Failure( exception.Message, exception );
		}
	}

	/// <inheritdoc/>
	public ValueTask<PlatformOperationResult> SetTimestampsAsync(
		string path,
		FileTimestampMutationRequest request,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		return SetTimestampsAsync(
			path,
			request,
			dereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		);
	}

	private static FileSystemMetadata? TryGetWindowsMetadata(
		string path,
		bool followSymbolicLink,
		ReadOnlyFileSystemEntry physical,
		ReadOnlyFileSystemEntry effective
	) {
		var flags = FileFlagBackupSemantics;
		if ( !followSymbolicLink ) {
			flags |= FileFlagOpenReparsePoint;
		}
		using var handle = CreateFileW(
			path,
			0,
			FileShare.Read | FileShare.Write | FileShare.Delete,
			IntPtr.Zero,
			OpenExisting,
			flags,
			IntPtr.Zero
		);
		if ( handle.IsInvalid || !GetFileInformationByHandle( handle, out var information ) ) {
			return null;
		}

		var hasBasic = GetFileBasicInformationByHandle(
			handle,
			FileInformationClass.Basic,
			out var basic,
			checked( (uint)Marshal.SizeOf<FileBasicInformation>() )
		);
		var hasStandard = GetFileStandardInformationByHandle(
			handle,
			FileInformationClass.Standard,
			out var standard,
			checked( (uint)Marshal.SizeOf<FileStandardInformation>() )
		);
		var size = hasStandard
			? checked( (ulong)Math.Max( 0, standard.EndOfFile ) )
			: ((ulong)information.FileSizeHigh << 32) | information.FileSizeLow;
		var linkCount = hasStandard ? standard.NumberOfLinks : information.NumberOfLinks;
		var fileIndex = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
		var security = TryGetWindowsAccountNames( path, followSymbolicLink );
		var allocationSize = hasStandard
			? FileSystemMetadataValue<ulong>.Available( checked( (ulong)Math.Max( 0, standard.AllocationSize ) ) )
			: FileSystemMetadataValue<ulong>.Unavailable( "Windows allocation size could not be queried." );

		return new FileSystemMetadata(
			path,
			effective.Kind,
			physical.IsSymbolicLink,
			effective.WasDereferenced,
			effective.EntryIdentity,
			effective.FileSystemIdentity,
			physical.Indirection
		) {
			LinkTarget = GetLinkTargetValue( physical ),
			ReparseTag = GetReparseTagValue( physical ),
			LinkIdentity = GetLinkIdentityValue( physical ),
			Size = FileSystemMetadataValue<ulong>.Available( size ),
			LinkCount = FileSystemMetadataValue<ulong>.Available( linkCount ),
			Mode = FileSystemMetadataValue<uint>.Unsupported( "Windows does not expose a POSIX mode through this adapter." ),
			UserId = FileSystemMetadataValue<uint>.Unsupported( "Windows ownership is represented by security identifiers rather than numeric user IDs." ),
			GroupId = FileSystemMetadataValue<uint>.Unsupported( "Windows ownership is represented by security identifiers rather than numeric group IDs." ),
			OwnerName = security.OwnerName,
			GroupName = security.GroupName,
			AccessTime = hasBasic
				? FromWindowsFileTime( basic.LastAccessTime )
				: FromWindowsFileTime( information.LastAccessTime ),
			ModificationTime = hasBasic
				? FromWindowsFileTime( basic.LastWriteTime )
				: FromWindowsFileTime( information.LastWriteTime ),
			ChangeTime = hasBasic
				? FromWindowsFileTime( basic.ChangeTime )
				: FileSystemMetadataValue<DateTimeOffset>.Unavailable( "Windows change time could not be queried." ),
			BirthTime = hasBasic
				? FromWindowsFileTime( basic.CreationTime )
				: FromWindowsFileTime( information.CreationTime ),
			DeviceIdentifier = FileSystemMetadataValue<string>.Available(
				information.VolumeSerialNumber.ToString( "x8", CultureInfo.InvariantCulture )
			),
			InodeNumber = FileSystemMetadataValue<ulong>.Available( fileIndex ),
			SpecialDeviceIdentifier = FileSystemMetadataValue<string>.NotApplicable(),
			AllocatedBlocks = allocationSize.IsAvailable
				? FileSystemMetadataValue<ulong>.Available( DivideRoundUp( allocationSize.GetRequiredValue(), 512 ) )
				: FileSystemMetadataValue<ulong>.Unavailable( allocationSize.Message ),
			AllocationBlockSize = allocationSize.IsAvailable
				? FileSystemMetadataValue<ulong>.Available( 512 )
				: FileSystemMetadataValue<ulong>.Unavailable( allocationSize.Message ),
			PreferredIoBlockSize = FileSystemMetadataValue<ulong>.Unavailable( "Windows did not report a preferred I/O block size for this entry." ),
			Attributes = FileSystemMetadataValue<FileAttributes>.Available( information.FileAttributes ),
			TimestampMutationCapabilities = FileSystemMetadataValue<FileTimestampMutationCapabilities>.Available(
				GetTimestampCapabilities()
			)
		};
	}

	private static (
		FileSystemMetadataValue<string> OwnerName,
		FileSystemMetadataValue<string> GroupName
	) TryGetWindowsAccountNames( string path, bool followSymbolicLink ) {
		var flags = FileFlagBackupSemantics;
		if ( !followSymbolicLink ) {
			flags |= FileFlagOpenReparsePoint;
		}
		using var handle = CreateFileW(
			path,
			ReadControl,
			FileShare.Read | FileShare.Write | FileShare.Delete,
			IntPtr.Zero,
			OpenExisting,
			flags,
			IntPtr.Zero
		);
		if ( handle.IsInvalid ) {
			var message = new Win32Exception( Marshal.GetLastPInvokeError() ).Message;
			return (
				FileSystemMetadataValue<string>.Unavailable( message ),
				FileSystemMetadataValue<string>.Unavailable( message )
			);
		}

		var result = GetSecurityInfo(
			handle,
			SecurityObjectType.File,
			OwnerSecurityInformation | GroupSecurityInformation,
			out var ownerSid,
			out var groupSid,
			IntPtr.Zero,
			IntPtr.Zero,
			out var securityDescriptor
		);
		if ( result != 0 ) {
			if ( securityDescriptor != IntPtr.Zero ) {
				_ = LocalFree( securityDescriptor );
			}
			var message = new Win32Exception( checked( (int)result ) ).Message;
			return (
				FileSystemMetadataValue<string>.Unavailable( message ),
				FileSystemMetadataValue<string>.Unavailable( message )
			);
		}
		try {
			return (
				GetWindowsSidDisplayName( ownerSid, "owner" ),
				GetWindowsSidDisplayName( groupSid, "group" )
			);
		} finally {
			if ( securityDescriptor != IntPtr.Zero ) {
				_ = LocalFree( securityDescriptor );
			}
		}
	}

	private static FileSystemMetadataValue<string> GetWindowsSidDisplayName(
		IntPtr sid,
		string description
	) {
		if ( sid == IntPtr.Zero ) {
			return FileSystemMetadataValue<string>.Unavailable(
				string.Concat( "Windows did not report a ", description, " security identifier." )
			);
		}

		uint nameLength = 0;
		uint domainLength = 0;
		_ = LookupAccountSidW(
			null,
			sid,
			null,
			ref nameLength,
			null,
			ref domainLength,
			out _
		);
		var lookupError = Marshal.GetLastPInvokeError();
		if ( lookupError == ErrorInsufficientBuffer && nameLength > 0 ) {
			var name = new StringBuilder( checked( (int)nameLength ) );
			domainLength = Math.Max( domainLength, 1 );
			var domain = new StringBuilder( checked( (int)domainLength ) );
			if (
				LookupAccountSidW(
					null,
					sid,
					name,
					ref nameLength,
					domain,
					ref domainLength,
					out _
				)
			) {
				return FileSystemMetadataValue<string>.Available(
					domain.Length == 0
						? name.ToString()
						: string.Concat( domain, "\\", name )
				);
			}
			lookupError = Marshal.GetLastPInvokeError();
		}

		if ( ConvertSidToStringSidW( sid, out var sidText ) ) {
			try {
				var value = Marshal.PtrToStringUni( sidText );
				if ( !string.IsNullOrEmpty( value ) ) {
					return FileSystemMetadataValue<string>.Available( value );
				}
			} finally {
				if ( sidText != IntPtr.Zero ) {
					_ = LocalFree( sidText );
				}
			}
		} else {
			lookupError = Marshal.GetLastPInvokeError();
		}

		return FileSystemMetadataValue<string>.Unavailable(
			new Win32Exception( lookupError ).Message
		);
	}

	private static FileSystemMetadata? TryGetLinuxMetadata(
		string path,
		bool followSymbolicLink,
		ReadOnlyFileSystemEntry physical,
		ReadOnlyFileSystemEntry effective
	) {
		var flags = followSymbolicLink ? 0 : LinuxAtSymbolicLinkNoFollow;
		if (
			Statx(
				LinuxAtFileDescriptorCurrentWorkingDirectory,
				path,
				flags,
				StatxBasicStatistics | StatxBirthTime | StatxMountIdentifier,
				out var statistics
			) != 0
		) {
			return null;
		}

		var specialDevice = effective.Kind is FileSystemEntryKind.BlockDevice or FileSystemEntryKind.CharacterDevice
			? FileSystemMetadataValue<string>.Available(
				string.Concat(
					statistics.DeviceSpecialMajor.ToString( CultureInfo.InvariantCulture ),
					":",
					statistics.DeviceSpecialMinor.ToString( CultureInfo.InvariantCulture )
				)
			)
			: FileSystemMetadataValue<string>.NotApplicable();

		return new FileSystemMetadata(
			path,
			effective.Kind,
			physical.IsSymbolicLink,
			effective.WasDereferenced,
			effective.EntryIdentity,
			effective.FileSystemIdentity,
			physical.Indirection
		) {
			LinkTarget = GetLinkTargetValue( physical ),
			ReparseTag = GetReparseTagValue( physical ),
			LinkIdentity = GetLinkIdentityValue( physical ),
			Size = FromStatxUnsigned( statistics.Mask, StatxSize, statistics.Size, "size" ),
			LinkCount = FromStatxUnsigned( statistics.Mask, StatxLinkCount, statistics.LinkCount, "link count" ),
			Mode = FromStatxUnsigned32( statistics.Mask, StatxMode | StatxType, statistics.Mode, "mode" ),
			UserId = FromStatxUnsigned32( statistics.Mask, StatxUserIdentifier, statistics.UserIdentifier, "owner ID" ),
			GroupId = FromStatxUnsigned32( statistics.Mask, StatxGroupIdentifier, statistics.GroupIdentifier, "group ID" ),
			OwnerName = FileSystemMetadataValue<string>.Unavailable( "Owner-name resolution is intentionally separate from numeric statx ownership." ),
			GroupName = FileSystemMetadataValue<string>.Unavailable( "Group-name resolution is intentionally separate from numeric statx ownership." ),
			AccessTime = FromStatxTimestamp( statistics.Mask, StatxAccessTime, statistics.AccessTime, "access time" ),
			ModificationTime = FromStatxTimestamp( statistics.Mask, StatxModificationTime, statistics.ModificationTime, "modification time" ),
			ChangeTime = FromStatxTimestamp( statistics.Mask, StatxChangeTime, statistics.ChangeTime, "inode-change time" ),
			BirthTime = FromStatxTimestamp( statistics.Mask, StatxBirthTime, statistics.BirthTime, "birth time" ),
			DeviceIdentifier = FileSystemMetadataValue<string>.Available(
				string.Concat(
					statistics.DeviceMajor.ToString( CultureInfo.InvariantCulture ),
					":",
					statistics.DeviceMinor.ToString( CultureInfo.InvariantCulture )
				)
			),
			InodeNumber = FromStatxUnsigned( statistics.Mask, StatxInode, statistics.Inode, "inode" ),
			SpecialDeviceIdentifier = specialDevice,
			AllocatedBlocks = FromStatxUnsigned( statistics.Mask, StatxBlocks, statistics.Blocks, "allocated blocks" ),
			AllocationBlockSize = (statistics.Mask & StatxBlocks) != 0
				? FileSystemMetadataValue<ulong>.Available( 512 )
				: FileSystemMetadataValue<ulong>.Unavailable( "statx did not report allocated blocks." ),
			PreferredIoBlockSize = FileSystemMetadataValue<ulong>.Available( statistics.BlockSize ),
			Attributes = Capture( () => File.GetAttributes( path ) ),
			TimestampMutationCapabilities = FileSystemMetadataValue<FileTimestampMutationCapabilities>.Available(
				GetTimestampCapabilities()
			)
		};
	}

	private static FileSystemMetadata? TryGetDarwinMetadata(
		string path,
		bool followSymbolicLink,
		ReadOnlyFileSystemEntry physical,
		ReadOnlyFileSystemEntry effective
	) {
		if ( InvokeDarwinStat( path, followSymbolicLink, out var statistics ) != 0 ) {
			return null;
		}
		var device = unchecked( (uint)statistics.Device );
		var specialDevice = effective.Kind is FileSystemEntryKind.BlockDevice or FileSystemEntryKind.CharacterDevice
			? FileSystemMetadataValue<string>.Available(
				unchecked( (uint)statistics.SpecialDevice ).ToString( CultureInfo.InvariantCulture )
			)
			: FileSystemMetadataValue<string>.NotApplicable();

		return new FileSystemMetadata(
			path,
			effective.Kind,
			physical.IsSymbolicLink,
			effective.WasDereferenced,
			effective.EntryIdentity,
			effective.FileSystemIdentity,
			physical.Indirection
		) {
			LinkTarget = GetLinkTargetValue( physical ),
			ReparseTag = GetReparseTagValue( physical ),
			LinkIdentity = GetLinkIdentityValue( physical ),
			Size = statistics.Size >= 0
				? FileSystemMetadataValue<ulong>.Available( checked( (ulong)statistics.Size ) )
				: FileSystemMetadataValue<ulong>.Unavailable( "Darwin reported a negative size." ),
			LinkCount = FileSystemMetadataValue<ulong>.Available( statistics.LinkCount ),
			Mode = FileSystemMetadataValue<uint>.Available( statistics.Mode ),
			UserId = FileSystemMetadataValue<uint>.Available( statistics.UserIdentifier ),
			GroupId = FileSystemMetadataValue<uint>.Available( statistics.GroupIdentifier ),
			OwnerName = FileSystemMetadataValue<string>.Unavailable( "Owner-name resolution is intentionally separate from numeric stat ownership." ),
			GroupName = FileSystemMetadataValue<string>.Unavailable( "Group-name resolution is intentionally separate from numeric stat ownership." ),
			AccessTime = FromUnixTimestamp( statistics.AccessTime.Seconds, statistics.AccessTime.Nanoseconds, "access time" ),
			ModificationTime = FromUnixTimestamp( statistics.ModificationTime.Seconds, statistics.ModificationTime.Nanoseconds, "modification time" ),
			ChangeTime = FromUnixTimestamp( statistics.ChangeTime.Seconds, statistics.ChangeTime.Nanoseconds, "inode-change time" ),
			BirthTime = FromUnixTimestamp( statistics.BirthTime.Seconds, statistics.BirthTime.Nanoseconds, "birth time" ),
			DeviceIdentifier = FileSystemMetadataValue<string>.Available( device.ToString( CultureInfo.InvariantCulture ) ),
			InodeNumber = FileSystemMetadataValue<ulong>.Available( statistics.Inode ),
			SpecialDeviceIdentifier = specialDevice,
			AllocatedBlocks = statistics.Blocks >= 0
				? FileSystemMetadataValue<ulong>.Available( checked( (ulong)statistics.Blocks ) )
				: FileSystemMetadataValue<ulong>.Unavailable( "Darwin reported a negative allocated-block count." ),
			AllocationBlockSize = statistics.Blocks >= 0
				? FileSystemMetadataValue<ulong>.Available( 512 )
				: FileSystemMetadataValue<ulong>.Unavailable( "Darwin did not report allocated blocks." ),
			PreferredIoBlockSize = statistics.BlockSize > 0
				? FileSystemMetadataValue<ulong>.Available( checked( (ulong)statistics.BlockSize ) )
				: FileSystemMetadataValue<ulong>.Unavailable( "Darwin did not report a preferred I/O block size." ),
			Attributes = Capture( () => File.GetAttributes( path ) ),
			TimestampMutationCapabilities = FileSystemMetadataValue<FileTimestampMutationCapabilities>.Available(
				GetTimestampCapabilities()
			)
		};
	}

	private static FileSystemMetadata GetManagedMetadata(
		string path,
		bool followSymbolicLink,
		ReadOnlyFileSystemEntry physical,
		ReadOnlyFileSystemEntry effective
	) {
		var linkObject = (physical.IsPathIndirection || physical.IsReparsePoint)
			&& !effective.WasDereferenced;
		var attributes = Capture( () => File.GetAttributes( path ) );
		var size = FileSystemMetadataValue<ulong>.Unavailable( "The managed fallback did not expose an authoritative size." );
		var accessTime = FileSystemMetadataValue<DateTimeOffset>.Unavailable( "The managed fallback could not obtain access time." );
		var modificationTime = FileSystemMetadataValue<DateTimeOffset>.Unavailable( "The managed fallback could not obtain modification time." );
		var birthTime = FileSystemMetadataValue<DateTimeOffset>.Unsupported( "Birth time is unavailable through this platform adapter." );
		var mode = FileSystemMetadataValue<uint>.Unsupported( "A native mode is unavailable through this platform adapter." );

		if ( !linkObject ) {
			accessTime = Capture( () => new DateTimeOffset( File.GetLastAccessTimeUtc( path ), TimeSpan.Zero ) );
			modificationTime = Capture( () => new DateTimeOffset( File.GetLastWriteTimeUtc( path ), TimeSpan.Zero ) );
			if ( OperatingSystem.IsWindows() ) {
				birthTime = Capture( () => new DateTimeOffset( File.GetCreationTimeUtc( path ), TimeSpan.Zero ) );
			}
			if ( effective.Kind == FileSystemEntryKind.File ) {
				size = CaptureUnsigned( () => new FileInfo( path ).Length );
			}
			if ( !OperatingSystem.IsWindows() ) {
				mode = CaptureUnixFileMode( path );
			}
		}

		return new FileSystemMetadata(
			path,
			effective.Kind,
			physical.IsSymbolicLink,
			effective.WasDereferenced,
			effective.EntryIdentity,
			effective.FileSystemIdentity,
			physical.Indirection
		) {
			LinkTarget = GetLinkTargetValue( physical ),
			ReparseTag = GetReparseTagValue( physical ),
			LinkIdentity = GetLinkIdentityValue( physical ),
			Size = size,
			LinkCount = FileSystemMetadataValue<ulong>.Unavailable( "The managed fallback did not expose a link count." ),
			Mode = mode,
			UserId = FileSystemMetadataValue<uint>.Unsupported( "Numeric ownership is unavailable through this platform adapter." ),
			GroupId = FileSystemMetadataValue<uint>.Unsupported( "Numeric group ownership is unavailable through this platform adapter." ),
			OwnerName = FileSystemMetadataValue<string>.Unavailable( "Owner-name resolution is not supplied by the managed fallback." ),
			GroupName = FileSystemMetadataValue<string>.Unavailable( "Group-name resolution is not supplied by the managed fallback." ),
			AccessTime = accessTime,
			ModificationTime = modificationTime,
			ChangeTime = FileSystemMetadataValue<DateTimeOffset>.Unavailable( "The managed fallback did not expose inode-change time." ),
			BirthTime = birthTime,
			DeviceIdentifier = FileSystemMetadataValue<string>.Unavailable( "The managed fallback did not expose a device identifier." ),
			InodeNumber = FileSystemMetadataValue<ulong>.Unavailable( "The managed fallback did not expose an inode or platform object number." ),
			SpecialDeviceIdentifier = effective.Kind is FileSystemEntryKind.BlockDevice or FileSystemEntryKind.CharacterDevice
				? FileSystemMetadataValue<string>.Unavailable( "The managed fallback did not expose a special-device identifier." )
				: FileSystemMetadataValue<string>.NotApplicable(),
			AllocatedBlocks = FileSystemMetadataValue<ulong>.Unavailable( "The managed fallback did not expose allocated blocks." ),
			AllocationBlockSize = FileSystemMetadataValue<ulong>.Unavailable( "The managed fallback did not expose allocated-block size." ),
			PreferredIoBlockSize = FileSystemMetadataValue<ulong>.Unavailable( "The managed fallback did not expose preferred I/O block size." ),
			Attributes = attributes,
			TimestampMutationCapabilities = FileSystemMetadataValue<FileTimestampMutationCapabilities>.Available(
				GetTimestampCapabilities()
			)
		};
	}

	private static PlatformOperationResult SetWindowsTimestamps(
		string path,
		FileTimestampMutationRequest request,
		bool followSymbolicLink
	) {
		var flags = FileFlagBackupSemantics;
		if ( !followSymbolicLink ) {
			flags |= FileFlagOpenReparsePoint;
		}
		using var handle = CreateFileW(
			path,
			FileWriteAttributes,
			FileShare.Read | FileShare.Write | FileShare.Delete,
			IntPtr.Zero,
			OpenExisting,
			flags,
			IntPtr.Zero
		);
		if ( handle.IsInvalid ) {
			var error = new Win32Exception( Marshal.GetLastPInvokeError() );
			return PlatformOperationResult.Failure( error.Message, error );
		}

		var now = DateTimeOffset.UtcNow;
		var creation = AllocateWindowsFileTime( request.BirthTime, now );
		var access = AllocateWindowsFileTime( request.AccessTime, now );
		var modification = AllocateWindowsFileTime( request.ModificationTime, now );
		try {
			if ( !SetFileTime( handle, creation, access, modification ) ) {
				var error = new Win32Exception( Marshal.GetLastPInvokeError() );
				return PlatformOperationResult.Failure( error.Message, error );
			}
			return PlatformOperationResult.Success();
		} finally {
			FreeNativePointer( creation );
			FreeNativePointer( access );
			FreeNativePointer( modification );
		}
	}

	private static PlatformOperationResult SetUnixTimestamps(
		string path,
		FileTimestampMutationRequest request,
		bool followSymbolicLink
	) {
		var isDarwin = OperatingSystem.IsMacOS();
		var timeNow = isDarwin ? DarwinUnixTimeNow : LinuxUnixTimeNow;
		var timeOmit = isDarwin ? DarwinUnixTimeOmit : LinuxUnixTimeOmit;
		var times = new[] {
			ToUnixTimespec( request.AccessTime, timeNow, timeOmit ),
			ToUnixTimespec( request.ModificationTime, timeNow, timeOmit )
		};
		var directoryFileDescriptor = isDarwin
			? DarwinAtFileDescriptorCurrentWorkingDirectory
			: LinuxAtFileDescriptorCurrentWorkingDirectory;
		var flags = followSymbolicLink
			? 0
			: isDarwin
				? DarwinAtSymbolicLinkNoFollow
				: LinuxAtSymbolicLinkNoFollow;
		if ( Utimensat( directoryFileDescriptor, path, times, flags ) != 0 ) {
			var error = new Win32Exception( Marshal.GetLastPInvokeError() );
			return PlatformOperationResult.Failure( error.Message, error );
		}
		return PlatformOperationResult.Success();
	}

	private static PlatformOperationResult SetManagedTimestamps(
		string path,
		FileTimestampMutationRequest request,
		ReadOnlyFileSystemEntry physical,
		bool followSymbolicLink
	) {
		if (
			(physical.IsPathIndirection || physical.IsReparsePoint)
			&& (!followSymbolicLink || !physical.Indirection.CanResolveAsPath)
		) {
			return PlatformOperationResult.Unsupported(
				"The managed timestamp adapter cannot safely select this reparse object without native no-follow support."
			);
		}
		var now = DateTimeOffset.UtcNow;
		var isDirectory = physical.Kind == FileSystemEntryKind.Directory
			|| (physical.Indirection.CanResolveAsPath && Directory.Exists( path ));
		ApplyManagedTime(
			request.AccessTime,
			now,
			value => {
				if ( isDirectory ) {
					Directory.SetLastAccessTimeUtc( path, value.UtcDateTime );
				} else {
					File.SetLastAccessTimeUtc( path, value.UtcDateTime );
				}
			}
		);
		ApplyManagedTime(
			request.ModificationTime,
			now,
			value => {
				if ( isDirectory ) {
					Directory.SetLastWriteTimeUtc( path, value.UtcDateTime );
				} else {
					File.SetLastWriteTimeUtc( path, value.UtcDateTime );
				}
			}
		);
		ApplyManagedTime(
			request.BirthTime,
			now,
			value => {
				if ( isDirectory ) {
					Directory.SetCreationTimeUtc( path, value.UtcDateTime );
				} else {
					File.SetCreationTimeUtc( path, value.UtcDateTime );
				}
			}
		);
		return PlatformOperationResult.Success();
	}

	private static string? GetUnsupportedTimestampRequest(
		FileTimestampMutationRequest request,
		ReadOnlyFileSystemEntry physical,
		bool followSymbolicLink,
		FileTimestampMutationCapabilities capabilities
	) {
		if (
			request.AccessTime.Kind != FileTimestampChangeKind.Unchanged
			&& (capabilities & FileTimestampMutationCapabilities.AccessTime) == 0
		) {
			return "Access-time mutation is unsupported on this platform.";
		}
		if (
			request.ModificationTime.Kind != FileTimestampChangeKind.Unchanged
			&& (capabilities & FileTimestampMutationCapabilities.ModificationTime) == 0
		) {
			return "Modification-time mutation is unsupported on this platform.";
		}
		if (
			request.BirthTime.Kind != FileTimestampChangeKind.Unchanged
			&& (capabilities & FileTimestampMutationCapabilities.BirthTime) == 0
		) {
			return "Birth- or creation-time mutation is unsupported on this platform.";
		}
		if (
			followSymbolicLink
			&& !physical.Indirection.CanResolveAsPath
			&& (
				physical.IsPathIndirection
				|| (
					physical.IsReparsePoint
					&& physical.Indirection.Kind == PathIndirectionKind.Unknown
				)
			)
		) {
			return "The requested reparse point does not expose a supported pathname target.";
		}
		if (
			(physical.IsPathIndirection || physical.IsReparsePoint)
			&& !followSymbolicLink
			&& (capabilities & FileTimestampMutationCapabilities.NoFollowSymbolicLink) == 0
		) {
			return "Timestamp mutation of a pathname-indirection or reparse object without dereferencing it is unsupported on this platform.";
		}
		return null;
	}

	private static FileTimestampMutationCapabilities GetTimestampCapabilities() {
		if ( OperatingSystem.IsWindows() ) {
			return FileTimestampMutationCapabilities.AccessTime
				| FileTimestampMutationCapabilities.ModificationTime
				| FileTimestampMutationCapabilities.BirthTime
				| FileTimestampMutationCapabilities.NoFollowSymbolicLink;
		}
		if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ) {
			return FileTimestampMutationCapabilities.AccessTime
				| FileTimestampMutationCapabilities.ModificationTime
				| FileTimestampMutationCapabilities.NoFollowSymbolicLink;
		}
		return FileTimestampMutationCapabilities.AccessTime
			| FileTimestampMutationCapabilities.ModificationTime;
	}

	private static NativeFileSystemDetails TryGetNativeFileSystemDetails( string path ) {
		try {
			if ( OperatingSystem.IsWindows() ) {
				return TryGetWindowsFileSystemDetails( path );
			}
			if ( OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ) {
				return TryGetPosixFileSystemDetails( path );
			}
		} catch ( Exception exception ) when (
			exception is DllNotFoundException
				or EntryPointNotFoundException
				or BadImageFormatException
				or IOException
				or UnauthorizedAccessException
				or ArgumentException
		) {
			return NativeFileSystemDetails.Unavailable( exception.Message );
		}
		return NativeFileSystemDetails.Unavailable( "No native filesystem-information adapter is available." );
	}

	private static NativeFileSystemDetails TryGetWindowsFileSystemDetails( string path ) {
		var root = Path.GetPathRoot( Path.GetFullPath( path ) );
		if ( string.IsNullOrEmpty( root ) ) {
			return NativeFileSystemDetails.Unavailable( "The volume root could not be determined." );
		}
		if ( !GetDiskFreeSpaceW( root, out var sectorsPerCluster, out var bytesPerSector, out _, out _ ) ) {
			return NativeFileSystemDetails.Unavailable( new Win32Exception( Marshal.GetLastPInvokeError() ).Message );
		}
		var volumeName = new StringBuilder( 261 );
		var fileSystemName = new StringBuilder( 261 );
		if (
			!GetVolumeInformationW(
				root,
				volumeName,
				volumeName.Capacity,
				out _,
				out var maximumComponentLength,
				out var flags,
				fileSystemName,
				fileSystemName.Capacity
			)
		) {
			return NativeFileSystemDetails.Unavailable( new Win32Exception( Marshal.GetLastPInvokeError() ).Message );
		}
		var allocationUnit = checked( (ulong)sectorsPerCluster * bytesPerSector );
		return new NativeFileSystemDetails {
			MountPoint = FileSystemMetadataValue<string>.Available( root ),
			FileSystemType = FileSystemMetadataValue<string>.Available( fileSystemName.ToString() ),
			VolumeName = FileSystemMetadataValue<string>.Available( volumeName.ToString() ),
			BlockSize = FileSystemMetadataValue<ulong>.Available( bytesPerSector ),
			FragmentSize = FileSystemMetadataValue<ulong>.Available( allocationUnit ),
			MaximumNameLength = FileSystemMetadataValue<ulong>.Available( maximumComponentLength ),
			IsReadOnly = FileSystemMetadataValue<bool>.Available( (flags & FileReadOnlyVolume) != 0 )
		};
	}

	private static NativeFileSystemDetails TryGetPosixFileSystemDetails( string path ) {
		if ( StatVfs( path, out var statistics ) != 0 ) {
			return NativeFileSystemDetails.Unavailable( new Win32Exception( Marshal.GetLastPInvokeError() ).Message );
		}
		var fragmentSize = statistics.FragmentSize == 0 ? statistics.BlockSize : statistics.FragmentSize;
		return new NativeFileSystemDetails {
			TotalBytes = MultiplyMetadata( statistics.Blocks, fragmentSize, "filesystem capacity" ),
			FreeBytes = MultiplyMetadata( statistics.BlocksFree, fragmentSize, "filesystem free capacity" ),
			AvailableBytes = MultiplyMetadata( statistics.BlocksAvailable, fragmentSize, "filesystem available capacity" ),
			BlockSize = FileSystemMetadataValue<ulong>.Available( statistics.BlockSize ),
			FragmentSize = FileSystemMetadataValue<ulong>.Available( fragmentSize ),
			MaximumNameLength = FileSystemMetadataValue<ulong>.Available( statistics.MaximumNameLength ),
			IsReadOnly = FileSystemMetadataValue<bool>.Available(
				(statistics.Flags & PosixReadOnlyFileSystem) != 0
			)
		};
	}

	private static DriveInfo? FindContainingDrive( string fullPath ) {
		var comparison = OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;
		DriveInfo? best = null;
		var bestLength = -1;
		foreach ( var drive in DriveInfo.GetDrives() ) {
			string root;
			try {
				root = Path.GetFullPath( drive.RootDirectory.FullName );
			} catch ( Exception exception ) when (
				exception is IOException
					or UnauthorizedAccessException
					or ArgumentException
					or NotSupportedException
			) {
				continue;
			}
			if ( !IsWithinRoot( fullPath, root, comparison ) || root.Length <= bestLength ) {
				continue;
			}
			best = drive;
			bestLength = root.Length;
		}
		return best;
	}

	private static bool IsWithinRoot( string path, string root, StringComparison comparison ) {
		if ( path.Equals( root, comparison ) ) {
			return true;
		}
		var rootWithSeparator = Path.EndsInDirectorySeparator( root )
			? root
			: string.Concat( root, Path.DirectorySeparatorChar );
		return path.StartsWith( rootWithSeparator, comparison );
	}

	private static FileSystemMetadataValue<string> GetLinkTargetValue( ReadOnlyFileSystemEntry physical ) {
		if ( !physical.IsPathIndirection ) {
			return FileSystemMetadataValue<string>.NotApplicable();
		}
		return physical.LinkTarget is null
			? FileSystemMetadataValue<string>.Unavailable( "The provider could not obtain the immediate link target." )
			: FileSystemMetadataValue<string>.Available( physical.LinkTarget );
	}

	private static FileSystemMetadataValue<uint> GetReparseTagValue( ReadOnlyFileSystemEntry physical ) {
		if ( !physical.IsReparsePoint ) {
			return FileSystemMetadataValue<uint>.NotApplicable();
		}
		return physical.Indirection.ReparseTag is uint tag
			? FileSystemMetadataValue<uint>.Available( tag )
			: FileSystemMetadataValue<uint>.Unavailable(
				"The Windows reparse tag could not be obtained."
			);
	}

	private static FileSystemMetadataValue<FileSystemEntryIdentity> GetLinkIdentityValue(
		ReadOnlyFileSystemEntry physical
	) {
		if ( !physical.IsPathIndirection && !physical.IsReparsePoint ) {
			return FileSystemMetadataValue<FileSystemEntryIdentity>.NotApplicable();
		}
		return physical.EntryIdentity.IsAvailable
			? FileSystemMetadataValue<FileSystemEntryIdentity>.Available( physical.EntryIdentity )
			: FileSystemMetadataValue<FileSystemEntryIdentity>.Unavailable(
				"The E1 provider could not obtain the link-object identity."
			);
	}

	private static FileSystemMetadataValue<ulong> FromStatxUnsigned(
		uint mask,
		uint requiredMask,
		ulong value,
		string name
	) => (mask & requiredMask) == requiredMask
		? FileSystemMetadataValue<ulong>.Available( value )
		: FileSystemMetadataValue<ulong>.Unavailable( string.Concat( "statx did not report ", name, "." ) );

	private static FileSystemMetadataValue<uint> FromStatxUnsigned32(
		uint mask,
		uint requiredMask,
		uint value,
		string name
	) => (mask & requiredMask) == requiredMask
		? FileSystemMetadataValue<uint>.Available( value )
		: FileSystemMetadataValue<uint>.Unavailable( string.Concat( "statx did not report ", name, "." ) );

	private static FileSystemMetadataValue<DateTimeOffset> FromStatxTimestamp(
		uint mask,
		uint requiredMask,
		LinuxStatxTimestamp value,
		string name
	) => (mask & requiredMask) == requiredMask
		? FromUnixTimestamp( value.Seconds, value.Nanoseconds, name )
		: FileSystemMetadataValue<DateTimeOffset>.Unavailable(
			string.Concat( "statx did not report ", name, "." )
		);

	private static FileSystemMetadataValue<DateTimeOffset> FromUnixTimestamp(
		long seconds,
		long nanoseconds,
		string name
	) {
		if ( nanoseconds is < 0 or >= 1_000_000_000 ) {
			return FileSystemMetadataValue<DateTimeOffset>.Unavailable(
				string.Concat( "The native ", name, " contained invalid nanoseconds." )
			);
		}
		try {
			return FileSystemMetadataValue<DateTimeOffset>.Available(
				DateTimeOffset.FromUnixTimeSeconds( seconds ).AddTicks( nanoseconds / 100 )
			);
		} catch ( ArgumentOutOfRangeException ) {
			return FileSystemMetadataValue<DateTimeOffset>.Unavailable(
				string.Concat( "The native ", name, " is outside DateTimeOffset range." )
			);
		}
	}

	private static FileSystemMetadataValue<DateTimeOffset> FromWindowsFileTime( long value ) {
		try {
			return FileSystemMetadataValue<DateTimeOffset>.Available(
				new DateTimeOffset( DateTime.FromFileTimeUtc( value ), TimeSpan.Zero )
			);
		} catch ( ArgumentOutOfRangeException ) {
			return FileSystemMetadataValue<DateTimeOffset>.Unavailable(
				"The Windows timestamp is outside DateTimeOffset range."
			);
		}
	}

	private static FileSystemMetadataValue<DateTimeOffset> FromWindowsFileTime(
		System.Runtime.InteropServices.ComTypes.FILETIME value
	) {
		var combined = ((long)(uint)value.dwHighDateTime << 32) | (uint)value.dwLowDateTime;
		return FromWindowsFileTime( combined );
	}

	private static IntPtr AllocateWindowsFileTime( FileTimestampChange change, DateTimeOffset now ) {
		if ( change.Kind == FileTimestampChangeKind.Unchanged ) {
			return IntPtr.Zero;
		}
		var value = ResolveTimestampChange( change, now ).UtcDateTime.ToFileTimeUtc();
		var pointer = Marshal.AllocHGlobal( sizeof( long ) );
		Marshal.WriteInt64( pointer, value );
		return pointer;
	}

	private static void FreeNativePointer( IntPtr pointer ) {
		if ( pointer != IntPtr.Zero ) {
			Marshal.FreeHGlobal( pointer );
		}
	}

	private static UnixTimespec ToUnixTimespec(
		FileTimestampChange change,
		long timeNow,
		long timeOmit
	) {
		if ( change.Kind == FileTimestampChangeKind.Unchanged ) {
			return new UnixTimespec { Seconds = 0, Nanoseconds = timeOmit };
		}
		if ( change.Kind == FileTimestampChangeKind.CurrentTime ) {
			return new UnixTimespec { Seconds = 0, Nanoseconds = timeNow };
		}
		var value = change.Value ?? throw new InvalidOperationException( "An explicit timestamp requires a value." );
		var ticks = checked( (value.ToUniversalTime() - DateTimeOffset.UnixEpoch).Ticks );
		var seconds = Math.DivRem( ticks, TimeSpan.TicksPerSecond, out var remainder );
		if ( remainder < 0 ) {
			seconds--;
			remainder += TimeSpan.TicksPerSecond;
		}
		return new UnixTimespec {
			Seconds = seconds,
			Nanoseconds = checked( remainder * 100 )
		};
	}

	private static DateTimeOffset ResolveTimestampChange( FileTimestampChange change, DateTimeOffset now ) => change.Kind switch {
		FileTimestampChangeKind.CurrentTime => now,
		FileTimestampChangeKind.Explicit => change.Value
			?? throw new InvalidOperationException( "An explicit timestamp requires a value." ),
		_ => throw new InvalidOperationException( "An unchanged timestamp does not have a replacement value." )
	};

	private static void ApplyManagedTime(
		FileTimestampChange change,
		DateTimeOffset now,
		Action<DateTimeOffset> apply
	) {
		if ( change.Kind != FileTimestampChangeKind.Unchanged ) {
			apply( ResolveTimestampChange( change, now ) );
		}
	}

	[System.Runtime.Versioning.UnsupportedOSPlatform( "windows" )]
	private static FileSystemMetadataValue<uint> CaptureUnixFileMode( string path ) {
		return Capture( () => checked( (uint)File.GetUnixFileMode( path ) ) );
	}

	private static FileSystemMetadataValue<T> Capture<T>( Func<T> valueFactory ) {
		try {
			return FileSystemMetadataValue<T>.Available( valueFactory() );
		} catch ( Exception exception ) when (
			exception is IOException
				or UnauthorizedAccessException
				or System.Security.SecurityException
				or ArgumentException
				or NotSupportedException
				or InvalidOperationException
		) {
			return FileSystemMetadataValue<T>.Unavailable( exception.Message );
		}
	}

	private static FileSystemMetadataValue<ulong> CaptureUnsigned( Func<long> valueFactory ) {
		try {
			var value = valueFactory();
			return value >= 0
				? FileSystemMetadataValue<ulong>.Available( checked( (ulong)value ) )
				: FileSystemMetadataValue<ulong>.Unavailable( "The host reported a negative value." );
		} catch ( Exception exception ) when (
			exception is IOException
				or UnauthorizedAccessException
				or System.Security.SecurityException
				or ArgumentException
				or NotSupportedException
				or InvalidOperationException
				or OverflowException
		) {
			return FileSystemMetadataValue<ulong>.Unavailable( exception.Message );
		}
	}

	private static FileSystemMetadataValue<T> Prefer<T>(
		FileSystemMetadataValue<T> preferred,
		FileSystemMetadataValue<T> fallback
	) => preferred.IsAvailable ? preferred : fallback;

	private static FileSystemMetadataValue<ulong> MultiplyMetadata(
		ulong left,
		ulong right,
		string name
	) {
		try {
			return FileSystemMetadataValue<ulong>.Available( checked( left * right ) );
		} catch ( OverflowException ) {
			return FileSystemMetadataValue<ulong>.Unavailable(
				string.Concat( "The reported ", name, " exceeds UInt64." )
			);
		}
	}

	private static ulong DivideRoundUp( ulong value, ulong divisor ) => value == 0
		? 0
		: checked( ((value - 1) / divisor) + 1 );

	private static int InvokeDarwinStat(
		string path,
		bool followSymbolicLink,
		out DarwinStatStructure statistics
	) {
		try {
			return followSymbolicLink
				? DarwinStatInode64( path, out statistics )
				: DarwinLStatInode64( path, out statistics );
		} catch ( EntryPointNotFoundException ) {
			return followSymbolicLink
				? DarwinStat64Only( path, out statistics )
				: DarwinLStat64Only( path, out statistics );
		}
	}

	[DllImport(
		"kernel32.dll",
		EntryPoint = "CreateFileW",
		CharSet = CharSet.Unicode,
		ExactSpelling = true,
		SetLastError = true
	)]
	private static extern SafeFileHandle CreateFileW(
		string fileName,
		uint desiredAccess,
		FileShare shareMode,
		IntPtr securityAttributes,
		uint creationDisposition,
		uint flagsAndAttributes,
		IntPtr templateFile
	);

	[DllImport( "kernel32.dll", ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool GetFileInformationByHandle(
		SafeFileHandle file,
		out ByHandleFileInformation information
	);

	[DllImport( "kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool GetFileBasicInformationByHandle(
		SafeFileHandle file,
		FileInformationClass informationClass,
		out FileBasicInformation information,
		uint bufferSize
	);

	[DllImport( "kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool GetFileStandardInformationByHandle(
		SafeFileHandle file,
		FileInformationClass informationClass,
		out FileStandardInformation information,
		uint bufferSize
	);

	[DllImport( "kernel32.dll", ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool SetFileTime(
		SafeFileHandle file,
		IntPtr creationTime,
		IntPtr lastAccessTime,
		IntPtr lastWriteTime
	);

	[DllImport( "kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool GetDiskFreeSpaceW(
		string rootPathName,
		out uint sectorsPerCluster,
		out uint bytesPerSector,
		out uint numberOfFreeClusters,
		out uint totalNumberOfClusters
	);

	[DllImport( "kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool GetVolumeInformationW(
		string rootPathName,
		StringBuilder volumeNameBuffer,
		int volumeNameSize,
		out uint volumeSerialNumber,
		out uint maximumComponentLength,
		out uint fileSystemFlags,
		StringBuilder fileSystemNameBuffer,
		int fileSystemNameSize
	);

	[DllImport( "advapi32.dll", ExactSpelling = true )]
	private static extern uint GetSecurityInfo(
		SafeFileHandle handle,
		SecurityObjectType objectType,
		uint securityInformation,
		out IntPtr ownerSid,
		out IntPtr groupSid,
		IntPtr discretionaryAccessControlList,
		IntPtr systemAccessControlList,
		out IntPtr securityDescriptor
	);

	[DllImport( "advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool LookupAccountSidW(
		string? systemName,
		IntPtr sid,
		StringBuilder? name,
		ref uint nameLength,
		StringBuilder? referencedDomainName,
		ref uint referencedDomainNameLength,
		out SidNameUse use
	);

	[DllImport( "advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool ConvertSidToStringSidW( IntPtr sid, out IntPtr stringSid );

	[DllImport( "kernel32.dll", ExactSpelling = true )]
	private static extern IntPtr LocalFree( IntPtr memory );

	[DllImport( "libc", EntryPoint = "statx", SetLastError = true )]
	private static extern int Statx(
		int directoryFileDescriptor,
		string path,
		int flags,
		uint mask,
		out LinuxStatx statistics
	);

	[DllImport( "libc", EntryPoint = "utimensat", SetLastError = true )]
	private static extern int Utimensat(
		int directoryFileDescriptor,
		string path,
		[In] UnixTimespec[] times,
		int flags
	);

	[DllImport( "libc", EntryPoint = "statvfs", SetLastError = true )]
	private static extern int StatVfs( string path, out PosixStatVfs statistics );

	[DllImport( "libc", EntryPoint = "stat$INODE64", SetLastError = true )]
	private static extern int DarwinStatInode64( string path, out DarwinStatStructure statistics );

	[DllImport( "libc", EntryPoint = "lstat$INODE64", SetLastError = true )]
	private static extern int DarwinLStatInode64( string path, out DarwinStatStructure statistics );

	[DllImport( "libc", EntryPoint = "stat", SetLastError = true )]
	private static extern int DarwinStat64Only( string path, out DarwinStatStructure statistics );

	[DllImport( "libc", EntryPoint = "lstat", SetLastError = true )]
	private static extern int DarwinLStat64Only( string path, out DarwinStatStructure statistics );

#pragma warning disable CS0169, CS0649 // Native interop populates layout fields directly.
	private enum SecurityObjectType {
		File = 1
	}

	private enum SidNameUse {
		User = 1,
		Group = 2,
		Domain = 3,
		Alias = 4,
		WellKnownGroup = 5,
		DeletedAccount = 6,
		Invalid = 7,
		Unknown = 8,
		Computer = 9,
		Label = 10,
		LogonSession = 11
	}

	private enum FileInformationClass {
		Basic = 0,
		Standard = 1
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct ByHandleFileInformation {
		internal FileAttributes FileAttributes;
		internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
		internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
		internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
		internal uint VolumeSerialNumber;
		internal uint FileSizeHigh;
		internal uint FileSizeLow;
		internal uint NumberOfLinks;
		internal uint FileIndexHigh;
		internal uint FileIndexLow;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct FileBasicInformation {
		internal long CreationTime;
		internal long LastAccessTime;
		internal long LastWriteTime;
		internal long ChangeTime;
		internal FileAttributes FileAttributes;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct FileStandardInformation {
		internal long AllocationSize;
		internal long EndOfFile;
		internal uint NumberOfLinks;
		internal byte DeletePending;
		internal byte Directory;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct LinuxStatxTimestamp {
		internal long Seconds;
		internal uint Nanoseconds;
		internal int Reserved;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct LinuxStatx {
		internal uint Mask;
		internal uint BlockSize;
		internal ulong Attributes;
		internal uint LinkCount;
		internal uint UserIdentifier;
		internal uint GroupIdentifier;
		internal ushort Mode;
		internal ushort Spare0;
		internal ulong Inode;
		internal ulong Size;
		internal ulong Blocks;
		internal ulong AttributesMask;
		internal LinuxStatxTimestamp AccessTime;
		internal LinuxStatxTimestamp BirthTime;
		internal LinuxStatxTimestamp ChangeTime;
		internal LinuxStatxTimestamp ModificationTime;
		internal uint DeviceSpecialMajor;
		internal uint DeviceSpecialMinor;
		internal uint DeviceMajor;
		internal uint DeviceMinor;
		internal ulong MountIdentifier;
		internal uint DirectIoMemoryAlignment;
		internal uint DirectIoOffsetAlignment;
		internal ulong Spare3_0;
		internal ulong Spare3_1;
		internal ulong Spare3_2;
		internal ulong Spare3_3;
		internal ulong Spare3_4;
		internal ulong Spare3_5;
		internal ulong Spare3_6;
		internal ulong Spare3_7;
		internal ulong Spare3_8;
		internal ulong Spare3_9;
		internal ulong Spare3_10;
		internal ulong Spare3_11;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct UnixTimespec {
		internal long Seconds;
		internal long Nanoseconds;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct PosixStatVfs {
		internal ulong BlockSize;
		internal ulong FragmentSize;
		internal ulong Blocks;
		internal ulong BlocksFree;
		internal ulong BlocksAvailable;
		internal ulong Files;
		internal ulong FilesFree;
		internal ulong FilesAvailable;
		internal ulong FileSystemIdentifier;
		internal ulong Flags;
		internal ulong MaximumNameLength;
		internal int Spare0;
		internal int Spare1;
		internal int Spare2;
		internal int Spare3;
		internal int Spare4;
		internal int Spare5;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct DarwinTimespec {
		internal long Seconds;
		internal long Nanoseconds;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct DarwinStatStructure {
		internal int Device;
		internal ushort Mode;
		internal ushort LinkCount;
		internal ulong Inode;
		internal uint UserIdentifier;
		internal uint GroupIdentifier;
		internal int SpecialDevice;
		internal DarwinTimespec AccessTime;
		internal DarwinTimespec ModificationTime;
		internal DarwinTimespec ChangeTime;
		internal DarwinTimespec BirthTime;
		internal long Size;
		internal long Blocks;
		internal int BlockSize;
		internal uint Flags;
		internal uint Generation;
		internal int Spare;
		internal long QuadSpare0;
		internal long QuadSpare1;
	}
#pragma warning restore CS0169, CS0649

	private readonly record struct NativeFileSystemDetails {
		/// <summary>Gets the native mount point or volume root.</summary>
		public FileSystemMetadataValue<string> MountPoint { get; init; }

		/// <summary>Gets the native filesystem type.</summary>
		public FileSystemMetadataValue<string> FileSystemType { get; init; }

		/// <summary>Gets the native volume name.</summary>
		public FileSystemMetadataValue<string> VolumeName { get; init; }

		/// <summary>Gets total bytes reported by the native adapter.</summary>
		public FileSystemMetadataValue<ulong> TotalBytes { get; init; }

		/// <summary>Gets free bytes reported by the native adapter.</summary>
		public FileSystemMetadataValue<ulong> FreeBytes { get; init; }

		/// <summary>Gets caller-available bytes reported by the native adapter.</summary>
		public FileSystemMetadataValue<ulong> AvailableBytes { get; init; }

		/// <summary>Gets the native filesystem block size.</summary>
		public FileSystemMetadataValue<ulong> BlockSize { get; init; }

		/// <summary>Gets the native fragment or allocation-unit size.</summary>
		public FileSystemMetadataValue<ulong> FragmentSize { get; init; }

		/// <summary>Gets the native maximum component-name length.</summary>
		public FileSystemMetadataValue<ulong> MaximumNameLength { get; init; }

		/// <summary>Gets the native read-only state.</summary>
		public FileSystemMetadataValue<bool> IsReadOnly { get; init; }

		/// <summary>Creates a native-detail result whose fields are unavailable.</summary>
		/// <param name="message">The unavailability explanation.</param>
		/// <returns>The unavailable native details.</returns>
		public static NativeFileSystemDetails Unavailable( string message ) => new() {
			MountPoint = FileSystemMetadataValue<string>.Unavailable( message ),
			FileSystemType = FileSystemMetadataValue<string>.Unavailable( message ),
			VolumeName = FileSystemMetadataValue<string>.Unavailable( message ),
			TotalBytes = FileSystemMetadataValue<ulong>.Unavailable( message ),
			FreeBytes = FileSystemMetadataValue<ulong>.Unavailable( message ),
			AvailableBytes = FileSystemMetadataValue<ulong>.Unavailable( message ),
			BlockSize = FileSystemMetadataValue<ulong>.Unavailable( message ),
			FragmentSize = FileSystemMetadataValue<ulong>.Unavailable( message ),
			MaximumNameLength = FileSystemMetadataValue<ulong>.Unavailable( message ),
			IsReadOnly = FileSystemMetadataValue<bool>.Unavailable( message )
		};
	}
}
