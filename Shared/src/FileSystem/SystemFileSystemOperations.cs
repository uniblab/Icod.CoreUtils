namespace Icod.CoreUtils.Shared.FileSystem;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.Platform;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Implements durable-flush and sparse-file operations using the current operating system.
/// Unsupported semantics return controlled platform results rather than reporting false success.
/// </summary>
public sealed class SystemFileSystemOperations : IFileSystemOperations {
	private const uint FsctlSetSparse = 0x000900c4;
	private const uint FsctlQueryAllocatedRanges = 0x000940cf;
	private const int ErrorInvalidFunction = 1;
	private const int ErrorNotSupported = 50;
	private const int ErrorInvalidParameter = 87;
	private const int ErrorMoreData = 234;
	private const int LinuxSeekData = 3;
	private const int LinuxSeekHole = 4;
	private const int ErrorNoSuchDeviceOrAddress = 6;
	private const int ErrorInvalidArgument = 22;
	private const int LinuxOperationNotSupported = 95;
	private const int WindowsRangeBufferSize = 64 * 1024;
	private const int ErrorIoPending = 997;

	/// <summary>Gets the process-wide system implementation.</summary>
	public static SystemFileSystemOperations Instance { get; } = new();

	/// <inheritdoc />
	public FileSystemCapabilities Capabilities { get; }

	private SystemFileSystemOperations() {
		this.Capabilities = CreateCapabilities();
	}

	/// <inheritdoc />
	public async ValueTask<PlatformOperationResult> FlushFileAsync(
		FileStream file,
		FileFlushMode mode,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			file
		);
		cancellationToken.ThrowIfCancellationRequested();
		if ( !file.CanWrite ) {
			return PlatformOperationResult.Failure(
				"the file is not open for writing"
			);
		}
		if (
			FileFlushMode.DataOnly == mode
			&& !this.Capabilities.SupportsDataOnlyFileFlush
		) {
			return PlatformOperationResult.Unsupported(
				"data-only file flushing is unavailable on this platform"
			);
		}
		if (
			FileFlushMode.DataAndMetadata == mode
			&& !this.Capabilities.SupportsDataAndMetadataFileFlush
		) {
			return PlatformOperationResult.Unsupported(
				"data-and-metadata file flushing is unavailable on this platform"
			);
		}
		try {
			await file.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
			cancellationToken.ThrowIfCancellationRequested();
			if ( OperatingSystem.IsWindows() ) {
				if ( NativeMethods.FlushFileBuffers( file.SafeFileHandle ) ) {
					return PlatformOperationResult.Success();
				}
				return CreateWindowsFailure(
					"FlushFileBuffers failed"
				);
			}
			var descriptor = GetFileDescriptor(
				file.SafeFileHandle
			);
			var result = FileFlushMode.DataOnly == mode
				? NativeMethods.FlushData( descriptor )
				: NativeMethods.FlushDataAndMetadata( descriptor )
			;
			if ( 0 == result ) {
				return PlatformOperationResult.Success();
			}
			return CreateUnixFailure(
				FileFlushMode.DataOnly == mode
					? "fdatasync failed"
					: "fsync failed"
			);
		} catch ( EntryPointNotFoundException exception ) {
			return PlatformOperationResult.Unsupported(
				System.String.Concat(
					"the requested file-flush primitive is unavailable: ",
					exception.Message
				)
			);
		} catch ( DllNotFoundException exception ) {
			return PlatformOperationResult.Unsupported(
				System.String.Concat(
					"the native filesystem library is unavailable: ",
					exception.Message
				)
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or ObjectDisposedException
			or NotSupportedException
		) {
			return PlatformOperationResult.Failure(
				exception.Message,
				exception
			);
		}
	}

