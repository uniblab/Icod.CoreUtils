namespace Icod.CoreUtils.Shared.Platform;

using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>
/// Provides BCL-first platform capability detection and controlled operations.
/// </summary>
public static class PlatformCapabilities {
	/// <summary>
	/// Gets whether the current runtime can attempt the supplied feature.
	/// </summary>
	public static bool IsSupported(
		PlatformFeature feature
	) {
		return feature switch {
			PlatformFeature.UnixFileModes => !OperatingSystem.IsWindows(),
			PlatformFeature.SymbolicLinks => true,
			PlatformFeature.HardLinks => IsHardLinkSupported(),
			PlatformFeature.FileOwnership => false,
			PlatformFeature.SecurityContexts => IsSecurityContextSupported(),
			PlatformFeature.ProcessSignals => false,
			PlatformFeature.EffectiveUserIdentity => false,
			PlatformFeature.FileSystemStatistics => true,
			_ => false
		};
	}

	/// <summary>
	/// Attempts to read Unix permission bits through the BCL.
	/// </summary>
	public static PlatformOperationResult<UnixFileMode> TryGetUnixFileMode(
		string path
	) {
		if ( OperatingSystem.IsWindows() ) {
			return PlatformOperationResult<UnixFileMode>.Unsupported(
				"Unix file modes are not supported on Windows."
			);
		}
		try {
			return PlatformOperationResult<UnixFileMode>.Success(
				File.GetUnixFileMode(
					path
				)
			);
		} catch ( Exception ex ) {
			return PlatformOperationResult<UnixFileMode>.Failure(
				ex.Message,
				ex
			);
		}
	}
	/// <summary>
	/// Attempts to set Unix permission bits through the BCL.
	/// </summary>
	public static PlatformOperationResult TrySetUnixFileMode(
		string path,
		UnixFileMode mode
	) {
		if ( OperatingSystem.IsWindows() ) {
			return PlatformOperationResult.Unsupported(
				"Unix file modes are not supported on Windows."
			);
		}
		try {
			File.SetUnixFileMode(
				path,
				mode
			);
			return PlatformOperationResult.Success();
		} catch ( Exception ex ) {
			return PlatformOperationResult.Failure(
				ex.Message,
				ex
			);
		}
	}
	/// <summary>
	/// Attempts to create a hard link through the native operating-system API.
	/// </summary>
	public static PlatformOperationResult TryCreateHardLink(
		string linkPath,
		string existingFilePath
	) {
		if ( !IsHardLinkSupported() ) {
			return PlatformOperationResult.Unsupported(
				"Hard links are not supported by the current platform layer on this operating system."
			);
		}
		try {
			bool succeeded;
			if ( OperatingSystem.IsWindows() ) {
				succeeded = CreateHardLinkWindows(
					linkPath,
					existingFilePath,
					IntPtr.Zero
				);
			} else if ( OperatingSystem.IsMacOS() ) {
				succeeded = 0 == CreateHardLinkMacOS(
					existingFilePath,
					linkPath
				);
			} else {
				succeeded = 0 == CreateHardLinkUnix(
					existingFilePath,
					linkPath
				);
			}

			if ( succeeded ) {
				return PlatformOperationResult.Success();
			}
			var error = Marshal.GetLastPInvokeError();
			var exception = new Win32Exception(
				error
			);
			return PlatformOperationResult.Failure(
				exception.Message,
				exception
			);
		} catch ( DllNotFoundException ex ) {
			return PlatformOperationResult.Unsupported(
				ex.Message
			);
		} catch ( EntryPointNotFoundException ex ) {
			return PlatformOperationResult.Unsupported(
				ex.Message
			);
		} catch ( PlatformNotSupportedException ex ) {
			return PlatformOperationResult.Unsupported(
				ex.Message
			);
		} catch ( Exception ex ) {
			return PlatformOperationResult.Failure(
				ex.Message,
				ex
			);
		}
	}
	/// <summary>
	/// Attempts to create a symbolic link through the BCL.
	/// </summary>
	public static PlatformOperationResult TryCreateSymbolicLink(
		string linkPath,
		string targetPath,
		bool targetIsDirectory
	) {
		try {
			if ( targetIsDirectory ) {
				Directory.CreateSymbolicLink(
					linkPath,
					targetPath
				);
			} else {
				File.CreateSymbolicLink(
					linkPath,
					targetPath
				);
			}
			return PlatformOperationResult.Success();
		} catch ( PlatformNotSupportedException ex ) {
			return PlatformOperationResult.Unsupported(
				ex.Message
			);
		} catch ( Exception ex ) {
			return PlatformOperationResult.Failure(
				ex.Message,
				ex
			);
		}
	}
	/// <summary>
	/// Attempts to read a symbolic-link target through the BCL.
	/// </summary>
	public static PlatformOperationResult<string?> TryGetLinkTarget(
		string path,
		bool isDirectory
	) {
		try {
			FileSystemInfo info = isDirectory
				? new DirectoryInfo( path )
				: new FileInfo( path )
			;
			return PlatformOperationResult<string?>.Success(
				info.LinkTarget
			);
		} catch ( PlatformNotSupportedException ex ) {
			return PlatformOperationResult<string?>.Unsupported(
				ex.Message
			);
		} catch ( Exception ex ) {
			return PlatformOperationResult<string?>.Failure(
				ex.Message,
				ex
			);
		}
	}

