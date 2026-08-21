namespace Icod.CoreUtils.Truncate;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.FileSystem;
using Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Implements <see cref="ITruncatePlatform"/> with BCL file operations and platform-specific block-size queries.
/// </summary>
/// <remarks>
/// The implementation prefers managed APIs, uses narrowly scoped native metadata calls where required, and reports unsupported or failed operations through <c>global::Icod.CommandFramework.Platform.PlatformOperationResult</c>.
/// </remarks>
public sealed class SystemTruncatePlatform : ITruncatePlatform {

	private const int AtEmptyPath = 0x1000;
	private const uint StatxBasicStats = 0x000007ff;
	private readonly IFileSystemOperations myFileSystemOperations;

	/// <summary>
	/// Gets the shared platform implementation backed by the default filesystem capability provider.
	/// </summary>
	/// <value>The shared implementation instance.</value>
	public static SystemTruncatePlatform Instance {
		get;
	} = new SystemTruncatePlatform(
		SystemFileSystemOperations.Instance
	);

	/// <summary>
	/// Initializes a platform implementation with the specified filesystem capability provider.
	/// </summary>
	/// <param name="fileSystemOperations">The shared filesystem capability provider used by the platform implementation.</param>
	/// <exception cref="ArgumentNullException"><paramref name="fileSystemOperations"/> is <see langword="null"/>.</exception>
	public SystemTruncatePlatform(
		IFileSystemOperations fileSystemOperations
	) {
		this.myFileSystemOperations = fileSystemOperations ?? throw new ArgumentNullException(
			nameof( fileSystemOperations )
		);
	}