	/// <inheritdoc />
	public ValueTask<PlatformOperationResult> FlushFileSystemAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			path
		);
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.Capabilities.SupportsFileSystemFlush ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					"filesystem-specific flushing is available only where syncfs is exposed"
				)
			);
		}
		try {
			using var handle = File.OpenHandle(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				FileOptions.None
			);
			var result = NativeMethods.FlushFileSystem(
				GetFileDescriptor( handle )
			);
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				0 == result
					? PlatformOperationResult.Success()
					: CreateUnixFailure( "syncfs failed" )
			);
		} catch ( EntryPointNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					System.String.Concat(
						"syncfs is unavailable: ",
						exception.Message
					)
				)
			);
		} catch ( DllNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					System.String.Concat(
						"the native filesystem library is unavailable: ",
						exception.Message
					)
				)
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException
			or Win32Exception
		) {
			return ValueTask.FromResult(
				PlatformOperationResult.Failure(
					exception.Message,
					exception
				)
			);
		}
	}

	/// <inheritdoc />
	public ValueTask<PlatformOperationResult> FlushAllFileSystemsAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.Capabilities.SupportsGlobalFlush ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					"a process-level request to flush all mounted filesystems is unavailable on this platform"
				)
			);
		}
		try {
			NativeMethods.FlushAllFileSystems();
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				PlatformOperationResult.Success()
			);
		} catch ( EntryPointNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					System.String.Concat(
						"sync is unavailable: ",
						exception.Message
					)
				)
			);
		} catch ( DllNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					System.String.Concat(
						"the native filesystem library is unavailable: ",
						exception.Message
					)
				)
			);
		}
	}

	/// <inheritdoc />
	public async ValueTask<PlatformOperationResult<SparseExtensionInfo>> ExtendSparseAsync(
		FileStream file,
		long newLength,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			file
		);
		cancellationToken.ThrowIfCancellationRequested();
		if ( 0 > newLength ) {
			return PlatformOperationResult<SparseExtensionInfo>.Failure(
				"the requested file length cannot be negative"
			);
		}
		if ( !file.CanWrite || !file.CanSeek ) {
			return PlatformOperationResult<SparseExtensionInfo>.Failure(
				"the file must be open for writing and seeking"
			);
		}
		if ( !this.Capabilities.SupportsSparseExtension ) {
			return PlatformOperationResult<SparseExtensionInfo>.Unsupported(
				"sparse file extension is unavailable on this platform"
			);
		}
		var originalLength = file.Length;
		if ( newLength < originalLength ) {
			return PlatformOperationResult<SparseExtensionInfo>.Failure(
				"sparse extension cannot reduce the file length"
			);
		}
		var originalPosition = file.Position;
		if ( newLength == originalLength ) {
			var existingAllocation = await this.GetAllocatedRangesAsync(
				file,
				cancellationToken
			).ConfigureAwait( false );
			return PlatformOperationResult<SparseExtensionInfo>.Success(
				new SparseExtensionInfo(
					originalLength,
					newLength,
					existingAllocation
				)
			);
		}
		try {
			if ( OperatingSystem.IsWindows() ) {
				var sparseResult = TryMarkSparseOnWindows(
					file
				);
				if ( !sparseResult.Succeeded ) {
					return sparseResult.Supported
						? PlatformOperationResult<SparseExtensionInfo>.Failure(
							sparseResult.Message ?? "could not mark the file sparse",
							sparseResult.Exception
						)
						: PlatformOperationResult<SparseExtensionInfo>.Unsupported(
							sparseResult.Message ?? "sparse files are unavailable"
						)
					;
				}
			}
			file.SetLength(
				newLength
			);
			file.Position = Math.Min(
				originalPosition,
				newLength
			);
			await file.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
			var allocation = await this.GetAllocatedRangesAsync(
				file,
				cancellationToken
			).ConfigureAwait( false );
			return PlatformOperationResult<SparseExtensionInfo>.Success(
				new SparseExtensionInfo(
					originalLength,
					newLength,
					allocation
				)
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or ObjectDisposedException
			or NotSupportedException
			or ArgumentException
		) {
			return PlatformOperationResult<SparseExtensionInfo>.Failure(
				exception.Message,
				exception
			);
		}
	}

	/// <inheritdoc />
	public ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
		FileStream file,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			file
		);
		cancellationToken.ThrowIfCancellationRequested();
		if ( !file.CanSeek ) {
			return ValueTask.FromResult(
				PlatformOperationResult<FileAllocationMap>.Failure(
					"allocated ranges require a seekable file"
				)
			);
		}
		if ( !this.Capabilities.SupportsAllocatedRangeQuery ) {
			return ValueTask.FromResult(
				PlatformOperationResult<FileAllocationMap>.Unsupported(
					"allocated-range queries are unavailable on this platform"
				)
			);
		}
		try {
			var result = OperatingSystem.IsWindows()
				? QueryAllocatedRangesOnWindows( file )
				: QueryAllocatedRangesOnLinux( file )
			;
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				result
			);
		} catch ( EntryPointNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult<FileAllocationMap>.Unsupported(
					System.String.Concat(
						"allocated-range discovery is unavailable: ",
						exception.Message
					)
				)
			);
		} catch ( DllNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult<FileAllocationMap>.Unsupported(
					System.String.Concat(
						"the native filesystem library is unavailable: ",
						exception.Message
					)
				)
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or ObjectDisposedException
			or NotSupportedException
			or ArgumentException
			or OverflowException
		) {
			return ValueTask.FromResult(
				PlatformOperationResult<FileAllocationMap>.Failure(
					exception.Message,
					exception
				)
			);
		}
	}

	/// <inheritdoc />
	public async ValueTask<PlatformOperationResult<FileAllocationMap>> GetAllocatedRangesAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			path
		);
		cancellationToken.ThrowIfCancellationRequested();
		try {
			await using var file = new FileStream(
				path,
				new FileStreamOptions {
					Access = FileAccess.Read,
					Mode = FileMode.Open,
					Share = FileShare.ReadWrite | FileShare.Delete,
					Options = FileOptions.Asynchronous | FileOptions.RandomAccess,
				}
			);
			return await this.GetAllocatedRangesAsync(
				file,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException
		) {
			return PlatformOperationResult<FileAllocationMap>.Failure(
				exception.Message,
				exception
			);
		}
	}

	private static FileSystemCapabilities CreateCapabilities() {
		if ( OperatingSystem.IsWindows() ) {
			return new FileSystemCapabilities(
				SupportsDataOnlyFileFlush: false,
				SupportsDataAndMetadataFileFlush: true,
				SupportsFileSystemFlush: false,
				SupportsGlobalFlush: false,
				SupportsSparseExtension: true,
				SupportsAllocatedRangeQuery: true
			);
		}
		if ( OperatingSystem.IsLinux() ) {
			return new FileSystemCapabilities(
				SupportsDataOnlyFileFlush: true,
				SupportsDataAndMetadataFileFlush: true,
				SupportsFileSystemFlush: true,
				SupportsGlobalFlush: true,
				SupportsSparseExtension: true,
				SupportsAllocatedRangeQuery: true
			);
		}
		if ( OperatingSystem.IsMacOS() ) {
			return new FileSystemCapabilities(
				SupportsDataOnlyFileFlush: false,
				SupportsDataAndMetadataFileFlush: true,
				SupportsFileSystemFlush: false,
				SupportsGlobalFlush: true,
				SupportsSparseExtension: true,
				SupportsAllocatedRangeQuery: false
			);
		}
		if ( OperatingSystem.IsFreeBSD() ) {
			return new FileSystemCapabilities(
				SupportsDataOnlyFileFlush: true,
				SupportsDataAndMetadataFileFlush: true,
				SupportsFileSystemFlush: false,
				SupportsGlobalFlush: true,
				SupportsSparseExtension: true,
				SupportsAllocatedRangeQuery: false
			);
		}
		return new FileSystemCapabilities(
			SupportsDataOnlyFileFlush: false,
			SupportsDataAndMetadataFileFlush: false,
			SupportsFileSystemFlush: false,
			SupportsGlobalFlush: false,
			SupportsSparseExtension: false,
			SupportsAllocatedRangeQuery: false
		);
	}

	private static bool InvokeDeviceIoControl(
		FileStream file,
		uint controlCode,
		IntPtr inputBuffer,
		int inputBufferSize,
		IntPtr outputBuffer,
		int outputBufferSize,
		out int bytesReturned,
		out int error
	) {
		if ( !file.IsAsync ) {
			var succeeded = NativeMethods.DeviceIoControl(
				file.SafeFileHandle,
				controlCode,
				inputBuffer,
				inputBufferSize,
				outputBuffer,
				outputBufferSize,
				out bytesReturned,
				IntPtr.Zero
			);
			error = succeeded
				? 0
				: Marshal.GetLastPInvokeError()
			;
			return succeeded;
		}

		using var completion = new EventWaitHandle(
			false,
			EventResetMode.ManualReset
		);
		var addedReference = false;
		try {
			completion.SafeWaitHandle.DangerousAddRef(
				ref addedReference
			);
			var overlapped = new WindowsOverlapped {
				EventHandle = completion.SafeWaitHandle.DangerousGetHandle(),
			};
			var succeeded = NativeMethods.DeviceIoControlOverlapped(
				file.SafeFileHandle,
				controlCode,
				inputBuffer,
				inputBufferSize,
				outputBuffer,
				outputBufferSize,
				out bytesReturned,
				ref overlapped
			);
			if ( succeeded ) {
				error = 0;
				return true;
			}
			error = Marshal.GetLastPInvokeError();
			if ( ErrorIoPending != error ) {
				return false;
			}
			succeeded = NativeMethods.GetOverlappedResult(
				file.SafeFileHandle,
				ref overlapped,
				out bytesReturned,
				true
			);
			error = succeeded
				? 0
				: Marshal.GetLastPInvokeError()
			;
			return succeeded;
		} finally {
			if ( addedReference ) {
				completion.SafeWaitHandle.DangerousRelease();
			}
		}
	}

	private static PlatformOperationResult TryMarkSparseOnWindows(
		FileStream file
	) {
		try {
			var succeeded = InvokeDeviceIoControl(
				file,
				FsctlSetSparse,
				IntPtr.Zero,
				0,
				IntPtr.Zero,
				0,
				out _,
				out var error
			);
			if ( succeeded ) {
				return PlatformOperationResult.Success();
			}
			if ( IsWindowsUnsupportedError( error ) ) {
				return PlatformOperationResult.Unsupported(
					new Win32Exception( error ).Message
				);
			}
			return CreateWindowsFailure(
				"FSCTL_SET_SPARSE failed",
				error
			);
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException
		) {
			return PlatformOperationResult.Failure(
				exception.Message,
				exception
			);
		}
	}

	private static PlatformOperationResult<FileAllocationMap> QueryAllocatedRangesOnWindows(
		FileStream file
	) {
		var length = file.Length;
		if ( 0 == length ) {
			return PlatformOperationResult<FileAllocationMap>.Success(
				new FileAllocationMap(
					0,
					Array.Empty<FileAllocationRange>()
				)
			);
		}
		var nativeRangeSize = Marshal.SizeOf<NativeAllocatedRange>();
		var input = Marshal.AllocHGlobal(
			nativeRangeSize
		);
		var output = Marshal.AllocHGlobal(
			WindowsRangeBufferSize
		);
		try {
			var ranges = new List<FileAllocationRange>();
			var queryOffset = 0L;
			while ( queryOffset < length ) {
				Marshal.StructureToPtr(
					new NativeAllocatedRange {
						FileOffset = queryOffset,
						Length = length - queryOffset,
					},
					input,
					false
				);
				var succeeded = InvokeDeviceIoControl(
					file,
					FsctlQueryAllocatedRanges,
					input,
					nativeRangeSize,
					output,
					WindowsRangeBufferSize,
					out var returnedBytes,
					out var error
				);
				var count = returnedBytes / nativeRangeSize;
				for ( var index = 0; index < count; index++ ) {
					var native = Marshal.PtrToStructure<NativeAllocatedRange>(
						IntPtr.Add(
							output,
							index * nativeRangeSize
						)
					);
					AddRange(
						ranges,
						native.FileOffset,
						native.Length,
						length
					);
				}
				if ( succeeded ) {
					return PlatformOperationResult<FileAllocationMap>.Success(
						new FileAllocationMap(
							length,
							ranges
						)
					);
				}
				if ( ErrorMoreData != error ) {
					if ( IsWindowsUnsupportedError( error ) ) {
						return PlatformOperationResult<FileAllocationMap>.Unsupported(
							new Win32Exception( error ).Message
						);
					}
					var exception = new Win32Exception( error );
					return PlatformOperationResult<FileAllocationMap>.Failure(
						System.String.Concat(
							"FSCTL_QUERY_ALLOCATED_RANGES failed: ",
							exception.Message
						),
						exception
					);
				}
				if ( 0 == count ) {
					return PlatformOperationResult<FileAllocationMap>.Failure(
						"FSCTL_QUERY_ALLOCATED_RANGES returned ERROR_MORE_DATA without a range"
					);
				}
				var nextOffset = ranges[^1].End;
				if ( nextOffset <= queryOffset ) {
					return PlatformOperationResult<FileAllocationMap>.Failure(
						"FSCTL_QUERY_ALLOCATED_RANGES did not advance the query"
					);
				}
				queryOffset = nextOffset;
			}
			return PlatformOperationResult<FileAllocationMap>.Success(
				new FileAllocationMap(
					length,
					ranges
				)
			);
		} finally {
			Marshal.FreeHGlobal(
				output
			);
			Marshal.FreeHGlobal(
				input
			);
		}
	}

	private static PlatformOperationResult<FileAllocationMap> QueryAllocatedRangesOnLinux(
		FileStream file
	) {
		var length = file.Length;
		if ( 0 == length ) {
			return PlatformOperationResult<FileAllocationMap>.Success(
				new FileAllocationMap(
					0,
					Array.Empty<FileAllocationRange>()
				)
			);
		}
		var originalPosition = file.Position;
		var ranges = new List<FileAllocationRange>();
		try {
			file.Flush();
			var descriptor = GetFileDescriptor(
				file.SafeFileHandle
			);
			var offset = 0L;
			while ( offset < length ) {
				var data = NativeMethods.Seek(
					descriptor,
					offset,
					LinuxSeekData
				);
				if ( 0 > data ) {
					var error = Marshal.GetLastPInvokeError();
					if ( ErrorNoSuchDeviceOrAddress == error ) {
						break;
					}
					if ( IsLinuxUnsupportedError( error ) ) {
						return PlatformOperationResult<FileAllocationMap>.Unsupported(
							new Win32Exception( error ).Message
						);
					}
					return CreateUnixAllocationFailure(
						"SEEK_DATA failed",
						error
					);
				}
				var hole = NativeMethods.Seek(
					descriptor,
					data,
					LinuxSeekHole
				);
				if ( 0 > hole ) {
					var error = Marshal.GetLastPInvokeError();
					if ( ErrorNoSuchDeviceOrAddress == error ) {
						hole = length;
					} else if ( IsLinuxUnsupportedError( error ) ) {
						return PlatformOperationResult<FileAllocationMap>.Unsupported(
							new Win32Exception( error ).Message
						);
					} else {
						return CreateUnixAllocationFailure(
							"SEEK_HOLE failed",
							error
						);
					}
				}
				if ( hole <= data ) {
					return PlatformOperationResult<FileAllocationMap>.Failure(
						"SEEK_HOLE did not advance beyond SEEK_DATA"
					);
				}
				AddRange(
					ranges,
					data,
					hole - data,
					length
				);
				offset = hole;
			}
			return PlatformOperationResult<FileAllocationMap>.Success(
				new FileAllocationMap(
					length,
					ranges
				)
			);
		} finally {
			file.Position = Math.Min(
				originalPosition,
				file.Length
			);
		}
	}

	private static void AddRange(
		List<FileAllocationRange> ranges,
		long offset,
		long length,
		long logicalLength
	) {
		if ( 0 > offset || 0 >= length || logicalLength <= offset ) {
			return;
		}
		var end = Math.Min(
			checked( offset + length ),
			logicalLength
		);
		if ( end <= offset ) {
			return;
		}
		if (
			0 < ranges.Count
			&& offset <= ranges[^1].End
		) {
			var previous = ranges[^1];
			var combinedEnd = Math.Max(
				previous.End,
				end
			);
			ranges[^1] = new FileAllocationRange(
				previous.Offset,
				combinedEnd - previous.Offset
			);
			return;
		}
		ranges.Add(
			new FileAllocationRange(
				offset,
				end - offset
			)
		);
	}

	private static int GetFileDescriptor(
		SafeFileHandle handle
	) {
		if ( handle.IsInvalid || handle.IsClosed ) {
			throw new ObjectDisposedException(
				nameof( handle )
			);
		}
		return handle.DangerousGetHandle().ToInt32();
	}

	private static bool IsWindowsUnsupportedError(
		int error
	) => error is
		ErrorInvalidFunction
		or ErrorNotSupported
		or ErrorInvalidParameter
	;

	private static bool IsLinuxUnsupportedError(
		int error
	) => error is
		ErrorInvalidArgument
		or LinuxOperationNotSupported
	;

	private static PlatformOperationResult CreateWindowsFailure(
		string operation,
		int? error = null
	) {
		var actualError = error ?? Marshal.GetLastPInvokeError();
		var exception = new Win32Exception(
			actualError
		);
		return PlatformOperationResult.Failure(
			System.String.Concat(
				operation,
				": ",
				exception.Message
			),
			exception
		);
	}

	private static PlatformOperationResult CreateUnixFailure(
		string operation
	) {
		var error = Marshal.GetLastPInvokeError();
		var exception = new Win32Exception(
			error
		);
		return PlatformOperationResult.Failure(
			System.String.Concat(
				operation,
				": ",
				exception.Message
			),
			exception
		);
	}

	private static PlatformOperationResult<FileAllocationMap> CreateUnixAllocationFailure(
		string operation,
		int error
	) {
		var exception = new Win32Exception(
			error
		);
		return PlatformOperationResult<FileAllocationMap>.Failure(
			System.String.Concat(
				operation,
				": ",
				exception.Message
			),
			exception
		);
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct WindowsOverlapped {
		public IntPtr Internal;
		public IntPtr InternalHigh;
		public uint Offset;
		public uint OffsetHigh;
		public IntPtr EventHandle;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct NativeAllocatedRange {
		public long FileOffset;
		public long Length;
	}

	private static class NativeMethods {
		[DllImport( "kernel32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool FlushFileBuffers(
			SafeFileHandle file
		);


		[DllImport( "kernel32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool DeviceIoControl(
			SafeFileHandle device,
			uint controlCode,
			IntPtr inputBuffer,
			int inputBufferSize,
			IntPtr outputBuffer,
			int outputBufferSize,
			out int bytesReturned,
			IntPtr overlapped
		);

		[DllImport( "kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool DeviceIoControlOverlapped(
			SafeFileHandle device,
			uint controlCode,
			IntPtr inputBuffer,
			int inputBufferSize,
			IntPtr outputBuffer,
			int outputBufferSize,
			out int bytesReturned,
			ref WindowsOverlapped overlapped
		);

		[DllImport( "kernel32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool GetOverlappedResult(
			SafeFileHandle file,
			ref WindowsOverlapped overlapped,
			out int bytesTransferred,
			[MarshalAs( UnmanagedType.Bool )] bool wait
		);

		[DllImport( "libc", EntryPoint = "fdatasync", SetLastError = true )]
		internal static extern int FlushData(
			int descriptor
		);

		[DllImport( "libc", EntryPoint = "fsync", SetLastError = true )]
		internal static extern int FlushDataAndMetadata(
			int descriptor
		);

		[DllImport( "libc", EntryPoint = "syncfs", SetLastError = true )]
		internal static extern int FlushFileSystem(
			int descriptor
		);

		[DllImport( "libc", EntryPoint = "sync" )]
		internal static extern void FlushAllFileSystems();

		[DllImport( "libc", EntryPoint = "lseek", SetLastError = true )]
		internal static extern long Seek(
			int descriptor,
			long offset,
			int origin
		);
	}
}
