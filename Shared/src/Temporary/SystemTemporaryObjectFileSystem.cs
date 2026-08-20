using Path = global::System.IO.Path;
namespace Icod.CoreUtils.Shared.Temporary;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>Provides host filesystem operations for secure temporary objects.</summary>
public sealed class SystemTemporaryObjectFileSystem : ITemporaryObjectFileSystem {
	private const int ErrorFileNotFound = 2;
	private const int ErrorPathNotFound = 3;
	private const int ErrorFileExists = 80;
	private const int ErrorAlreadyExists = 183;
	private const int ErrorSharingViolation = 32;
	private const int ErrorOverflowLinux = 75;
	private const int ErrorOverflowBsd = 84;
	private const int ErrorExists = 17;
	private const uint UnixUserReadWriteExecute = 0x1c0;
	private const int StatBufferSize = 512;
	private const uint GenericReadShare = 0x00000001;
	private const uint GenericWriteShare = 0x00000002;
	private const uint DeleteShare = 0x00000004;
	private const uint OpenExisting = 3;
	private const uint FileFlagBackupSemantics = 0x02000000;
	private const uint FileFlagOpenReparsePoint = 0x00200000;

	/// <summary>Gets the shared host filesystem provider.</summary>
	public static SystemTemporaryObjectFileSystem Instance { get; } = new();

	private SystemTemporaryObjectFileSystem() {
	}

	/// <inheritdoc/>
	public TemporaryObjectAttemptResult TryCreate(
		string path,
		TemporaryObjectKind kind
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		return kind switch {
			TemporaryObjectKind.File => TryCreateFile( path ),
			TemporaryObjectKind.Directory => TryCreateDirectory( path ),
			TemporaryObjectKind.NameOnly => TryReserveName( path ),
			_ => TemporaryObjectAttemptResult.Failed( "unknown temporary-object kind" )
		};
	}

	/// <inheritdoc/>
	public bool TryDelete(
		string path,
		TemporaryObjectKind kind,
		out string? errorMessage
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		errorMessage = null;
		try {
			switch ( kind ) {
				case TemporaryObjectKind.File:
					File.Delete( path );
					return true;
				case TemporaryObjectKind.Directory:
					Directory.Delete( path );
					return true;
				case TemporaryObjectKind.NameOnly:
					return true;
				default:
					errorMessage = "unknown temporary-object kind";
					return false;
			}
		} catch ( FileNotFoundException ) {
			return true;
		} catch ( DirectoryNotFoundException ) {
			return true;
		} catch ( Exception exception ) when ( IsExpectedFileSystemException( exception ) ) {
			errorMessage = exception.Message;
			return false;
		}
	}

	private static TemporaryObjectAttemptResult TryCreateFile( string path ) {
		FileStream stream;
		try {
			var options = new FileStreamOptions {
				Mode = FileMode.CreateNew,
				Access = FileAccess.ReadWrite,
				Share = FileShare.None,
				BufferSize = 1,
				Options = FileOptions.None
			};
			if ( !OperatingSystem.IsWindows() ) {
				options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
			}
			stream = new FileStream( path, options );
		} catch ( IOException exception ) {
			if ( IsCollisionError( exception.HResult ) ) {
				return TemporaryObjectAttemptResult.Collided();
			}
			var existing = TryReserveName( path );
			return TemporaryObjectAttemptStatus.Collision == existing.Status
				? TemporaryObjectAttemptResult.Collided()
				: TemporaryObjectAttemptResult.Failed( exception.Message );
		} catch ( Exception exception ) when ( IsExpectedFileSystemException( exception ) ) {
			return TemporaryObjectAttemptResult.Failed( exception.Message );
		}

		try {
			stream.Dispose();
			return TemporaryObjectAttemptResult.Succeeded();
		} catch ( Exception exception ) when ( IsExpectedFileSystemException( exception ) ) {
			try {
				File.Delete( path );
			} catch ( Exception cleanupException ) when ( IsExpectedFileSystemException( cleanupException ) ) {
				// Preserve the close failure as the primary diagnostic.
			}
			return TemporaryObjectAttemptResult.Failed( exception.Message );
		}
	}

