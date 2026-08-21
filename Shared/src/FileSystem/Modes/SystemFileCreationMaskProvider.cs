using System.Runtime.InteropServices;

using FrameworkModes = Icod.CommandFramework.FileSystem.Modes;

namespace Icod.CoreUtils.Shared.FileSystem.Modes;

/// <summary>
/// Supplies the process file-creation mask used by commands that implement GNU mode semantics.
/// </summary>
/// <remarks>
/// Linux is observed through <c>/proc/self/status</c> without changing process state. Darwin and
/// best-effort FreeBSD use the native <c>umask</c> query-and-restore idiom under a process-local
/// lock because those systems do not expose a non-mutating query. Windows reports an empty mask.
/// </remarks>
public interface IFileCreationMaskProvider {
	/// <summary>Gets the current ordinary-permission creation mask.</summary>
	/// <returns>The current mask.</returns>
	FrameworkModes.FileCreationMask GetCurrentMask();
}

/// <summary>
/// Reads the host process file-creation mask for GNU-compatible command front ends.
/// </summary>
public sealed class SystemFileCreationMaskProvider : IFileCreationMaskProvider {
	private static readonly object NativeUmaskLock = new();

	/// <summary>Gets the shared system provider.</summary>
	public static SystemFileCreationMaskProvider Instance { get; } = new();

	/// <summary>Initializes the system provider.</summary>
	public SystemFileCreationMaskProvider() { }

	/// <inheritdoc/>
	public FrameworkModes.FileCreationMask GetCurrentMask() {
		if ( OperatingSystem.IsWindows() ) {
			return FrameworkModes.FileCreationMask.None;
		}
		if ( OperatingSystem.IsLinux() && TryReadLinuxProcMask( out var linuxMask ) ) {
			return linuxMask;
		}
		if ( OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() ) {
			return QueryAndRestoreUnixMask( NativeUmaskUnix );
		}
		if ( OperatingSystem.IsMacOS() ) {
			return QueryAndRestoreUnixMask( NativeUmaskDarwin );
		}
		return FrameworkModes.FileCreationMask.None;
	}

	private static FrameworkModes.FileCreationMask QueryAndRestoreUnixMask( Func<uint, uint> nativeUmask ) {
		lock ( NativeUmaskLock ) {
			var previous = nativeUmask( 0 );
			_ = nativeUmask( previous );
			return new FrameworkModes.FileCreationMask( checked((int)(previous & 0x01ffU)) );
		}
	}

	private static bool TryReadLinuxProcMask( out FrameworkModes.FileCreationMask mask ) {
		mask = FrameworkModes.FileCreationMask.None;
		try {
			foreach ( var line in File.ReadLines( "/proc/self/status" ) ) {
				if ( !line.StartsWith( "Umask:", StringComparison.Ordinal ) ) {
					continue;
				}
				var value = line[ "Umask:".Length.. ].Trim();
				if ( value.Length == 0 ) {
					return false;
				}
				var parsed = Convert.ToInt32( value, 8 );
				mask = new FrameworkModes.FileCreationMask( parsed & 0x01ff );
				return true;
			}
		} catch ( IOException ) {
			return false;
		} catch ( UnauthorizedAccessException ) {
			return false;
		} catch ( FormatException ) {
			return false;
		} catch ( OverflowException ) {
			return false;
		}
		return false;
	}

	[DllImport( "libc", EntryPoint = "umask", SetLastError = false )]
	private static extern uint NativeUmaskUnix( uint mask );

	[DllImport( "libSystem.dylib", EntryPoint = "umask", SetLastError = false )]
	private static extern uint NativeUmaskDarwin( uint mask );
}