	/// <summary>
	/// Attempts to resolve the final symbolic-link target through the BCL.
	/// </summary>
	public static PlatformOperationResult<string?> TryResolveLinkTarget(
		string path,
		bool isDirectory,
		bool returnFinalTarget = true
	) {
		try {
			FileSystemInfo info = isDirectory
				? new DirectoryInfo( path )
				: new FileInfo( path )
			;
			return PlatformOperationResult<string?>.Success(
				info.ResolveLinkTarget(
					returnFinalTarget
				)?.FullName
			);
		} catch ( PlatformNotSupportedException ex ) {
			return PlatformOperationResult<string?>.Unsupported(
				ex.Message
			);
		} catch ( Exception ex ) {
			return PlatformOperationResult<string?>.Failure(
				ex.Message,
				ex
			);
		}
	}
	/// <summary>
	/// Returns a controlled unsupported result for ownership changes until a platform implementation is supplied.
	/// </summary>
	public static PlatformOperationResult TrySetOwnership(
		string path,
		string? owner,
		string? group
	) {
		_ = path;
		_ = owner;
		_ = group;
		return PlatformOperationResult.Unsupported(
			"File ownership changes are not available through the current shared platform layer."
		);
	}
	/// <summary>
	/// Attempts to set a file security context through the shared SELinux provider.
	/// </summary>
	public static PlatformOperationResult TrySetSecurityContext(
		string path,
		string context
	) {
		var platform = new NativeSelinuxPlatform();
		if ( !platform.IsSupported )
			return PlatformOperationResult.Unsupported( platform.UnsupportedReason );
		if ( !platform.IsEnabled( out var enabledError ) )
			return PlatformOperationResult.Unsupported(
				enabledError == 0
					? "SELinux is disabled on this kernel."
					: $"SELinux is unavailable: {platform.DescribeError( enabledError )}"
			);
		if ( !platform.TryValidateContext( context, out var validationError ) )
			return PlatformOperationResult.Failure(
				$"Invalid SELinux security context: {platform.DescribeError( validationError )}"
			);
		if ( platform.TrySetFileContext( path, context, true, out var setError ) )
			return PlatformOperationResult.Success();
		return PlatformOperationResult.Failure(
			$"Unable to set SELinux security context: {platform.DescribeError( setError )}"
		);
	}
	private static bool IsSecurityContextSupported() {
		var platform = new NativeSelinuxPlatform();
		return platform.IsSupported && platform.IsEnabled( out _ );
	}
	private static bool IsHardLinkSupported() {
		return (
			OperatingSystem.IsWindows()
			|| OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD()
		);
	}
#pragma warning disable SYSLIB1054 // LibraryImport requires unsafe project settings; keep this helper self-contained.

	[DllImport(
		"kernel32.dll",
		EntryPoint = "CreateHardLinkW",
		CharSet = CharSet.Unicode,
		ExactSpelling = true,
		SetLastError = true
	)]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool CreateHardLinkWindows(
		string fileName,
		string existingFileName,
		IntPtr securityAttributes
	);
	[DllImport(
		"libc",
		EntryPoint = "link",
		ExactSpelling = true,
		SetLastError = true
	)]
	private static extern int CreateHardLinkUnix(
		[MarshalAs( UnmanagedType.LPUTF8Str )] string existingFilePath,
		[MarshalAs( UnmanagedType.LPUTF8Str )] string linkPath
	);
	[DllImport(
		"libSystem.B.dylib",
		EntryPoint = "link",
		ExactSpelling = true,
		SetLastError = true
	)]
	private static extern int CreateHardLinkMacOS(
		[MarshalAs( UnmanagedType.LPUTF8Str )] string existingFilePath,
		[MarshalAs( UnmanagedType.LPUTF8Str )] string linkPath
	);
#pragma warning restore SYSLIB1054

}
