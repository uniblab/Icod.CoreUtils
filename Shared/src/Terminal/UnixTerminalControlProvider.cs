namespace Icod.CoreUtils.Shared.Terminal;

using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>
/// Implements terminal identification and complete <c>termios</c> access on
/// Linux and macOS.
/// </summary>
internal sealed class UnixTerminalControlProvider : ITerminalControlProvider {
	private const string LibC = "libc";
	private const int InvalidArgument = 22;
	private const int NotATerminal = 25;
	private const int OpenReadOnly = 0;
	private const int OpenWriteOnly = 1;
	private const int OpenReadWrite = 2;
	private const int LinuxOpenNoControllingTerminal = 0x100;
	private const int LinuxOpenNonBlocking = 0x800;
	private const int MacOpenNonBlocking = 0x4;
	private const int MacOpenNoControllingTerminal = 0x20000;
	private const int TerminalApplyImmediately = 0;
	private const int TerminalApplyAfterDrain = 1;
	private const int TerminalApplyAfterDrainAndFlush = 2;
	private const int LinuxTermiosSize = 60;
	private const int LinuxControlCharacterOffset = 17;
	private const int LinuxControlCharacterCount = 32;
	private const int LinuxInputSpeedOffset = 52;
	private const int LinuxOutputSpeedOffset = 56;
	private const int MacTermiosSize = 72;
	private const int MacControlCharacterOffset = 32;
	private const int MacControlCharacterCount = 20;
	private const int MacInputSpeedOffset = 56;
	private const int MacOutputSpeedOffset = 64;

	private UnixTerminalControlProvider() {
	}

	/// <summary>Gets the process-wide POSIX terminal-control provider.</summary>
	public static UnixTerminalControlProvider Instance {
		get;
	} = new UnixTerminalControlProvider();