	private static TemporaryObjectAttemptResult TryCreateDirectory( string path ) {
		if ( OperatingSystem.IsWindows() ) {
			if ( NativeMethods.CreateDirectoryWindows( path, IntPtr.Zero ) ) {
				return TemporaryObjectAttemptResult.Succeeded();
			}
			return FromNativeError( Marshal.GetLastPInvokeError() );
		}

		int result;
		if ( OperatingSystem.IsMacOS() ) {
			result = NativeMethods.MkdirMacOs( path, UnixUserReadWriteExecute );
		} else if ( OperatingSystem.IsFreeBSD() ) {
			// Best effort: FreeBSD exposes the POSIX mkdir ABI through libc.
			result = NativeMethods.MkdirFreeBsd( path, UnixUserReadWriteExecute );
		} else if ( OperatingSystem.IsLinux() ) {
			result = NativeMethods.MkdirLinux( path, UnixUserReadWriteExecute );
		} else {
			return TemporaryObjectAttemptResult.Failed(
				"exclusive temporary-directory creation is unsupported on this platform"
			);
		}
		return 0 == result
			? TemporaryObjectAttemptResult.Succeeded()
			: FromNativeError( Marshal.GetLastPInvokeError() );
	}

	private static TemporaryObjectAttemptResult TryReserveName( string path ) {
		if ( OperatingSystem.IsWindows() ) {
			using var handle = NativeMethods.OpenReparsePointWindows(
				path,
				0,
				GenericReadShare | GenericWriteShare | DeleteShare,
				IntPtr.Zero,
				OpenExisting,
				FileFlagBackupSemantics | FileFlagOpenReparsePoint,
				IntPtr.Zero
			);
			if ( !handle.IsInvalid ) {
				return TemporaryObjectAttemptResult.Collided();
			}
			var error = Marshal.GetLastPInvokeError();
			if ( ErrorSharingViolation == error ) {
				return TemporaryObjectAttemptResult.Collided();
			}
			if ( ( ErrorFileNotFound == error ) || ( ErrorPathNotFound == error ) ) {
				var nonDirectoryAncestor = FindExistingNonDirectoryAncestor( path );
				return null == nonDirectoryAncestor
					? TemporaryObjectAttemptResult.Succeeded()
					: TemporaryObjectAttemptResult.Failed(
						string.Concat( "path component is not a directory: ", nonDirectoryAncestor )
					);
			}
			return TemporaryObjectAttemptResult.Failed( GetNativeErrorMessage( error ) );
		}

		if (
			OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
		) {
			return TryReserveNameUnix( path );
		}
		return TryReserveNamePortable( path );
	}

	private static string? FindExistingNonDirectoryAncestor( string path ) {
		var ancestor = Path.GetDirectoryName( path );
		while ( !string.IsNullOrEmpty( ancestor ) ) {
			if ( File.Exists( ancestor ) ) {
				return ancestor;
			}
			if ( Directory.Exists( ancestor ) ) {
				return null;
			}
			var parent = Path.GetDirectoryName( ancestor );
			if ( string.Equals( parent, ancestor, StringComparison.Ordinal ) ) {
				break;
			}
			ancestor = parent;
		}
		return null;
	}

	private static TemporaryObjectAttemptResult TryReserveNameUnix( string path ) {
		var buffer = Marshal.AllocHGlobal( StatBufferSize );
		try {
			int result;
			if ( OperatingSystem.IsMacOS() ) {
				result = NativeMethods.LstatMacOs( path, buffer );
			} else if ( OperatingSystem.IsFreeBSD() ) {
				// Best effort: only the return status is consumed; no struct stat fields are interpreted.
				result = NativeMethods.LstatFreeBsd( path, buffer );
			} else {
				result = NativeMethods.LstatLinux( path, buffer );
			}
			if ( 0 == result ) {
				return TemporaryObjectAttemptResult.Collided();
			}
			var error = Marshal.GetLastPInvokeError();
			if ( ErrorFileNotFound == error ) {
				return TemporaryObjectAttemptResult.Succeeded();
			}
			if ( ( ErrorOverflowLinux == error ) || ( ErrorOverflowBsd == error ) ) {
				return TemporaryObjectAttemptResult.Collided();
			}
			return TemporaryObjectAttemptResult.Failed( GetNativeErrorMessage( error ) );
		} finally {
			Marshal.FreeHGlobal( buffer );
		}
	}