	/// <summary>
	/// Gets the filesystem-preferred I/O block size using the best available host metadata API.
	/// </summary>
	/// <param name="file">The open file whose filesystem allocation preference is queried.</param>
	/// <param name="path">The file pathname used by path-based host metadata APIs.</param>
	/// <param name="cancellationToken">The token checked before and during the host metadata query.</param>
	/// <returns>A capability result containing the positive preferred block size on success.</returns>
	public ValueTask<global::Icod.CommandFramework.Platform.PlatformOperationResult<long>> GetIoBlockSizeAsync(
		FileStream file,
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			file
		);
		ArgumentException.ThrowIfNullOrWhiteSpace(
			path
		);
		cancellationToken.ThrowIfCancellationRequested();
		try {
			if ( OperatingSystem.IsWindows() ) {
				return ValueTask.FromResult(
					GetWindowsIoBlockSize( path )
				);
			}
			if ( OperatingSystem.IsLinux() ) {
				return ValueTask.FromResult(
					GetLinuxIoBlockSize( file )
				);
			}
			if ( OperatingSystem.IsMacOS() ) {
				return ValueTask.FromResult(
					GetMacOsIoBlockSize( file )
				);
			}
			if ( OperatingSystem.IsFreeBSD() ) {
				return ValueTask.FromResult(
					GetFreeBsdIoBlockSize( file )
				);
			}
			return ValueTask.FromResult(
				global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Unsupported(
					"preferred I/O block-size discovery is not implemented on this platform"
				)
			);
		} catch ( EntryPointNotFoundException exception ) {
			return ValueTask.FromResult(
				global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Unsupported(
					String.Concat( "the required native entry point is unavailable: ", exception.Message )
				)
			);
		} catch ( DllNotFoundException exception ) {
			return ValueTask.FromResult(
				global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Unsupported(
					String.Concat( "the required native library is unavailable: ", exception.Message )
				)
			);
		} catch ( BadImageFormatException exception ) {
			return ValueTask.FromResult(
				global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Unsupported(
					String.Concat( "the native platform ABI is incompatible: ", exception.Message )
				)
			);
		} catch ( ObjectDisposedException exception ) {
			return ValueTask.FromResult(
				global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
					"the file handle is closed",
					exception
				)
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or ArgumentException
			or NotSupportedException
			or OverflowException
		) {
			return ValueTask.FromResult(
				global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
					exception.Message,
					exception
				)
			);
		}
	}

	/// <summary>
	/// Changes file length and prefers the shared sparse-extension capability when extending.
	/// </summary>
	/// <param name="file">The open writable file whose length is changed.</param>
	/// <param name="length">The requested non-negative logical length in bytes.</param>
	/// <param name="cancellationToken">The token used to cancel sparse extension or length adjustment.</param>
	/// <returns>A capability result describing the length-change outcome.</returns>
	public async ValueTask<global::Icod.CommandFramework.Platform.PlatformOperationResult> SetLengthAsync(
		FileStream file,
		long length,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			file
		);
		if ( 0 > length ) {
			throw new ArgumentOutOfRangeException(
				nameof( length )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();
		try {
			if ( file.Length < length ) {
				var sparseResult = await this.myFileSystemOperations.ExtendSparseAsync(
					file,
					length,
					cancellationToken
				).ConfigureAwait( false );
				if ( sparseResult.Succeeded ) {
					return sparseResult;
				}
				if ( sparseResult.Supported ) {
					return sparseResult;
				}

				file.SetLength(
					length
				);
				return global::Icod.CommandFramework.Platform.PlatformOperationResult.Success();
			}

			file.SetLength(
				length
			);
			return global::Icod.CommandFramework.Platform.PlatformOperationResult.Success();
		} catch ( ObjectDisposedException exception ) {
			return global::Icod.CommandFramework.Platform.PlatformOperationResult.Failure(
				"the file handle is closed",
				exception
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or ArgumentException
			or NotSupportedException
		) {
			return global::Icod.CommandFramework.Platform.PlatformOperationResult.Failure(
				exception.Message,
				exception
			);
		}
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> GetWindowsIoBlockSize(
		string path
	) {
		var fullPath = System.IO.Path.GetFullPath(
			path
		);
		var volumePath = new StringBuilder(
			32768
		);
		if ( !NativeMethods.GetVolumePathNameW(
			fullPath,
			volumePath,
			volumePath.Capacity
		) ) {
			var error = Marshal.GetLastPInvokeError();
			var exception = new Win32Exception( error );
			return global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
				exception.Message,
				exception
			);
		}
		if ( !NativeMethods.GetDiskFreeSpaceW(
			volumePath.ToString(),
			out var sectorsPerCluster,
			out var bytesPerSector,
			out _,
			out _
		) ) {
			var error = Marshal.GetLastPInvokeError();
			var exception = new Win32Exception( error );
			return global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
				exception.Message,
				exception
			);
		}
		var blockSize = checked(
			( long )sectorsPerCluster * bytesPerSector
		);
		return ValidateBlockSize(
			blockSize
		);
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> GetLinuxIoBlockSize(
		FileStream file
	) {
		return WithFileDescriptor(
			file,
			descriptor => {
				if ( 0 != NativeMethods.Statx(
					descriptor,
					string.Empty,
					AtEmptyPath,
					StatxBasicStats,
					out var status
				) ) {
					return NativeFailure(
						"statx"
					);
				}
				return ValidateBlockSize(
					status.BlockSize
				);
			}
		);
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> GetMacOsIoBlockSize(
		FileStream file
	) {
		return WithFileDescriptor(
			file,
			descriptor => {
				if ( 0 != NativeMethods.MacOsFStat(
					descriptor,
					out var status
				) ) {
					return NativeFailure(
						"fstat"
					);
				}
				return ValidateBlockSize(
					status.BlockSize
				);
			}
		);
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> GetFreeBsdIoBlockSize(
		FileStream file
	) {
		return WithFileDescriptor(
			file,
			descriptor => {
				if ( 0 != NativeMethods.FreeBsdFStat(
					descriptor,
					out var status
				) ) {
					return NativeFailure(
						"fstat"
					);
				}
				return ValidateBlockSize(
					status.BlockSize
				);
			}
		);
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> WithFileDescriptor(
		FileStream file,
		Func<int, global::Icod.CommandFramework.Platform.PlatformOperationResult<long>> operation
	) {
		var handle = file.SafeFileHandle;
		var referenceAdded = false;
		try {
			handle.DangerousAddRef(
				ref referenceAdded
			);
			var descriptorValue = handle.DangerousGetHandle().ToInt64();
			if (
				descriptorValue < int.MinValue
				|| descriptorValue > int.MaxValue
			) {
				return global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
					"the native file descriptor is outside the supported range"
				);
			}
			return operation(
				( int )descriptorValue
			);
		} finally {
			if ( referenceAdded ) {
				handle.DangerousRelease();
			}
		}
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> ValidateBlockSize(
		long blockSize
	) {
		return 0 < blockSize
			? global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Success( blockSize )
			: global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
				"the operating system returned an invalid I/O block size"
			)
		;
	}

	private static global::Icod.CommandFramework.Platform.PlatformOperationResult<long> NativeFailure(
		string operation
	) {
		var error = Marshal.GetLastPInvokeError();
		var exception = new Win32Exception( error );
		return global::Icod.CommandFramework.Platform.PlatformOperationResult<long>.Failure(
			String.Concat( operation, ": ", exception.Message ),
			exception
		);
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct LinuxStatxTimestamp {
		public long Seconds;
		public uint Nanoseconds;
		public int Reserved;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct LinuxStatx {
		public uint Mask;
		public uint BlockSize;
		public ulong Attributes;
		public uint LinkCount;
		public uint UserId;
		public uint GroupId;
		public ushort Mode;
		public ushort SpareZero;
		public ulong Inode;
		public ulong Size;
		public ulong Blocks;
		public ulong AttributesMask;
		public LinuxStatxTimestamp AccessTime;
		public LinuxStatxTimestamp BirthTime;
		public LinuxStatxTimestamp ChangeTime;
		public LinuxStatxTimestamp ModificationTime;
		public uint DeviceMajor;
		public uint DeviceMinor;
		public uint SpecialDeviceMajor;
		public uint SpecialDeviceMinor;
		public ulong MountId;
		public uint DirectIoMemoryAlignment;
		public uint DirectIoOffsetAlignment;
		public ulong SpareOne;
		public ulong SpareTwo;
		public ulong SpareThree;
		public ulong SpareFour;
		public ulong SpareFive;
		public ulong SpareSix;
		public ulong SpareSeven;
		public ulong SpareEight;
		public ulong SpareNine;
		public ulong SpareTen;
		public ulong SpareEleven;
		public ulong SpareTwelve;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct NativeTimespec {
		public long Seconds;
		public long Nanoseconds;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct MacOsStat {
		public int Device;
		public ushort Mode;
		public ushort LinkCount;
		public ulong Inode;
		public uint UserId;
		public uint GroupId;
		public int SpecialDevice;
		public NativeTimespec AccessTime;
		public NativeTimespec ModificationTime;
		public NativeTimespec ChangeTime;
		public NativeTimespec BirthTime;
		public long Size;
		public long Blocks;
		public int BlockSize;
		public uint Flags;
		public uint Generation;
		public int Spare;
		public long SpareOne;
		public long SpareTwo;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct FreeBsdStat {
		public ulong Device;
		public ulong Inode;
		public ulong LinkCount;
		public ushort Mode;
		public short BsdFlags;
		public uint UserId;
		public uint GroupId;
		public int PaddingOne;
		public ulong SpecialDevice;
		public NativeTimespec AccessTime;
		public NativeTimespec ModificationTime;
		public NativeTimespec ChangeTime;
		public NativeTimespec BirthTime;
		public long Size;
		public long Blocks;
		public int BlockSize;
		public uint Flags;
		public ulong Generation;
		public ulong FileRevision;
		public ulong SpareOne;
		public ulong SpareTwo;
		public ulong SpareThree;
		public ulong SpareFour;
		public ulong SpareFive;
		public ulong SpareSix;
		public ulong SpareSeven;
		public ulong SpareEight;
		public ulong SpareNine;
	}

	private static class NativeMethods {

		[DllImport(
			"kernel32.dll",
			CharSet = CharSet.Unicode,
			EntryPoint = "GetVolumePathNameW",
			SetLastError = true
		)]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool GetVolumePathNameW(
			string fileName,
			StringBuilder volumePathName,
			int bufferLength
		);

		[DllImport(
			"kernel32.dll",
			CharSet = CharSet.Unicode,
			EntryPoint = "GetDiskFreeSpaceW",
			SetLastError = true
		)]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool GetDiskFreeSpaceW(
			string rootPathName,
			out uint sectorsPerCluster,
			out uint bytesPerSector,
			out uint numberOfFreeClusters,
			out uint totalNumberOfClusters
		);

		[DllImport(
			"libc",
			EntryPoint = "statx",
			SetLastError = true
		)]
		public static extern int Statx(
			int directoryFileDescriptor,
			[MarshalAs( UnmanagedType.LPUTF8Str )] string path,
			int flags,
			uint mask,
			out LinuxStatx status
		);

		[DllImport(
			"libc",
			EntryPoint = "fstat",
			SetLastError = true
		)]
		public static extern int MacOsFStat(
			int fileDescriptor,
			out MacOsStat status
		);

		[DllImport(
			"libc",
			EntryPoint = "fstat",
			SetLastError = true
		)]
		public static extern int FreeBsdFStat(
			int fileDescriptor,
			out FreeBsdStat status
		);
	}
}
