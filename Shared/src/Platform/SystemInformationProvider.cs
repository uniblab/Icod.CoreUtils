namespace Icod.CoreUtils.Shared.Platform;

using System.Net;
using System.Runtime.InteropServices;

/// <summary>Describes the operating system information displayed by <c>uname</c>.</summary>
public sealed record SystemInformationSnapshot(
	string KernelName,
	string NodeName,
	string KernelRelease,
	string KernelVersion,
	string Machine,
	string Processor,
	string HardwarePlatform,
	string OperatingSystem
);

/// <summary>Supplies operating-system identity information.</summary>
public interface ISystemInformationProvider {
	/// <summary>Gets the current operating-system identity.</summary>
	ValueTask<SystemInformationSnapshot> GetAsync( CancellationToken cancellationToken = default );
}

/// <summary>Gets operating-system identity information from the current host.</summary>
public sealed class SystemInformationProvider : ISystemInformationProvider {
	private const int LinuxFieldLength = 65;
	private const int LinuxFieldCount = 6;

	/// <summary>Gets the process-wide provider instance.</summary>
	public static SystemInformationProvider Instance { get; } = new();

	private SystemInformationProvider() { }

	/// <inheritdoc />
	public ValueTask<SystemInformationSnapshot> GetAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult( OperatingSystem.IsLinux() ? ReadLinux() : ReadPortable() );
	}

	private static SystemInformationSnapshot ReadLinux() {
		var buffer = Marshal.AllocHGlobal( LinuxFieldLength * LinuxFieldCount );
		try {
			for ( var index = 0; index < LinuxFieldLength * LinuxFieldCount; index++ ) {
				Marshal.WriteByte( buffer, index, 0 );
			}
			if ( 0 != NativeMethods.Uname( buffer ) ) {
				return ReadPortable();
			}
			return new SystemInformationSnapshot(
				ReadField( buffer, 0 ),
				ReadField( buffer, 1 ),
				ReadField( buffer, 2 ),
				ReadField( buffer, 3 ),
				ReadField( buffer, 4 ),
				"unknown",
				"unknown",
				"GNU/Linux"
			);
		} finally {
			Marshal.FreeHGlobal( buffer );
		}
	}

	private static string ReadField( IntPtr buffer, int fieldIndex ) {
		var bytes = new byte[ LinuxFieldLength ];
		Marshal.Copy( IntPtr.Add( buffer, fieldIndex * LinuxFieldLength ), bytes, 0, bytes.Length );
		var length = Array.IndexOf( bytes, (byte)0 );
		if ( 0 > length ) length = bytes.Length;
		return System.Text.Encoding.UTF8.GetString( bytes, 0, length );
	}

	private static SystemInformationSnapshot ReadPortable() {
		var machine = RuntimeInformation.OSArchitecture switch {
			Architecture.X64 => "x86_64",
			Architecture.X86 => "i686",
			Architecture.Arm64 => "aarch64",
			Architecture.Arm => "arm",
			Architecture.Armv6 => "armv6",
			Architecture.Wasm => "wasm",
			Architecture.S390x => "s390x",
			Architecture.LoongArch64 => "loongarch64",
			Architecture.Ppc64le => "ppc64le",
			Architecture.RiscV64 => "riscv64",
			_ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
		};
		var kernelName = OperatingSystem.IsWindows()
			? "Windows_NT"
			: OperatingSystem.IsMacOS() ? "Darwin" : RuntimeInformation.OSDescription.Split( ' ', 2 )[0];
		var operatingSystem = OperatingSystem.IsWindows()
			? "Windows"
			: OperatingSystem.IsMacOS() ? "Darwin" : RuntimeInformation.OSDescription;
		return new SystemInformationSnapshot(
			kernelName,
			Dns.GetHostName(),
			Environment.OSVersion.Version.ToString(),
			RuntimeInformation.OSDescription,
			machine,
			"unknown",
			"unknown",
			operatingSystem
		);
	}

	private static class NativeMethods {
		[DllImport( "libc", EntryPoint = "uname", SetLastError = true )]
		internal static extern int Uname( IntPtr buffer );
	}
}
