extern alias IcodPath;

using IPathIndirectionInspector = IcodPath::Icod.Path.IPathIndirectionInspector;
using PathIndirectionInfo = IcodPath::Icod.Path.PathIndirectionInfo;
using PathIndirectionKind = IcodPath::Icod.Path.PathIndirectionKind;
using SystemPathIndirectionInspector = IcodPath::Icod.Path.SystemPathIndirectionInspector;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Provides one-level read-only observation through the host filesystem APIs.
/// </summary>
public sealed class SystemReadOnlyFileSystemProvider : IReadOnlyFileSystemProvider {
	private const int AtFileDescriptorCurrentWorkingDirectory = -100;
	private const int AtSymbolicLinkNoFollow = 0x100;
	private const uint StatxBasicStatistics = 0x000007ff;
	private const uint StatxMountIdentifier = 0x00001000;
	private const ushort FileTypeMask = 0xf000;
	private const ushort FileTypeFifo = 0x1000;
	private const ushort FileTypeCharacterDevice = 0x2000;
	private const ushort FileTypeDirectory = 0x4000;
	private const ushort FileTypeBlockDevice = 0x6000;
	private const ushort FileTypeRegular = 0x8000;
	private const ushort FileTypeSymbolicLink = 0xa000;
	private const ushort FileTypeSocket = 0xc000;
	private const uint FileFlagBackupSemantics = 0x02000000;
	private const uint FileFlagOpenReparsePoint = 0x00200000;
	private const uint OpenExisting = 3;

	private readonly IPathIndirectionInspector indirectionInspector;

	/// <summary>
	/// Gets the shared system provider.
	/// </summary>
	public static SystemReadOnlyFileSystemProvider Instance { get; } = new(
		SystemPathIndirectionInspector.Instance
	);

	/// <summary>Initializes a host provider over an injectable no-follow indirection inspector.</summary>
	/// <param name="indirectionInspector">The physical pathname-indirection inspector.</param>
	public SystemReadOnlyFileSystemProvider( IPathIndirectionInspector indirectionInspector ) {
		this.indirectionInspector = indirectionInspector
			?? throw new ArgumentNullException( nameof( indirectionInspector ) );
	}

	/// <inheritdoc/>
	public async ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
		string path,
		bool followSymbolicLink,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		cancellationToken.ThrowIfCancellationRequested();

		var physicalNative = TryGetNativeObservation( path, false );
		FileAttributes? attributes = null;
		Exception? attributeException = null;
		try {
			attributes = File.GetAttributes( path );
		} catch ( Exception exception ) when (
			exception is IOException
				or UnauthorizedAccessException
				or System.Security.SecurityException
				or NotSupportedException
				or ArgumentException
		) {
			attributeException = exception;
		}
		if ( physicalNative.Kind == FileSystemEntryKind.Unknown && attributes is null ) {
			throw attributeException ?? new IOException( "The filesystem entry could not be observed." );
		}

		var indirection = await indirectionInspector.InspectAsync(
			path,
			cancellationToken
		).ConfigureAwait( false );
		if (
			physicalNative.Kind == FileSystemEntryKind.SymbolicLink
			&& !indirection.IsPathIndirection
		) {
			indirection = PathIndirectionInfo.PosixSymbolicLink(
				indirection.Target,
				false,
				attributes ?? default
			);
		}
		var shouldDereference = followSymbolicLink && indirection.CanResolveAsPath;
		var native = shouldDereference
			? TryGetNativeObservation( path, true )
			: physicalNative;
		if (
			shouldDereference
			&& (
				(
					physicalNative.Kind != FileSystemEntryKind.Unknown
					&& native.Kind == FileSystemEntryKind.Unknown
				)
				|| (
					!Directory.Exists( path )
					&& !File.Exists( path )
				)
			)
		) {
			throw new FileNotFoundException( "The pathname-indirection target does not exist.", path );
		}

		var kind = !shouldDereference && indirection.IsSymbolicLink
			? FileSystemEntryKind.SymbolicLink
			: !shouldDereference && indirection.IsNameSurrogate
				? FileSystemEntryKind.NameSurrogate
				: !shouldDereference
					&& indirection.IsReparsePoint
					&& indirection.Kind == PathIndirectionKind.Unknown
						? FileSystemEntryKind.ReparsePoint
						: native.Kind != FileSystemEntryKind.Unknown
							? native.Kind
							: !shouldDereference && indirection.IsReparsePoint
								? FileSystemEntryKind.ReparsePoint
								: ClassifyUsingManagedApis(
									path,
									attributes,
									indirection.IsPathIndirection,
									shouldDereference
								);

