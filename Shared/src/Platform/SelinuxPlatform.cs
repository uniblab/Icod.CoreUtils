// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Shared.Platform;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>Represents the four conventional components of an SELinux security context.</summary>
public sealed record SelinuxContext( string User, string Role, string Type, string? Range ) {
	/// <summary>Parses a conventional SELinux context without interpreting MLS/MCS range syntax.</summary>
	public static bool TryParse( string value, out SelinuxContext? context ) {
		context = null;
		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var parts = value.Split( new[] { ':' }, 4, StringSplitOptions.None );
		if ( parts.Length < 3 || string.IsNullOrEmpty( parts[0] ) || string.IsNullOrEmpty( parts[1] ) || string.IsNullOrEmpty( parts[2] ) )
			return false;

		context = new SelinuxContext( parts[0], parts[1], parts[2], parts.Length == 4 ? parts[3] : null );
		return true;
	}

	/// <inheritdoc />
	public override string ToString() {
		return Range is null ? $"{User}:{Role}:{Type}" : $"{User}:{Role}:{Type}:{Range}";
	}
}

/// <summary>Result of attempting to replace the current process image under an SELinux execution context.</summary>
public readonly record struct SelinuxExecutionResult( int ExitCode, int ErrorNumber, string? Diagnostic );

/// <summary>Injectable SELinux/native process boundary shared by <c>chcon</c> and <c>runcon</c>.</summary>
public interface ISelinuxPlatform {
	/// <summary>Gets whether the host provides a usable SELinux native API.</summary>
	bool IsSupported { get; }
	/// <summary>Gets a diagnostic suitable for an unsupported host.</summary>
	string UnsupportedReason { get; }
	/// <summary>Determines whether SELinux is enabled by the running kernel.</summary>
	bool IsEnabled( out int errorNumber );
	/// <summary>Gets the current process context.</summary>
	bool TryGetCurrentContext( out string context, out int errorNumber );
	/// <summary>Gets a file or link security context.</summary>
	bool TryGetFileContext( string path, bool dereference, out string context, out int errorNumber );
	/// <summary>Sets a file or link security context.</summary>
	bool TrySetFileContext( string path, string context, bool dereference, out int errorNumber );
	/// <summary>Asks libselinux to validate a context.</summary>
	bool TryValidateContext( string context, out int errorNumber );
	/// <summary>Computes a process transition context from source and executable contexts.</summary>
	bool TryComputeProcessContext( string sourceContext, string executableContext, out string context, out int errorNumber );
	/// <summary>Sets the execution context and replaces the process image with the requested command.</summary>
	SelinuxExecutionResult ExecuteWithContext( string context, IReadOnlyList<string> command, bool searchPath );
	/// <summary>Formats a native errno value.</summary>
	string DescribeError( int errorNumber );
}

/// <summary>Linux implementation backed by libselinux and libc; no host command is invoked.</summary>
public sealed class NativeSelinuxPlatform : ISelinuxPlatform {
	private const string LibSelinux = "libselinux.so.1";
	private const string LibC = "libc";
	private const int ENoEnt = 2;
	private readonly bool available;

	/// <summary>Initializes the native SELinux provider.</summary>
	public NativeSelinuxPlatform() {
		if ( !OperatingSystem.IsLinux() ) {
			available = false;
			UnsupportedReason = "SELinux context operations are supported only on Linux";
			return;
		}

		if ( NativeLibrary.TryLoad( LibSelinux, out var handle ) ) {
			available = true;
			NativeLibrary.Free( handle );
			UnsupportedReason = string.Empty;
		} else {
			available = false;
			UnsupportedReason = $"{LibSelinux} is unavailable";
		}
	}

	/// <inheritdoc />
	public bool IsSupported => available;
	/// <inheritdoc />
	public string UnsupportedReason { get; }

	/// <inheritdoc />
	public bool IsEnabled( out int errorNumber ) {
		errorNumber = 0;
		if ( !available )
			return false;
		try {
			var result = is_selinux_enabled();
			if ( result > 0 )
				return true;
			errorNumber = result < 0 ? Marshal.GetLastPInvokeError() : 0;
			return false;
		} catch ( DllNotFoundException ) {
			return false;
		} catch ( EntryPointNotFoundException ) {
			return false;
		}
	}

	/// <inheritdoc />
	public bool TryGetCurrentContext( out string context, out int errorNumber ) {
		context = string.Empty;
		errorNumber = 0;
		if ( getcon( out var pointer ) < 0 ) {
			errorNumber = Marshal.GetLastPInvokeError();
			return false;
		}
		context = ConsumeContext( pointer );
		return true;
	}

	/// <inheritdoc />
	public bool TryGetFileContext( string path, bool dereference, out string context, out int errorNumber ) {
		context = string.Empty;
		errorNumber = 0;
		IntPtr pointer;
		var result = dereference ? getfilecon( path, out pointer ) : lgetfilecon( path, out pointer );
		if ( result < 0 ) {
			errorNumber = Marshal.GetLastPInvokeError();
			return false;
		}
		context = ConsumeContext( pointer );
		return true;
	}

