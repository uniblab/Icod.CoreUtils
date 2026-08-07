namespace Icod.CoreUtils.Shared.Terminal;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Implements terminal identification and console-mode access for Windows
/// console handles. POSIX speeds and control characters are deliberately not
/// synthesized.
/// </summary>
internal sealed class WindowsTerminalControlProvider : ITerminalControlProvider {
	private const int StandardInputHandle = -10;
	private const int StandardOutputHandle = -11;
	private const int StandardErrorHandle = -12;
	private const uint GenericRead = 0x80000000;
	private const uint GenericWrite = 0x40000000;
	private const uint ShareRead = 0x00000001;
	private const uint ShareWrite = 0x00000002;
	private const uint OpenExisting = 3;
	private const int ErrorInvalidHandle = 6;
	private const int ErrorInvalidParameter = 87;

	private WindowsTerminalControlProvider() {
	}

	/// <summary>Gets the process-wide Windows terminal-control provider.</summary>
	public static WindowsTerminalControlProvider Instance {
		get;
	} = new WindowsTerminalControlProvider();

	/// <inheritdoc />
	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		if ( !TryAcquire( endpoint, out var lease, out var error ) ) {
			return error!;
		}
		using ( lease ) {
			if ( !NativeGetConsoleMode( lease.Handle, out _ ) ) {
				var nativeError = Marshal.GetLastPInvokeError();
				if ( IsNonConsoleError( nativeError ) ) {
					return TerminalControlResult<TerminalEndpointObservation>.Available(
						new TerminalEndpointObservation(
							false,
							null,
							null,
							TerminalControlCapabilities.None
						)
					);
				}
				return TerminalControlResult<TerminalEndpointObservation>.Failed(
					BuildErrorMessage( endpoint, "inspect console attachment", nativeError ),
					nativeError
				);
			}

			var direction = GetDirection( endpoint, lease.Handle );
			var alias = TerminalConsoleDirection.Input == direction ? "CONIN$" : "CONOUT$";
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					alias,
					TerminalPlatformKind.WindowsConsole,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.Pathname
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
						| TerminalControlCapabilities.MachineSerialization
				)
			);
		}
	}

	/// <inheritdoc />
	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		if ( !TryAcquire( endpoint, out var lease, out var error ) ) {
			return ConvertAcquisitionError<TerminalModeSnapshot>( error! );
		}
		using ( lease ) {
			if ( NativeGetConsoleMode( lease.Handle, out var mode ) ) {
				return TerminalControlResult<TerminalModeSnapshot>.Available(
					TerminalModeSnapshot.CreateWindowsConsole(
						GetDirection( endpoint, lease.Handle ),
						mode
					)
				);
			}
			var nativeError = Marshal.GetLastPInvokeError();
			if ( IsNonConsoleError( nativeError ) ) {
				return TerminalControlResult<TerminalModeSnapshot>.Unavailable(
					string.Concat( endpoint.DisplayName, " is not attached to a Windows console." ),
					nativeError
				);
			}
			return TerminalControlResult<TerminalModeSnapshot>.Failed(
				BuildErrorMessage( endpoint, "read console mode", nativeError ),
				nativeError
			);
		}
	}

	/// <inheritdoc />
	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( mode );
		if ( !Enum.IsDefined( timing ) ) {
			throw new ArgumentOutOfRangeException( nameof( timing ) );
		}
		if ( TerminalPlatformKind.WindowsConsole != mode.Platform ) {
			return TerminalControlMutationResult.Unsupported(
				"A POSIX terminal mode cannot be applied to a Windows console."
			);
		}
		if ( TerminalModeApplyTiming.Immediately != timing ) {
			return TerminalControlMutationResult.Unsupported(
				"Windows console modes can only be applied immediately; drain and flush timing is a POSIX terminal capability."
			);
		}
		if ( !TryAcquire( endpoint, out var lease, out var acquisitionError ) ) {
			return ConvertAcquisitionMutationError( acquisitionError! );
		}
		using ( lease ) {
			var direction = GetDirection( endpoint, lease.Handle );
			if ( direction != mode.ConsoleDirection ) {
				return TerminalControlMutationResult.Unavailable(
					"The console mode direction does not match the destination handle."
				);
			}
			if ( NativeSetConsoleMode( lease.Handle, mode.ConsoleMode!.Value ) ) {
				return TerminalControlMutationResult.Success();
			}
			var nativeError = Marshal.GetLastPInvokeError();
			if ( IsNonConsoleError( nativeError ) ) {
				return TerminalControlMutationResult.Unavailable(
					string.Concat( endpoint.DisplayName, " is not attached to a Windows console." ),
					nativeError
				);
			}
			return TerminalControlMutationResult.Failed(
				BuildErrorMessage( endpoint, "change console mode", nativeError ),
				nativeError
			);
		}
	}

	private static bool TryAcquire(
		TerminalEndpoint endpoint,
		out ConsoleHandleLease lease,
		out TerminalControlResult<TerminalEndpointObservation>? error
	) {
		if ( TerminalEndpointKind.FileDescriptor == endpoint.Kind ) {
			var fileDescriptor = endpoint.FileDescriptor!.Value;
			IntPtr handle;
			try {
				handle = GetFileDescriptorHandle( fileDescriptor );
			} catch ( DllNotFoundException exception ) {
				lease = ConsoleHandleLease.Empty;
				error = TerminalControlResult<TerminalEndpointObservation>.Unsupported(
					string.Concat( "The Windows C runtime cannot resolve this file descriptor: ", exception.Message )
				);
				return false;
			} catch ( EntryPointNotFoundException exception ) {
				lease = ConsoleHandleLease.Empty;
				error = TerminalControlResult<TerminalEndpointObservation>.Unsupported(
					string.Concat( "The Windows C runtime cannot resolve this file descriptor: ", exception.Message )
				);
				return false;
			}
			if ( InvalidHandle( handle ) ) {
				var nativeError = 2 >= fileDescriptor
					? Marshal.GetLastPInvokeError()
					: ErrorInvalidHandle;
				if ( 0 == nativeError ) {
					nativeError = ErrorInvalidHandle;
				}
				lease = ConsoleHandleLease.Empty;
				error = TerminalControlResult<TerminalEndpointObservation>.Failed(
					BuildErrorMessage( endpoint, "resolve console handle", nativeError ),
					nativeError
				);
				return false;
			}
			lease = new ConsoleHandleLease( handle, null );
			error = null;
			return true;
		}

		var safeHandle = NativeCreateFile(
			endpoint.Path!,
			GenericRead | GenericWrite,
			ShareRead | ShareWrite,
			IntPtr.Zero,
			OpenExisting,
			0,
			IntPtr.Zero
		);
		if ( safeHandle.IsInvalid ) {
			safeHandle.Dispose();
			safeHandle = NativeCreateFile(
				endpoint.Path!,
				GenericRead,
				ShareRead | ShareWrite,
				IntPtr.Zero,
				OpenExisting,
				0,
				IntPtr.Zero
			);
		}
		if ( safeHandle.IsInvalid ) {
			var nativeError = Marshal.GetLastPInvokeError();
			safeHandle.Dispose();
			lease = ConsoleHandleLease.Empty;
			error = TerminalControlResult<TerminalEndpointObservation>.Failed(
				BuildErrorMessage( endpoint, "open console device", nativeError ),
				nativeError
			);
			return false;
		}
		lease = new ConsoleHandleLease( safeHandle.DangerousGetHandle(), safeHandle );
		error = null;
		return true;
	}

	private static IntPtr GetFileDescriptorHandle(
		int fileDescriptor
	) {
		return fileDescriptor switch {
			0 => NativeGetStandardHandle( StandardInputHandle ),
			1 => NativeGetStandardHandle( StandardOutputHandle ),
			2 => NativeGetStandardHandle( StandardErrorHandle ),
			_ => NativeGetOperatingSystemFileHandle( fileDescriptor )
		};
	}

	private static TerminalConsoleDirection GetDirection(
		TerminalEndpoint endpoint,
		IntPtr handle
	) {
		if ( NativeGetNumberOfConsoleInputEvents( handle, out _ ) ) {
			return TerminalConsoleDirection.Input;
		}
		if ( TerminalEndpointKind.Path == endpoint.Kind ) {
			var fileName = System.IO.Path.GetFileName( endpoint.Path! );
			if ( fileName.Equals( "CONIN$", StringComparison.OrdinalIgnoreCase ) ) {
				return TerminalConsoleDirection.Input;
			}
		}
		return TerminalConsoleDirection.Output;
	}

	private static bool InvalidHandle(
		IntPtr handle
	) {
		return IntPtr.Zero == handle || new IntPtr( -1 ) == handle;
	}

	private static bool IsNonConsoleError(
		int nativeError
	) {
		return ErrorInvalidHandle == nativeError || ErrorInvalidParameter == nativeError;
	}

	private static TerminalControlResult<T> ConvertAcquisitionError<T>(
		TerminalControlResult<TerminalEndpointObservation> error
	) {
		return error.Status switch {
			TerminalControlStatus.Unavailable => TerminalControlResult<T>.Unavailable( error.Message, error.NativeErrorCode ),
			TerminalControlStatus.Unsupported => TerminalControlResult<T>.Unsupported( error.Message ),
			_ => TerminalControlResult<T>.Failed( error.Message, error.NativeErrorCode )
		};
	}

	private static TerminalControlMutationResult ConvertAcquisitionMutationError(
		TerminalControlResult<TerminalEndpointObservation> error
	) {
		return error.Status switch {
			TerminalControlStatus.Unavailable => TerminalControlMutationResult.Unavailable( error.Message, error.NativeErrorCode ),
			TerminalControlStatus.Unsupported => TerminalControlMutationResult.Unsupported( error.Message ),
			_ => TerminalControlMutationResult.Failed( error.Message, error.NativeErrorCode )
		};
	}

	private static string BuildErrorMessage(
		TerminalEndpoint endpoint,
		string operation,
		int nativeError
	) {
		return string.Concat(
			"Cannot ",
			operation,
			" for ",
			endpoint.DisplayName,
			": ",
			new Win32Exception( nativeError ).Message
		);
	}

	[DllImport( "kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true )]
	private static extern IntPtr NativeGetStandardHandle(
		int standardHandle
	);

	[DllImport( "ucrtbase.dll", EntryPoint = "_get_osfhandle", SetLastError = false )]
	private static extern IntPtr NativeGetOperatingSystemFileHandle(
		int fileDescriptor
	);

	[DllImport( "kernel32.dll", EntryPoint = "GetConsoleMode", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeGetConsoleMode(
		IntPtr handle,
		out uint mode
	);

	[DllImport( "kernel32.dll", EntryPoint = "SetConsoleMode", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeSetConsoleMode(
		IntPtr handle,
		uint mode
	);

	[DllImport( "kernel32.dll", EntryPoint = "GetNumberOfConsoleInputEvents", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool NativeGetNumberOfConsoleInputEvents(
		IntPtr handle,
		out uint eventCount
	);

	[DllImport( "kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true )]
	private static extern SafeFileHandle NativeCreateFile(
		string fileName,
		uint desiredAccess,
		uint shareMode,
		IntPtr securityAttributes,
		uint creationDisposition,
		uint flagsAndAttributes,
		IntPtr templateFile
	);

	private sealed class ConsoleHandleLease : IDisposable {
		private readonly SafeFileHandle? ownedHandle;
		private bool disposed;

		public ConsoleHandleLease(
			IntPtr handle,
			SafeFileHandle? ownedHandle
		) {
			this.Handle = handle;
			this.ownedHandle = ownedHandle;
		}

		public static ConsoleHandleLease Empty {
			get;
		} = new ConsoleHandleLease( IntPtr.Zero, null );

		public IntPtr Handle {
			get;
		}

		public void Dispose() {
			if ( this.disposed ) {
				return;
			}
			this.disposed = true;
			this.ownedHandle?.Dispose();
		}
	}
}