	/// <inheritdoc />
	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		var acquired = TryAcquire( endpoint, false, out var lease, out var error );
		if ( !acquired ) {
			return error!;
		}
		using ( lease ) {
			if ( 0 == NativeIsATerminal( lease.FileDescriptor ) ) {
				var nativeError = Marshal.GetLastPInvokeError();
				if ( NotATerminal == nativeError ) {
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
					BuildErrorMessage( endpoint, "inspect terminal attachment", nativeError ),
					nativeError
				);
			}

			var pathname = TryGetTerminalName( lease.FileDescriptor );
			var capabilities = TerminalControlCapabilities.Attachment
				| TerminalControlCapabilities.ModeRead
				| TerminalControlCapabilities.ModeWrite
				| TerminalControlCapabilities.Speeds
				| TerminalControlCapabilities.ControlCharacters
				| TerminalControlCapabilities.MachineSerialization;
			if ( null is not pathname ) {
				capabilities |= TerminalControlCapabilities.Pathname;
			}
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					pathname,
					TerminalPlatformKind.PosixTermios,
					capabilities
				)
			);
		}
	}

	/// <inheritdoc />
	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		var acquired = TryAcquire( endpoint, false, out var lease, out var error );
		if ( !acquired ) {
			return ConvertAcquisitionError<TerminalModeSnapshot>( error! );
		}
		using ( lease ) {
			var abi = GetAbi();
			var buffer = Marshal.AllocHGlobal( abi.StructureSize );
			try {
				if ( 0 != NativeGetAttributes( lease.FileDescriptor, buffer ) ) {
					var nativeError = Marshal.GetLastPInvokeError();
					if ( NotATerminal == nativeError ) {
						return TerminalControlResult<TerminalModeSnapshot>.Unavailable(
							string.Concat( endpoint.DisplayName, " is not a terminal." ),
							nativeError
						);
					}
					return TerminalControlResult<TerminalModeSnapshot>.Failed(
						BuildErrorMessage( endpoint, "read terminal mode", nativeError ),
						nativeError
					);
				}

				var bytes = new byte[ abi.StructureSize ];
				Marshal.Copy( buffer, bytes, 0, bytes.Length );
				var inputSpeedCode = checked( (ulong)NativeGetInputSpeed( buffer ) );
				var outputSpeedCode = checked( (ulong)NativeGetOutputSpeed( buffer ) );
				return TerminalControlResult<TerminalModeSnapshot>.Available(
					CreateSnapshot( bytes, abi, inputSpeedCode, outputSpeedCode )
				);
			} finally {
				Marshal.FreeHGlobal( buffer );
			}
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
		if ( TerminalPlatformKind.PosixTermios != mode.Platform ) {
			return TerminalControlMutationResult.Unsupported(
				"A Windows console mode cannot be applied to a POSIX terminal."
			);
		}
		if ( !Enum.IsDefined( timing ) ) {
			throw new ArgumentOutOfRangeException( nameof( timing ) );
		}

		var abi = GetAbi();
		if ( abi.FlagWidth != mode.NativeFlagWidth ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal mode was captured for a different native flag width."
			);
		}
		if ( abi.ControlCharacterCount != mode.ControlCharacters.Count ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal mode contains a control-character array for a different host ABI."
			);
		}
		if ( abi.DisabledControlCharacter != mode.DisabledControlCharacter ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal mode uses a disabled-character value for a different host ABI."
			);
		}
		if ( abi.LineDisciplineOffset.HasValue != mode.LineDiscipline.HasValue ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal mode uses a line-discipline layout for a different host ABI."
			);
		}
		if ( ( 32 == abi.FlagWidth )
			&& ( ( uint.MaxValue < mode.InputFlags )
				|| ( uint.MaxValue < mode.OutputFlags )
				|| ( uint.MaxValue < mode.ControlFlags )
				|| ( uint.MaxValue < mode.LocalFlags ) ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal mode contains flags wider than this host ABI."
			);
		}
		if ( ( 4 == abi.SpeedByteWidth )
			&& ( ( uint.MaxValue < mode.InputSpeed!.Value.NativeCode )
				|| ( uint.MaxValue < mode.OutputSpeed!.Value.NativeCode ) ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal mode contains speed codes wider than this host ABI."
			);
		}

		var acquired = TryAcquire( endpoint, true, out var lease, out var acquisitionError );
		if ( !acquired ) {
			return ConvertAcquisitionMutationError( acquisitionError! );
		}
		using ( lease ) {
			var bytes = CreateNativeBytes( mode, abi );
			var buffer = Marshal.AllocHGlobal( bytes.Length );
			try {
				Marshal.Copy( bytes, 0, buffer, bytes.Length );
				if ( 0 != NativeSetInputSpeed( buffer, checked( (nuint)mode.InputSpeed!.Value.NativeCode ) )
					|| 0 != NativeSetOutputSpeed( buffer, checked( (nuint)mode.OutputSpeed!.Value.NativeCode ) ) ) {
					var nativeError = Marshal.GetLastPInvokeError();
					return TerminalControlMutationResult.Unavailable(
						string.Concat(
							"The terminal speed code is not supported by this host: ",
							new Win32Exception( nativeError ).Message
						),
						nativeError
					);
				}
				var action = timing switch {
					TerminalModeApplyTiming.Immediately => TerminalApplyImmediately,
					TerminalModeApplyTiming.AfterOutputDrained => TerminalApplyAfterDrain,
					TerminalModeApplyTiming.AfterOutputDrainedAndInputDiscarded => TerminalApplyAfterDrainAndFlush,
					_ => throw new ArgumentOutOfRangeException( nameof( timing ) )
				};
				if ( 0 == NativeSetAttributes( lease.FileDescriptor, action, buffer ) ) {
					return TerminalControlMutationResult.Success();
				}
				var nativeError = Marshal.GetLastPInvokeError();
				if ( NotATerminal == nativeError ) {
					return TerminalControlMutationResult.Unavailable(
						string.Concat( endpoint.DisplayName, " is not a terminal." ),
						nativeError
					);
				}
				if ( InvalidArgument == nativeError ) {
					return TerminalControlMutationResult.Unavailable(
						string.Concat( "The terminal rejected one or more mode values for ", endpoint.DisplayName, "." ),
						nativeError
					);
				}
				return TerminalControlMutationResult.Failed(
					BuildErrorMessage( endpoint, "change terminal mode", nativeError ),
					nativeError
				);
			} finally {
				Marshal.FreeHGlobal( buffer );
			}
		}
	}

	private static bool TryAcquire(
		TerminalEndpoint endpoint,
		bool requireWrite,
		out FileDescriptorLease lease,
		out TerminalControlResult<TerminalEndpointObservation>? error
	) {
		if ( TerminalEndpointKind.FileDescriptor == endpoint.Kind ) {
			lease = new FileDescriptorLease( endpoint.FileDescriptor!.Value, false );
			error = null;
			return true;
		}

		var flags = GetOpenNoControllingTerminal() | GetOpenNonBlocking();
		var descriptor = NativeOpen(
			endpoint.Path!,
			( requireWrite ? OpenReadWrite : OpenReadOnly ) | flags
		);
		if ( 0 > descriptor && requireWrite ) {
			descriptor = NativeOpen( endpoint.Path!, OpenReadOnly | flags );
		}
		if ( 0 > descriptor ) {
			descriptor = NativeOpen( endpoint.Path!, OpenWriteOnly | flags );
		}
		if ( 0 > descriptor ) {
			var nativeError = Marshal.GetLastPInvokeError();
			lease = FileDescriptorLease.Empty;
			error = TerminalControlResult<TerminalEndpointObservation>.Failed(
				BuildErrorMessage( endpoint, "open terminal device", nativeError ),
				nativeError
			);
			return false;
		}
		lease = new FileDescriptorLease( descriptor, true );
		error = null;
		return true;
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

	private static string? TryGetTerminalName(
		int fileDescriptor
	) {
		var buffer = new byte[ 4096 ];
		var result = NativeGetTerminalName( fileDescriptor, buffer, (nuint)buffer.Length );
		if ( 0 != result ) {
			return null;
		}
		var terminator = Array.IndexOf( buffer, (byte)0 );
		if ( 0 > terminator ) {
			terminator = buffer.Length;
		}
		var pathname = System.Text.Encoding.UTF8.GetString( buffer, 0, terminator );
		return string.IsNullOrWhiteSpace( pathname ) ? null : pathname;
	}

	private static TerminalModeSnapshot CreateSnapshot(
		byte[] bytes,
		TermiosAbi abi,
		ulong inputSpeedCode,
		ulong outputSpeedCode
	) {
		var characters = new byte[ abi.ControlCharacterCount ];
		Array.Copy(
			bytes,
			abi.ControlCharacterOffset,
			characters,
			0,
			characters.Length
		);
		return TerminalModeSnapshot.CreatePosix(
			ReadFlag( bytes, 0, abi.FlagWidth ),
			ReadFlag( bytes, abi.FlagByteWidth, abi.FlagWidth ),
			ReadFlag( bytes, 2 * abi.FlagByteWidth, abi.FlagWidth ),
			ReadFlag( bytes, 3 * abi.FlagByteWidth, abi.FlagWidth ),
			characters,
			abi.DisabledControlCharacter,
			abi.FlagWidth,
			abi.LineDisciplineOffset.HasValue ? bytes[ abi.LineDisciplineOffset.Value ] : null,
			new TerminalSpeed( inputSpeedCode, GetBaudRate( inputSpeedCode ) ),
			new TerminalSpeed( outputSpeedCode, GetBaudRate( outputSpeedCode ) )
		);
	}

	private static byte[] CreateNativeBytes(
		TerminalModeSnapshot mode,
		TermiosAbi abi
	) {
		var bytes = new byte[ abi.StructureSize ];
		WriteFlag( bytes, 0, abi.FlagWidth, mode.InputFlags );
		WriteFlag( bytes, abi.FlagByteWidth, abi.FlagWidth, mode.OutputFlags );
		WriteFlag( bytes, 2 * abi.FlagByteWidth, abi.FlagWidth, mode.ControlFlags );
		WriteFlag( bytes, 3 * abi.FlagByteWidth, abi.FlagWidth, mode.LocalFlags );
		if ( abi.LineDisciplineOffset.HasValue && mode.LineDiscipline.HasValue ) {
			bytes[ abi.LineDisciplineOffset.Value ] = mode.LineDiscipline.Value;
		}
		for ( var index = 0; index < mode.ControlCharacters.Count; ++index ) {
			bytes[ abi.ControlCharacterOffset + index ] = mode.ControlCharacters[ index ];
		}
		WriteNativeSpeed( bytes, abi.InputSpeedOffset, abi.SpeedByteWidth, mode.InputSpeed!.Value.NativeCode );
		WriteNativeSpeed( bytes, abi.OutputSpeedOffset, abi.SpeedByteWidth, mode.OutputSpeed!.Value.NativeCode );
		return bytes;
	}

	private static ulong ReadFlag(
		byte[] bytes,
		int offset,
		int flagWidth
	) {
		return 32 == flagWidth
			? BitConverter.ToUInt32( bytes, offset )
			: BitConverter.ToUInt64( bytes, offset );
	}

	private static void WriteFlag(
		byte[] bytes,
		int offset,
		int flagWidth,
		ulong value
	) {
		if ( 32 == flagWidth ) {
			BitConverter.GetBytes( checked( (uint)value ) ).CopyTo( bytes, offset );
		} else {
			BitConverter.GetBytes( value ).CopyTo( bytes, offset );
		}
	}

	private static void WriteNativeSpeed(
		byte[] bytes,
		int offset,
		int width,
		ulong value
	) {
		if ( 4 == width ) {
			BitConverter.GetBytes( checked( (uint)value ) ).CopyTo( bytes, offset );
		} else {
			BitConverter.GetBytes( value ).CopyTo( bytes, offset );
		}
	}

	private static ulong? GetBaudRate(
		ulong nativeCode
	) {
		if ( OperatingSystem.IsMacOS() ) {
			return nativeCode;
		}
		return nativeCode switch {
			0 => 0,
			1 => 50,
			2 => 75,
			3 => 110,
			4 => 134,
			5 => 150,
			6 => 200,
			7 => 300,
			8 => 600,
			9 => 1200,
			10 => 1800,
			11 => 2400,
			12 => 4800,
			13 => 9600,
			14 => 19200,
			15 => 38400,
			4097 => 57600,
			4098 => 115200,
			4099 => 230400,
			4100 => 460800,
			4101 => 500000,
			4102 => 576000,
			4103 => 921600,
			4104 => 1000000,
			4105 => 1152000,
			4106 => 1500000,
			4107 => 2000000,
			4108 => 2500000,
			4109 => 3000000,
			4110 => 3500000,
			4111 => 4000000,
			_ => null
		};
	}

	private static TermiosAbi GetAbi() {
		if ( OperatingSystem.IsLinux() ) {
			return new TermiosAbi(
				LinuxTermiosSize,
				32,
				LinuxControlCharacterOffset,
				LinuxControlCharacterCount,
				16,
				LinuxInputSpeedOffset,
				LinuxOutputSpeedOffset,
				4,
				0
			);
		}
		if ( OperatingSystem.IsMacOS() ) {
			return new TermiosAbi(
				MacTermiosSize,
				64,
				MacControlCharacterOffset,
				MacControlCharacterCount,
				null,
				MacInputSpeedOffset,
				MacOutputSpeedOffset,
				8,
				0xff
			);
		}
		throw new PlatformNotSupportedException(
			"The POSIX terminal provider supports Linux and macOS."
		);
	}

	private static int GetOpenNoControllingTerminal() {
		return OperatingSystem.IsMacOS()
			? MacOpenNoControllingTerminal
			: LinuxOpenNoControllingTerminal;
	}

	private static int GetOpenNonBlocking() {
		return OperatingSystem.IsMacOS()
			? MacOpenNonBlocking
			: LinuxOpenNonBlocking;
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

	[DllImport( LibC, EntryPoint = "open", SetLastError = true )]
	private static extern int NativeOpen(
		[MarshalAs( UnmanagedType.LPUTF8Str )] string path,
		int flags
	);

	[DllImport( LibC, EntryPoint = "close", SetLastError = true )]
	private static extern int NativeClose(
		int fileDescriptor
	);

	[DllImport( LibC, EntryPoint = "isatty", SetLastError = true )]
	private static extern int NativeIsATerminal(
		int fileDescriptor
	);

	[DllImport( LibC, EntryPoint = "ttyname_r", SetLastError = true )]
	private static extern int NativeGetTerminalName(
		int fileDescriptor,
		byte[] buffer,
		nuint bufferLength
	);

	[DllImport( LibC, EntryPoint = "tcgetattr", SetLastError = true )]
	private static extern int NativeGetAttributes(
		int fileDescriptor,
		IntPtr attributes
	);

	[DllImport( LibC, EntryPoint = "tcsetattr", SetLastError = true )]
	private static extern int NativeSetAttributes(
		int fileDescriptor,
		int optionalActions,
		IntPtr attributes
	);

	[DllImport( LibC, EntryPoint = "cfgetispeed", SetLastError = false )]
	private static extern nuint NativeGetInputSpeed(
		IntPtr attributes
	);

	[DllImport( LibC, EntryPoint = "cfgetospeed", SetLastError = false )]
	private static extern nuint NativeGetOutputSpeed(
		IntPtr attributes
	);

	[DllImport( LibC, EntryPoint = "cfsetispeed", SetLastError = true )]
	private static extern int NativeSetInputSpeed(
		IntPtr attributes,
		nuint speed
	);

	[DllImport( LibC, EntryPoint = "cfsetospeed", SetLastError = true )]
	private static extern int NativeSetOutputSpeed(
		IntPtr attributes,
		nuint speed
	);

	private readonly record struct TermiosAbi(
		int StructureSize,
		int FlagWidth,
		int ControlCharacterOffset,
		int ControlCharacterCount,
		int? LineDisciplineOffset,
		int InputSpeedOffset,
		int OutputSpeedOffset,
		int SpeedByteWidth,
		byte DisabledControlCharacter
	) {
		public int FlagByteWidth {
			get {
				return this.FlagWidth / 8;
			}
		}
	}

	private sealed class FileDescriptorLease : IDisposable {
		private readonly bool ownsDescriptor;
		private bool disposed;

		public FileDescriptorLease(
			int fileDescriptor,
			bool ownsDescriptor
		) {
			this.FileDescriptor = fileDescriptor;
			this.ownsDescriptor = ownsDescriptor;
		}

		public static FileDescriptorLease Empty {
			get;
		} = new FileDescriptorLease( -1, false );

		public int FileDescriptor {
			get;
		}

		public void Dispose() {
			if ( this.disposed ) {
				return;
			}
			this.disposed = true;
			if ( this.ownsDescriptor ) {
				_ = NativeClose( this.FileDescriptor );
			}
		}
	}
}