	/// <inheritdoc />
	public bool TrySetFileContext( string path, string context, bool dereference, out int errorNumber ) {
		errorNumber = 0;
		var result = dereference ? setfilecon( path, context ) : lsetfilecon( path, context );
		if ( result == 0 )
			return true;
		errorNumber = Marshal.GetLastPInvokeError();
		return false;
	}

	/// <inheritdoc />
	public bool TryValidateContext( string context, out int errorNumber ) {
		errorNumber = 0;
		if ( security_check_context( context ) == 0 )
			return true;
		errorNumber = Marshal.GetLastPInvokeError();
		return false;
	}

	/// <inheritdoc />
	public bool TryComputeProcessContext( string sourceContext, string executableContext, out string context, out int errorNumber ) {
		context = string.Empty;
		errorNumber = 0;
		var processClass = string_to_security_class( "process" );
		if ( processClass == 0 ) {
			errorNumber = Marshal.GetLastPInvokeError();
			return false;
		}
		if ( security_compute_create( sourceContext, executableContext, processClass, out var pointer ) < 0 ) {
			errorNumber = Marshal.GetLastPInvokeError();
			return false;
		}
		context = ConsumeContext( pointer );
		return true;
	}

	/// <inheritdoc />
	public SelinuxExecutionResult ExecuteWithContext( string context, IReadOnlyList<string> command, bool searchPath ) {
		if ( command.Count == 0 )
			return new SelinuxExecutionResult( 125, 0, "no command specified" );
		if ( setexeccon( context ) != 0 ) {
			var error = Marshal.GetLastPInvokeError();
			return new SelinuxExecutionResult( 125, error, $"failed to set execution context: {DescribeError( error )}" );
		}

		var pointers = new IntPtr[command.Count + 1];
		try {
			for ( var i = 0; i < command.Count; i++ )
				pointers[i] = Marshal.StringToCoTaskMemUTF8( command[i] );
			pointers[command.Count] = IntPtr.Zero;

			var argv = Marshal.AllocHGlobal( IntPtr.Size * pointers.Length );
			try {
				for ( var i = 0; i < pointers.Length; i++ )
					Marshal.WriteIntPtr( argv, i * IntPtr.Size, pointers[i] );
				if ( searchPath )
					execvp( command[0], argv );
				else
					execv( command[0], argv );
				var error = Marshal.GetLastPInvokeError();
				var status = error == ENoEnt ? 127 : 126;
				return new SelinuxExecutionResult( status, error, $"failed to execute '{command[0]}': {DescribeError( error )}" );
			} finally {
				Marshal.FreeHGlobal( argv );
			}
		} finally {
			// setexeccon affects the next exec only. If allocation or exec fails, clear the pending context.
			_ = setexeccon( null );
			foreach ( var pointer in pointers ) {
				if ( pointer != IntPtr.Zero )
					Marshal.FreeCoTaskMem( pointer );
			}
		}
	}

	/// <inheritdoc />
	public string DescribeError( int errorNumber ) {
		if ( errorNumber == 0 )
			return "SELinux is disabled or unavailable on this kernel";
		var pointer = strerror( errorNumber );
		return pointer == IntPtr.Zero ? $"native error {errorNumber}" : Marshal.PtrToStringUTF8( pointer ) ?? $"native error {errorNumber}";
	}

	private static string ConsumeContext( IntPtr pointer ) {
		try {
			return Marshal.PtrToStringUTF8( pointer ) ?? string.Empty;
		} finally {
			if ( pointer != IntPtr.Zero )
				freecon( pointer );
		}
	}

#pragma warning disable SYSLIB1054 // LibraryImport would require changing the shared project unsafe/source-generation policy.
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int is_selinux_enabled();
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int getcon( out IntPtr context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int getfilecon( [MarshalAs( UnmanagedType.LPUTF8Str )] string path, out IntPtr context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int lgetfilecon( [MarshalAs( UnmanagedType.LPUTF8Str )] string path, out IntPtr context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int setfilecon( [MarshalAs( UnmanagedType.LPUTF8Str )] string path, [MarshalAs( UnmanagedType.LPUTF8Str )] string context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int lsetfilecon( [MarshalAs( UnmanagedType.LPUTF8Str )] string path, [MarshalAs( UnmanagedType.LPUTF8Str )] string context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int security_check_context( [MarshalAs( UnmanagedType.LPUTF8Str )] string context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern ushort string_to_security_class( [MarshalAs( UnmanagedType.LPUTF8Str )] string name );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int security_compute_create( [MarshalAs( UnmanagedType.LPUTF8Str )] string source, [MarshalAs( UnmanagedType.LPUTF8Str )] string target, ushort targetClass, out IntPtr context );
	[DllImport( LibSelinux, SetLastError = true )]
	private static extern int setexeccon( [MarshalAs( UnmanagedType.LPUTF8Str )] string? context );
	[DllImport( LibSelinux )]
	private static extern void freecon( IntPtr context );
	[DllImport( LibC, SetLastError = true )]
	private static extern int execv( [MarshalAs( UnmanagedType.LPUTF8Str )] string file, IntPtr argv );
	[DllImport( LibC, SetLastError = true )]
	private static extern int execvp( [MarshalAs( UnmanagedType.LPUTF8Str )] string file, IntPtr argv );
	[DllImport( LibC )]
	private static extern IntPtr strerror( int errorNumber );
#pragma warning restore SYSLIB1054

}