		return new ReadOnlyFileSystemEntry(
			path,
			GetName( path ),
			kind,
			indirection.IsSymbolicLink,
			shouldDereference,
			indirection.Target,
			native.EntryIdentity,
			native.FileSystemIdentity,
			indirection
		);
	}

	/// <inheritdoc/>
	public ValueTask<ReadOnlyFileSystemEntry> ObserveAsync(
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken = default
	) {
		if ( !Enum.IsDefined( typeof( PathDereferenceMode ), dereferenceMode ) ) {
			throw new ArgumentOutOfRangeException( nameof( dereferenceMode ) );
		}
		return ObserveAsync(
			path,
			dereferenceMode == PathDereferenceMode.FollowEligiblePathIndirection,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<ReadOnlyDirectoryEntry> EnumerateDirectoryAsync(
		string directoryPath,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrEmpty( directoryPath );
		cancellationToken.ThrowIfCancellationRequested();
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();
		foreach ( var path in Directory.EnumerateFileSystemEntries( directoryPath ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			yield return new ReadOnlyDirectoryEntry( GetName( path ), path );
		}
	}

	private static FileSystemEntryKind ClassifyUsingManagedApis(
		string path,
		FileAttributes? attributes,
		bool isLink,
		bool followSymbolicLink
	) {
		if ( isLink && followSymbolicLink ) {
			if ( Directory.Exists( path ) ) {
				return FileSystemEntryKind.Directory;
			}
			if ( File.Exists( path ) ) {
				return FileSystemEntryKind.File;
			}
			return FileSystemEntryKind.Unknown;
		}
		if ( attributes is FileAttributes value && (value & FileAttributes.Device) != 0 ) {
			return FileSystemEntryKind.Other;
		}
		return attributes is FileAttributes directoryValue && (directoryValue & FileAttributes.Directory) != 0
			? FileSystemEntryKind.Directory
			: FileSystemEntryKind.File;
	}

	private static string GetName( string path ) {
		var trimmed = Path.TrimEndingDirectorySeparator( path );
		var name = Path.GetFileName( trimmed );
		return name.Length > 0 ? name : trimmed;
	}

	private static NativeObservation TryGetNativeObservation( string path, bool followSymbolicLink ) {
		try {
			if ( OperatingSystem.IsWindows() ) {
				return TryGetWindowsObservation( path, followSymbolicLink );
			}
			if ( OperatingSystem.IsLinux() ) {
				return TryGetLinuxObservation( path, followSymbolicLink );
			}
			if ( OperatingSystem.IsMacOS() ) {
				return TryGetDarwinObservation( path, followSymbolicLink );
			}
		} catch ( Exception exception ) when (
			exception is DllNotFoundException
				or EntryPointNotFoundException
				or BadImageFormatException
		) {
			return NativeObservation.Unavailable;
		}
		return NativeObservation.Unavailable;
	}

	private static NativeObservation TryGetWindowsObservation( string path, bool followSymbolicLink ) {
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
			return NativeObservation.Unavailable;
		}

		var fileIndex = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
		var entryIdentity = new FileSystemEntryIdentity(
			"windows-file-id",
			string.Concat(
				information.VolumeSerialNumber.ToString( "x8", System.Globalization.CultureInfo.InvariantCulture ),
				"-",
				fileIndex.ToString( "x16", System.Globalization.CultureInfo.InvariantCulture )
			)
		);
		var fileSystemIdentity = new FileSystemIdentity(
			"windows-volume",
			information.VolumeSerialNumber.ToString( "x8", System.Globalization.CultureInfo.InvariantCulture )
		);
		var kind = (information.FileAttributes & FileAttributes.Device) != 0
			? FileSystemEntryKind.Other
			: (information.FileAttributes & FileAttributes.Directory) != 0
				? FileSystemEntryKind.Directory
				: FileSystemEntryKind.File;
		return new NativeObservation( entryIdentity, fileSystemIdentity, kind );
	}

	private static NativeObservation TryGetLinuxObservation( string path, bool followSymbolicLink ) {
		var flags = followSymbolicLink ? 0 : AtSymbolicLinkNoFollow;
		if (
			Statx(
				AtFileDescriptorCurrentWorkingDirectory,
				path,
				flags,
				StatxBasicStatistics | StatxMountIdentifier,
				out var statistics
			) != 0
		) {
			return NativeObservation.Unavailable;
		}

		var entryIdentity = new FileSystemEntryIdentity(
			"linux-statx",
			string.Concat(
				statistics.DeviceMajor.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				":",
				statistics.DeviceMinor.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				":",
				statistics.Inode.ToString( System.Globalization.CultureInfo.InvariantCulture )
			)
		);
		var hasMountIdentifier = (statistics.Mask & StatxMountIdentifier) != 0
			&& statistics.MountIdentifier != 0;
		var fileSystemValue = hasMountIdentifier
			? statistics.MountIdentifier.ToString( System.Globalization.CultureInfo.InvariantCulture )
			: string.Concat(
				statistics.DeviceMajor.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				":",
				statistics.DeviceMinor.ToString( System.Globalization.CultureInfo.InvariantCulture )
			);
		return new NativeObservation(
			entryIdentity,
			new FileSystemIdentity(
				hasMountIdentifier ? "linux-mount-id" : "linux-device",
				fileSystemValue
			),
			ClassifyUnixMode( statistics.Mode )
		);
	}

	private static NativeObservation TryGetDarwinObservation( string path, bool followSymbolicLink ) {
		var result = InvokeDarwinStat( path, followSymbolicLink, out var statistics );
		if ( result != 0 ) {
			return NativeObservation.Unavailable;
		}

		var device = unchecked((uint)statistics.Device);
		return new NativeObservation(
			new FileSystemEntryIdentity(
				"darwin-stat",
				string.Concat(
					device.ToString( System.Globalization.CultureInfo.InvariantCulture ),
					":",
					statistics.Inode.ToString( System.Globalization.CultureInfo.InvariantCulture )
				)
			),
			new FileSystemIdentity(
				"darwin-device",
				device.ToString( System.Globalization.CultureInfo.InvariantCulture )
			),
			ClassifyUnixMode( statistics.Mode )
		);
	}

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

	private static FileSystemEntryKind ClassifyUnixMode( ushort mode ) => (ushort)(mode & FileTypeMask) switch {
		FileTypeFifo => FileSystemEntryKind.Fifo,
		FileTypeCharacterDevice => FileSystemEntryKind.CharacterDevice,
		FileTypeDirectory => FileSystemEntryKind.Directory,
		FileTypeBlockDevice => FileSystemEntryKind.BlockDevice,
		FileTypeRegular => FileSystemEntryKind.File,
		FileTypeSymbolicLink => FileSystemEntryKind.SymbolicLink,
		FileTypeSocket => FileSystemEntryKind.Socket,
		_ => FileSystemEntryKind.Other
	};

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

	[DllImport( "libc", EntryPoint = "statx", SetLastError = true )]
	private static extern int Statx(
		int directoryFileDescriptor,
		string path,
		int flags,
		uint mask,
		out LinuxStatx statistics
	);

	[DllImport( "libc", EntryPoint = "stat$INODE64", SetLastError = true )]
	private static extern int DarwinStatInode64( string path, out DarwinStatStructure statistics );

	[DllImport( "libc", EntryPoint = "lstat$INODE64", SetLastError = true )]
	private static extern int DarwinLStatInode64( string path, out DarwinStatStructure statistics );

	[DllImport( "libc", EntryPoint = "stat", SetLastError = true )]
	private static extern int DarwinStat64Only( string path, out DarwinStatStructure statistics );

	[DllImport( "libc", EntryPoint = "lstat", SetLastError = true )]
	private static extern int DarwinLStat64Only( string path, out DarwinStatStructure statistics );

#pragma warning disable CS0169, CS0649 // Native interop populates layout fields directly.
	[StructLayout( LayoutKind.Sequential )]
	private struct ByHandleFileInformation {
		/// <summary>Retains the native <c>FileAttributes</c> layout field.</summary>
		internal FileAttributes FileAttributes;
		/// <summary>Retains the native <c>CreationTime</c> layout field.</summary>
		internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
		/// <summary>Retains the native <c>LastAccessTime</c> layout field.</summary>
		internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
		/// <summary>Retains the native <c>LastWriteTime</c> layout field.</summary>
		internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
		/// <summary>Retains the native <c>VolumeSerialNumber</c> layout field.</summary>
		internal uint VolumeSerialNumber;
		/// <summary>Retains the native <c>FileSizeHigh</c> layout field.</summary>
		internal uint FileSizeHigh;
		/// <summary>Retains the native <c>FileSizeLow</c> layout field.</summary>
		internal uint FileSizeLow;
		/// <summary>Retains the native <c>NumberOfLinks</c> layout field.</summary>
		internal uint NumberOfLinks;
		/// <summary>Retains the native <c>FileIndexHigh</c> layout field.</summary>
		internal uint FileIndexHigh;
		/// <summary>Retains the native <c>FileIndexLow</c> layout field.</summary>
		internal uint FileIndexLow;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct LinuxStatxTimestamp {
		/// <summary>Retains the native <c>Seconds</c> layout field.</summary>
		internal long Seconds;
		/// <summary>Retains the native <c>Nanoseconds</c> layout field.</summary>
		internal uint Nanoseconds;
		/// <summary>Retains the native <c>Reserved</c> layout field.</summary>
		internal int Reserved;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct LinuxStatx {
		/// <summary>Retains the native <c>Mask</c> layout field.</summary>
		internal uint Mask;
		/// <summary>Retains the native <c>BlockSize</c> layout field.</summary>
		internal uint BlockSize;
		/// <summary>Retains the native <c>Attributes</c> layout field.</summary>
		internal ulong Attributes;
		/// <summary>Retains the native <c>LinkCount</c> layout field.</summary>
		internal uint LinkCount;
		/// <summary>Retains the native <c>UserIdentifier</c> layout field.</summary>
		internal uint UserIdentifier;
		/// <summary>Retains the native <c>GroupIdentifier</c> layout field.</summary>
		internal uint GroupIdentifier;
		/// <summary>Retains the native <c>Mode</c> layout field.</summary>
		internal ushort Mode;
		/// <summary>Retains the native <c>Spare0</c> layout field.</summary>
		internal ushort Spare0;
		/// <summary>Retains the native <c>Inode</c> layout field.</summary>
		internal ulong Inode;
		/// <summary>Retains the native <c>Size</c> layout field.</summary>
		internal ulong Size;
		/// <summary>Retains the native <c>Blocks</c> layout field.</summary>
		internal ulong Blocks;
		/// <summary>Retains the native <c>AttributesMask</c> layout field.</summary>
		internal ulong AttributesMask;
		/// <summary>Retains the native <c>AccessTime</c> layout field.</summary>
		internal LinuxStatxTimestamp AccessTime;
		/// <summary>Retains the native <c>BirthTime</c> layout field.</summary>
		internal LinuxStatxTimestamp BirthTime;
		/// <summary>Retains the native <c>ChangeTime</c> layout field.</summary>
		internal LinuxStatxTimestamp ChangeTime;
		/// <summary>Retains the native <c>ModificationTime</c> layout field.</summary>
		internal LinuxStatxTimestamp ModificationTime;
		/// <summary>Retains the native <c>DeviceSpecialMajor</c> layout field.</summary>
		internal uint DeviceSpecialMajor;
		/// <summary>Retains the native <c>DeviceSpecialMinor</c> layout field.</summary>
		internal uint DeviceSpecialMinor;
		/// <summary>Retains the native <c>DeviceMajor</c> layout field.</summary>
		internal uint DeviceMajor;
		/// <summary>Retains the native <c>DeviceMinor</c> layout field.</summary>
		internal uint DeviceMinor;
		/// <summary>Retains the native <c>MountIdentifier</c> layout field.</summary>
		internal ulong MountIdentifier;
		/// <summary>Retains the native <c>DirectIoMemoryAlignment</c> layout field.</summary>
		internal uint DirectIoMemoryAlignment;
		/// <summary>Retains the native <c>DirectIoOffsetAlignment</c> layout field.</summary>
		internal uint DirectIoOffsetAlignment;
		/// <summary>Retains the native <c>Spare3_0</c> layout field.</summary>
		internal ulong Spare3_0;
		/// <summary>Retains the native <c>Spare3_1</c> layout field.</summary>
		internal ulong Spare3_1;
		/// <summary>Retains the native <c>Spare3_2</c> layout field.</summary>
		internal ulong Spare3_2;
		/// <summary>Retains the native <c>Spare3_3</c> layout field.</summary>
		internal ulong Spare3_3;
		/// <summary>Retains the native <c>Spare3_4</c> layout field.</summary>
		internal ulong Spare3_4;
		/// <summary>Retains the native <c>Spare3_5</c> layout field.</summary>
		internal ulong Spare3_5;
		/// <summary>Retains the native <c>Spare3_6</c> layout field.</summary>
		internal ulong Spare3_6;
		/// <summary>Retains the native <c>Spare3_7</c> layout field.</summary>
		internal ulong Spare3_7;
		/// <summary>Retains the native <c>Spare3_8</c> layout field.</summary>
		internal ulong Spare3_8;
		/// <summary>Retains the native <c>Spare3_9</c> layout field.</summary>
		internal ulong Spare3_9;
		/// <summary>Retains the native <c>Spare3_10</c> layout field.</summary>
		internal ulong Spare3_10;
		/// <summary>Retains the native <c>Spare3_11</c> layout field.</summary>
		internal ulong Spare3_11;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct DarwinTimespec {
		/// <summary>Retains the native <c>Seconds</c> layout field.</summary>
		internal long Seconds;
		/// <summary>Retains the native <c>Nanoseconds</c> layout field.</summary>
		internal long Nanoseconds;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct DarwinStatStructure {
		/// <summary>Retains the native <c>Device</c> layout field.</summary>
		internal int Device;
		/// <summary>Retains the native <c>Mode</c> layout field.</summary>
		internal ushort Mode;
		/// <summary>Retains the native <c>LinkCount</c> layout field.</summary>
		internal ushort LinkCount;
		/// <summary>Retains the native <c>Inode</c> layout field.</summary>
		internal ulong Inode;
		/// <summary>Retains the native <c>UserIdentifier</c> layout field.</summary>
		internal uint UserIdentifier;
		/// <summary>Retains the native <c>GroupIdentifier</c> layout field.</summary>
		internal uint GroupIdentifier;
		/// <summary>Retains the native <c>SpecialDevice</c> layout field.</summary>
		internal int SpecialDevice;
		/// <summary>Retains the native <c>AccessTime</c> layout field.</summary>
		internal DarwinTimespec AccessTime;
		/// <summary>Retains the native <c>ModificationTime</c> layout field.</summary>
		internal DarwinTimespec ModificationTime;
		/// <summary>Retains the native <c>ChangeTime</c> layout field.</summary>
		internal DarwinTimespec ChangeTime;
		/// <summary>Retains the native <c>BirthTime</c> layout field.</summary>
		internal DarwinTimespec BirthTime;
		/// <summary>Retains the native <c>Size</c> layout field.</summary>
		internal long Size;
		/// <summary>Retains the native <c>Blocks</c> layout field.</summary>
		internal long Blocks;
		/// <summary>Retains the native <c>BlockSize</c> layout field.</summary>
		internal int BlockSize;
		/// <summary>Retains the native <c>Flags</c> layout field.</summary>
		internal uint Flags;
		/// <summary>Retains the native <c>Generation</c> layout field.</summary>
		internal uint Generation;
		/// <summary>Retains the native <c>Spare</c> layout field.</summary>
		internal int Spare;
		/// <summary>Retains the native <c>QuadSpare0</c> layout field.</summary>
		internal long QuadSpare0;
		/// <summary>Retains the native <c>QuadSpare1</c> layout field.</summary>
		internal long QuadSpare1;
	}
#pragma warning restore CS0169, CS0649

	private readonly record struct NativeObservation {
		/// <summary>
		/// Initializes one native observation.
		/// </summary>
		/// <param name="entryIdentity">The entry identity.</param>
		/// <param name="fileSystemIdentity">The filesystem identity.</param>
		/// <param name="kind">The entry kind.</param>
		internal NativeObservation(
			FileSystemEntryIdentity entryIdentity,
			FileSystemIdentity fileSystemIdentity,
			FileSystemEntryKind kind
		) {
			EntryIdentity = entryIdentity;
			FileSystemIdentity = fileSystemIdentity;
			Kind = kind;
		}

		/// <summary>Gets the entry identity.</summary>
		internal FileSystemEntryIdentity EntryIdentity { get; }

		/// <summary>Gets the filesystem identity.</summary>
		internal FileSystemIdentity FileSystemIdentity { get; }

		/// <summary>Gets the entry kind.</summary>
		internal FileSystemEntryKind Kind { get; }

		/// <summary>Gets an unavailable native observation.</summary>
		internal static NativeObservation Unavailable { get; } = new(
			FileSystemEntryIdentity.Unavailable,
			FileSystemIdentity.Unavailable,
			FileSystemEntryKind.Unknown
		);
	}
}
