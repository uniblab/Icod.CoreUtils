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
	private const uint GenericWrite = 0x40000000;
	private const uint FileShareRead = 0x00000001;
	private const uint FileShareWrite = 0x00000002;
	private const uint FileShareDelete = 0x00000004;
	private const uint OpenExisting = 3;
	private const uint FileFlagBackupSemantics = 0x02000000;
	private const int ErrorInvalidFunction = 1;
	private const int ErrorNotSupported = 50;
	private const int ErrorMoreData = 234;
	private const int ErrorIoPending = 997;
	private const int ErrorNoSuchDeviceOrAddress = 6;
	private const int ErrorInvalidArgument = 22;
	private const int LinuxOperationNotSupported = 95;
	private const int FreeBsdOperationNotSupported = 45;
	private const int SeekSet = 0;
	private const int SeekData = 3;
	private const int SeekHole = 4;
	private const int OpenReadOnly = 0;
	private const int OpenWriteOnly = 1;
	private const int GetFileStatusFlags = 3;
	private const int SetFileStatusFlags = 4;
	private const int WindowsRangeBufferSize = 64 * 1024;

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
		if (
			FileFlushMode.DataOnly != mode
			&& FileFlushMode.DataAndMetadata != mode
		) {
			return PlatformOperationResult.Failure(
				"the requested file-flush mode is invalid"
			);
		}

		try {
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

			var handle = file.SafeFileHandle;
			var addedReference = false;
			try {
				var descriptor = AcquireFileDescriptor(
					handle,
					out addedReference
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
			} finally {
				if ( addedReference ) {
					handle.DangerousRelease();
				}
			}
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
			or ArgumentException
		) {
			return PlatformOperationResult.Failure(
				exception.Message,
				exception
			);
		}
	}


	/// <inheritdoc />
	public ValueTask<PlatformOperationResult> FlushFileAsync(
		string path,
		FileFlushMode mode,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			path
		);
		cancellationToken.ThrowIfCancellationRequested();
		if (
			FileFlushMode.DataOnly != mode
			&& FileFlushMode.DataAndMetadata != mode
		) {
			return ValueTask.FromResult(
				PlatformOperationResult.Failure(
					"the requested file-flush mode is invalid"
				)
			);
		}
		if (
			FileFlushMode.DataOnly == mode
			&& !this.Capabilities.SupportsDataOnlyFileFlush
		) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					"data-only file flushing is unavailable on this platform"
				)
			);
		}
		if (
			FileFlushMode.DataAndMetadata == mode
			&& !this.Capabilities.SupportsDataAndMetadataFileFlush
		) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					"data-and-metadata file flushing is unavailable on this platform"
				)
			);
		}

		try {
			var result = OperatingSystem.IsWindows()
				? FlushPathOnWindows( path )
				: FlushPathOnUnix(
					path,
					mode,
					cancellationToken
				)
			;
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				result
			);
		} catch ( EntryPointNotFoundException exception ) {
			return ValueTask.FromResult(
				PlatformOperationResult.Unsupported(
					System.String.Concat(
						"the requested pathname-flush primitive is unavailable: ",
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
			or ObjectDisposedException
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
			var result = FlushFileSystemPathOnUnix(
				path,
				cancellationToken
			);
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult(
				result
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
		if ( !this.Capabilities.SupportsSparseExtension ) {
			return PlatformOperationResult<SparseExtensionInfo>.Unsupported(
				"sparse file extension is unavailable on this platform"
			);
		}

		try {
			if ( !file.CanWrite || !file.CanSeek ) {
				return PlatformOperationResult<SparseExtensionInfo>.Failure(
					"the file must be open for writing and seeking"
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
			or OverflowException
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
		if ( !this.Capabilities.SupportsAllocatedRangeQuery ) {
			return ValueTask.FromResult(
				PlatformOperationResult<FileAllocationMap>.Unsupported(
					"allocated-range queries are unavailable on this platform"
				)
			);
		}

		try {
			if ( !file.CanSeek ) {
				return ValueTask.FromResult(
					PlatformOperationResult<FileAllocationMap>.Failure(
						"allocated ranges require a seekable file"
					)
				);
			}
			file.Flush();
			var result = OperatingSystem.IsWindows()
				? QueryAllocatedRangesOnWindows( file )
				: QueryAllocatedRangesOnUnix( file )
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
			or ObjectDisposedException
			or NotSupportedException
			or ArgumentException
		) {
			return PlatformOperationResult<FileAllocationMap>.Failure(
				exception.Message,
				exception
			);
		}
	}


	private static PlatformOperationResult FlushPathOnWindows(
		string path
	) {
		using var handle = NativeMethods.OpenWindowsPath(
			path,
			GenericWrite,
			FileShareRead | FileShareWrite | FileShareDelete,
			IntPtr.Zero,
			OpenExisting,
			FileFlagBackupSemantics,
			IntPtr.Zero
		);
		if ( handle.IsInvalid ) {
			return CreateWindowsFailure(
				System.String.Concat(
					"cannot open '",
					path,
					"'"
				)
			);
		}
		if ( NativeMethods.FlushFileBuffers( handle ) ) {
			return PlatformOperationResult.Success();
		}
		return CreateWindowsFailure(
			System.String.Concat(
				"cannot synchronize '",
				path,
				"'"
			)
		);
	}

	private static PlatformOperationResult FlushPathOnUnix(
		string path,
		FileFlushMode mode,
		CancellationToken cancellationToken
	) {
		var nonBlocking = GetUnixNonBlockingFlag();
		var descriptor = NativeMethods.OpenUnixPath(
			path,
			OpenReadOnly | nonBlocking
		);
		if ( 0 > descriptor ) {
			var readError = Marshal.GetLastPInvokeError();
			descriptor = NativeMethods.OpenUnixPath(
				path,
				OpenWriteOnly | nonBlocking
			);
			if ( 0 > descriptor ) {
				// GNU reports the read-only open error because it is more
				// informative for directories and other special operands.
				return CreateUnixPathFailure(
					"cannot open",
					path,
					readError
				);
			}
		}

		try {
			if ( 0 != nonBlocking ) {
				var flags = NativeMethods.ControlFile(
					descriptor,
					GetFileStatusFlags,
					0
				);
				if ( 0 > flags ) {
					return CreateUnixPathFailure(
						"cannot read status flags for",
						path,
						Marshal.GetLastPInvokeError()
					);
				}
				if (
					0 > NativeMethods.ControlFile(
						descriptor,
						SetFileStatusFlags,
						flags & ~nonBlocking
					)
				) {
					return CreateUnixPathFailure(
						"cannot clear nonblocking mode for",
						path,
						Marshal.GetLastPInvokeError()
					);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			var synchronized = FileFlushMode.DataOnly == mode
				? NativeMethods.FlushData( descriptor )
				: NativeMethods.FlushDataAndMetadata( descriptor )
			;
			var synchronizationError = 0 == synchronized
				? 0
				: Marshal.GetLastPInvokeError()
			;
			cancellationToken.ThrowIfCancellationRequested();

			var closed = NativeMethods.CloseFile(
				descriptor
			);
			descriptor = -1;
			var closeError = 0 == closed
				? 0
				: Marshal.GetLastPInvokeError()
			;
			if ( 0 != synchronizationError ) {
				return CreateUnixPathFailure(
					FileFlushMode.DataOnly == mode
						? "cannot synchronize data for"
						: "cannot synchronize",
					path,
					synchronizationError
				);
			}
			if ( 0 != closeError ) {
				return CreateUnixPathFailure(
					"cannot close",
					path,
					closeError
				);
			}
			return PlatformOperationResult.Success();
		} finally {
			if ( 0 <= descriptor ) {
				_ = NativeMethods.CloseFile(
					descriptor
				);
			}
		}
	}


	private static PlatformOperationResult FlushFileSystemPathOnUnix(
		string path,
		CancellationToken cancellationToken
	) {
		var nonBlocking = GetUnixNonBlockingFlag();
		var descriptor = NativeMethods.OpenUnixPath(
			path,
			OpenReadOnly | nonBlocking
		);
		if ( 0 > descriptor ) {
			var readError = Marshal.GetLastPInvokeError();
			descriptor = NativeMethods.OpenUnixPath(
				path,
				OpenWriteOnly | nonBlocking
			);
			if ( 0 > descriptor ) {
				// GNU reports the read-only open error because it is more
				// informative for directories and other special operands.
				return CreateUnixPathFailure(
					"cannot open",
					path,
					readError
				);
			}
		}

		try {
			if ( 0 != nonBlocking ) {
				var flags = NativeMethods.ControlFile(
					descriptor,
					GetFileStatusFlags,
					0
				);
				if ( 0 > flags ) {
					return CreateUnixPathFailure(
						"cannot read status flags for",
						path,
						Marshal.GetLastPInvokeError()
					);
				}
				if (
					0 > NativeMethods.ControlFile(
						descriptor,
						SetFileStatusFlags,
						flags & ~nonBlocking
					)
				) {
					return CreateUnixPathFailure(
						"cannot clear nonblocking mode for",
						path,
						Marshal.GetLastPInvokeError()
					);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			var synchronized = NativeMethods.FlushFileSystem(
				descriptor
			);
			var synchronizationError = 0 == synchronized
				? 0
				: Marshal.GetLastPInvokeError()
			;
			cancellationToken.ThrowIfCancellationRequested();

			var closed = NativeMethods.CloseFile(
				descriptor
			);
			descriptor = -1;
			var closeError = 0 == closed
				? 0
				: Marshal.GetLastPInvokeError()
			;
			if ( 0 != synchronizationError ) {
				return CreateUnixPathFailure(
					"cannot synchronize filesystem for",
					path,
					synchronizationError
				);
			}
			if ( 0 != closeError ) {
				return CreateUnixPathFailure(
					"cannot close",
					path,
					closeError
				);
			}
			return PlatformOperationResult.Success();
		} finally {
			if ( 0 <= descriptor ) {
				_ = NativeMethods.CloseFile(
					descriptor
				);
			}
		}
	}

	private static int GetUnixNonBlockingFlag() {
		if ( OperatingSystem.IsLinux() ) {
			return 0x00000800;
		}
		if (
			OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
		) {
			return 0x00000004;
		}
		return 0;
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
				SupportsAllocatedRangeQuery: true
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
		ArgumentNullException.ThrowIfNull(
			file
		);
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
		var fileHandle = file.SafeFileHandle;
		var completionHandle = completion.SafeWaitHandle;
		var fileReferenceAdded = false;
		var eventReferenceAdded = false;
		var overlappedBuffer = IntPtr.Zero;
		try {
			fileHandle.DangerousAddRef(
				ref fileReferenceAdded
			);
			completionHandle.DangerousAddRef(
				ref eventReferenceAdded
			);

			var rawEventHandle = completionHandle.DangerousGetHandle();
			var overlapped = new WindowsOverlapped {
				Internal = IntPtr.Zero,
				InternalHigh = IntPtr.Zero,
				Offset = 0,
				OffsetHigh = 0,
				// A low-order bit of one suppresses IOCP notification for this request.
				// The event is still signalled and GetOverlappedResult can wait for it.
				EventHandle = new IntPtr(
					rawEventHandle.ToInt64() | 1L
				),
			};
			overlappedBuffer = Marshal.AllocHGlobal(
				Marshal.SizeOf<WindowsOverlapped>()
			);
			Marshal.StructureToPtr(
				overlapped,
				overlappedBuffer,
				false
			);

			var succeeded = NativeMethods.DeviceIoControl(
				fileHandle,
				controlCode,
				inputBuffer,
				inputBufferSize,
				outputBuffer,
				outputBufferSize,
				out bytesReturned,
				overlappedBuffer
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
				fileHandle,
				overlappedBuffer,
				out bytesReturned,
				true
			);
			error = succeeded
				? 0
				: Marshal.GetLastPInvokeError()
			;
			return succeeded;
		} finally {
			if ( IntPtr.Zero != overlappedBuffer ) {
				Marshal.FreeHGlobal(
					overlappedBuffer
				);
			}
			if ( eventReferenceAdded ) {
				completionHandle.DangerousRelease();
			}
			if ( fileReferenceAdded ) {
				fileHandle.DangerousRelease();
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
			or ObjectDisposedException
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

				if ( !succeeded && ErrorMoreData != error ) {
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

				if (
					0 > returnedBytes
					|| WindowsRangeBufferSize < returnedBytes
					|| 0 != returnedBytes % nativeRangeSize
				) {
					return PlatformOperationResult<FileAllocationMap>.Failure(
						"FSCTL_QUERY_ALLOCATED_RANGES returned an invalid buffer length"
					);
				}

				var count = returnedBytes / nativeRangeSize;
				for ( var index = 0; index < count; index++ ) {
					var native = Marshal.PtrToStructure<NativeAllocatedRange>(
						IntPtr.Add(
							output,
							index * nativeRangeSize
						)
					);
					if ( 0 > native.FileOffset || 0 >= native.Length ) {
						return PlatformOperationResult<FileAllocationMap>.Failure(
							"FSCTL_QUERY_ALLOCATED_RANGES returned an invalid range"
						);
					}
					long nativeEnd;
					try {
						nativeEnd = checked(
							native.FileOffset + native.Length
						);
					} catch ( OverflowException exception ) {
						return PlatformOperationResult<FileAllocationMap>.Failure(
							"FSCTL_QUERY_ALLOCATED_RANGES returned an overflowing range",
							exception
						);
					}
					if (
						length <= native.FileOffset
						|| nativeEnd <= queryOffset
					) {
						return PlatformOperationResult<FileAllocationMap>.Failure(
							"FSCTL_QUERY_ALLOCATED_RANGES returned a range outside the requested interval"
						);
					}
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

				if ( 0 == ranges.Count ) {
					return PlatformOperationResult<FileAllocationMap>.Failure(
						"FSCTL_QUERY_ALLOCATED_RANGES returned ERROR_MORE_DATA without a usable range"
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

	private static PlatformOperationResult<FileAllocationMap> QueryAllocatedRangesOnUnix(
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
		var handle = file.SafeFileHandle;
		var addedReference = false;
		var descriptor = -1;
		try {
			descriptor = AcquireFileDescriptor(
				handle,
				out addedReference
			);
			var offset = 0L;
			while ( offset < length ) {
				var data = NativeMethods.Seek(
					descriptor,
					offset,
					SeekData
				);
				if ( 0 > data ) {
					var error = Marshal.GetLastPInvokeError();
					if ( ErrorNoSuchDeviceOrAddress == error ) {
						break;
					}
					if ( IsUnixSeekUnsupportedError( error ) ) {
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
					SeekHole
				);
				if ( 0 > hole ) {
					var error = Marshal.GetLastPInvokeError();
					if ( ErrorNoSuchDeviceOrAddress == error ) {
						hole = length;
					} else if ( IsUnixSeekUnsupportedError( error ) ) {
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
			try {
				if (
					0 <= descriptor
					&& 0 > NativeMethods.Seek(
						descriptor,
						originalPosition,
						SeekSet
					)
				) {
					var error = Marshal.GetLastPInvokeError();
					var exception = new Win32Exception(
						error
					);
					throw new IOException(
						System.String.Concat(
							"could not restore the file position after allocated-range discovery: ",
							exception.Message
						),
						exception
					);
				}
				file.Position = originalPosition;
			} finally {
				if ( addedReference ) {
					handle.DangerousRelease();
				}
			}
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

	private static int AcquireFileDescriptor(
		SafeFileHandle handle,
		out bool addedReference
	) {
		ArgumentNullException.ThrowIfNull(
			handle
		);
		addedReference = false;
		try {
			handle.DangerousAddRef(
				ref addedReference
			);
			if ( handle.IsInvalid ) {
				throw new ObjectDisposedException(
					nameof( handle )
				);
			}
			return handle.DangerousGetHandle().ToInt32();
		} catch {
			if ( addedReference ) {
				handle.DangerousRelease();
				addedReference = false;
			}
			throw;
		}
	}

	private static bool IsWindowsUnsupportedError(
		int error
	) => error is
		ErrorInvalidFunction
		or ErrorNotSupported
	;

	private static bool IsUnixSeekUnsupportedError(
		int error
	) {
		if ( ErrorInvalidArgument == error ) {
			return true;
		}
		if (
			OperatingSystem.IsLinux()
			&& LinuxOperationNotSupported == error
		) {
			return true;
		}
		return
			OperatingSystem.IsFreeBSD()
			&& FreeBsdOperationNotSupported == error
		;
	}

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


	private static PlatformOperationResult CreateUnixPathFailure(
		string operation,
		string path,
		int error
	) {
		var exception = new Win32Exception(
			error
		);
		return PlatformOperationResult.Failure(
			System.String.Concat(
				operation,
				" '",
				path,
				"': ",
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
		/// <summary>
		/// Stores the internal value.
		/// </summary>
		public IntPtr Internal;
		/// <summary>
		/// Stores the internal high value.
		/// </summary>
		public IntPtr InternalHigh;
		/// <summary>
		/// Stores the offset value.
		/// </summary>
		public uint Offset;
		/// <summary>
		/// Stores the offset high value.
		/// </summary>
		public uint OffsetHigh;
		/// <summary>
		/// Stores the event handle value.
		/// </summary>
		public IntPtr EventHandle;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct NativeAllocatedRange {
		/// <summary>
		/// Stores the file offset value.
		/// </summary>
		public long FileOffset;
		/// <summary>
		/// Stores the length value.
		/// </summary>
		public long Length;
	}

	private static class NativeMethods {
		/// <summary>
		/// Performs the flush file buffers operation.
		/// </summary>
		[DllImport( "kernel32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool FlushFileBuffers(
			SafeFileHandle file
		);



		/// <summary>
		/// Performs the open windows path operation.
		/// </summary>
		[DllImport(
			"kernel32.dll",
			EntryPoint = "CreateFileW",
			CharSet = CharSet.Unicode,
			SetLastError = true
		)]
		internal static extern SafeFileHandle OpenWindowsPath(
			string fileName,
			uint desiredAccess,
			uint shareMode,
			IntPtr securityAttributes,
			uint creationDisposition,
			uint flagsAndAttributes,
			IntPtr templateFile
		);

		/// <summary>
		/// Performs the device io control operation.
		/// </summary>
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

		/// <summary>
		/// Gets overlapped result.
		/// </summary>
		[DllImport( "kernel32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool GetOverlappedResult(
			SafeFileHandle file,
			IntPtr overlapped,
			out int bytesTransferred,
			[MarshalAs( UnmanagedType.Bool )] bool wait
		);


		/// <summary>
		/// Performs the open unix path operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "open", SetLastError = true )]
		internal static extern int OpenUnixPath(
			string path,
			int flags
		);

		/// <summary>
		/// Performs the control file operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "fcntl", SetLastError = true )]
		internal static extern int ControlFile(
			int descriptor,
			int command,
			int argument
		);

		/// <summary>
		/// Performs the close file operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "close", SetLastError = true )]
		internal static extern int CloseFile(
			int descriptor
		);

		/// <summary>
		/// Performs the flush data operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "fdatasync", SetLastError = true )]
		internal static extern int FlushData(
			int descriptor
		);

		/// <summary>
		/// Performs the flush data and metadata operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "fsync", SetLastError = true )]
		internal static extern int FlushDataAndMetadata(
			int descriptor
		);

		/// <summary>
		/// Performs the flush file system operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "syncfs", SetLastError = true )]
		internal static extern int FlushFileSystem(
			int descriptor
		);

		/// <summary>
		/// Performs the flush all file systems operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "sync" )]
		internal static extern void FlushAllFileSystems();

		/// <summary>
		/// Performs the seek operation.
		/// </summary>
		[DllImport( "libc", EntryPoint = "lseek", SetLastError = true )]
		internal static extern long Seek(
			int descriptor,
			long offset,
			int origin
		);
	}
}