	private static TemporaryObjectAttemptResult TryReserveNamePortable( string path ) {
		try {
			_ = File.GetAttributes( path );
			return TemporaryObjectAttemptResult.Collided();
		} catch ( FileNotFoundException ) {
			return TemporaryObjectAttemptResult.Succeeded();
		} catch ( DirectoryNotFoundException ) {
			return TemporaryObjectAttemptResult.Succeeded();
		} catch ( Exception exception ) when ( IsExpectedFileSystemException( exception ) ) {
			return TemporaryObjectAttemptResult.Failed( exception.Message );
		}
	}

	private static TemporaryObjectAttemptResult FromNativeError( int error ) {
		return IsCollisionError( error )
			? TemporaryObjectAttemptResult.Collided()
			: TemporaryObjectAttemptResult.Failed( GetNativeErrorMessage( error ) );
	}

	private static bool IsCollisionError( int error ) {
		var nativeError = error & 0xffff;
		return ( ErrorExists == nativeError )
			|| ( ErrorFileExists == nativeError )
			|| ( ErrorAlreadyExists == nativeError );
	}

	private static string GetNativeErrorMessage( int error ) {
		return new Win32Exception( error ).Message;
	}

	private static bool IsExpectedFileSystemException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException;
	}

	private static class NativeMethods {
		/// <summary>
		/// Creates directory windows.
		/// </summary>
		[DllImport(
			"kernel32.dll",
			EntryPoint = "CreateDirectoryW",
			CharSet = CharSet.Unicode,
			SetLastError = true
		)]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern bool CreateDirectoryWindows(
			string path,
			IntPtr securityAttributes
		);

		/// <summary>
		/// Performs the open reparse point windows operation.
		/// </summary>
		[DllImport(
			"kernel32.dll",
			EntryPoint = "CreateFileW",
			CharSet = CharSet.Unicode,
			SetLastError = true
		)]
		internal static extern SafeFileHandle OpenReparsePointWindows(
			string path,
			uint desiredAccess,
			uint shareMode,
			IntPtr securityAttributes,
			uint creationDisposition,
			uint flagsAndAttributes,
			IntPtr templateFile
		);

		/// <summary>
		/// Performs the mkdir linux operation.
		/// </summary>
		[DllImport(
			"libc",
			EntryPoint = "mkdir",
			CharSet = CharSet.Ansi,
			SetLastError = true
		)]
		internal static extern int MkdirLinux( string path, uint mode );

		/// <summary>
		/// Performs the mkdir mac os operation.
		/// </summary>
		[DllImport(
			"libSystem.B.dylib",
			EntryPoint = "mkdir",
			CharSet = CharSet.Ansi,
			SetLastError = true
		)]
		internal static extern int MkdirMacOs( string path, uint mode );

		/// <summary>
		/// Performs the mkdir free bsd operation.
		/// </summary>
		[DllImport(
			"libc",
			EntryPoint = "mkdir",
			CharSet = CharSet.Ansi,
			SetLastError = true
		)]
		internal static extern int MkdirFreeBsd( string path, uint mode );

		/// <summary>
		/// Performs the lstat linux operation.
		/// </summary>
		[DllImport(
			"libc",
			EntryPoint = "lstat",
			CharSet = CharSet.Ansi,
			SetLastError = true
		)]
		internal static extern int LstatLinux( string path, IntPtr buffer );

		/// <summary>
		/// Performs the lstat mac os operation.
		/// </summary>
		[DllImport(
			"libSystem.B.dylib",
			EntryPoint = "lstat",
			CharSet = CharSet.Ansi,
			SetLastError = true
		)]
		internal static extern int LstatMacOs( string path, IntPtr buffer );

		/// <summary>
		/// Performs the lstat free bsd operation.
		/// </summary>
		[DllImport(
			"libc",
			EntryPoint = "lstat",
			CharSet = CharSet.Ansi,
			SetLastError = true
		)]
		internal static extern int LstatFreeBsd( string path, IntPtr buffer );
	}
}
