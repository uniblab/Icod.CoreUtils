namespace Icod.CoreUtils.HostName;

using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Defines the host operations required by GNU Coreutils <c>hostname</c>.
/// </summary>
public interface IHostNamePlatform {
	/// <summary>
	/// Gets the active host name of the current system.
	/// </summary>
	/// <returns>The active host name.</returns>
	string GetHostName();

	/// <summary>
	/// Changes the active host name of the current system.
	/// </summary>
	/// <param name="hostName">The exact host name requested by the caller.</param>
	/// <exception cref="ArgumentException"><paramref name="hostName"/> is empty.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="hostName"/> is <see langword="null"/>.</exception>
	/// <exception cref="PlatformNotSupportedException">The host cannot provide GNU-compatible active-hostname mutation.</exception>
	/// <exception cref="Win32Exception">The native host-name mutation failed.</exception>
	void SetHostName( string hostName );
}

/// <summary>
/// Provides the system implementation of the host-name operations required by <c>hostname</c>.
/// </summary>
public sealed class SystemHostNamePlatform : IHostNamePlatform {
	/// <summary>
	/// Gets the singleton system provider.
	/// </summary>
	public static SystemHostNamePlatform Instance {
		get;
	} = new();

	private SystemHostNamePlatform() {
	}

	/// <inheritdoc />
	public string GetHostName() {
		return Dns.GetHostName();
	}

	/// <inheritdoc />
	public void SetHostName( string hostName ) {
		ArgumentException.ThrowIfNullOrEmpty( hostName );
		if (
			!OperatingSystem.IsLinux()
			&& !OperatingSystem.IsMacOS()
			&& !OperatingSystem.IsFreeBSD()
		) {
			throw new PlatformNotSupportedException(
				"setting the active host name is unsupported on this host"
			);
		}

		var byteCount = Encoding.UTF8.GetByteCount( hostName );
		var pointer = Marshal.StringToCoTaskMemUTF8( hostName );
		try {
			var result = ( OperatingSystem.IsLinux() )
				? NativeMethods.SetHostNameLinux(
					pointer,
					( nuint )byteCount
				)
				: NativeMethods.SetHostNameBsd(
					pointer,
					byteCount
				)
			;
			if ( 0 != result ) {
				throw new Win32Exception(
					Marshal.GetLastPInvokeError()
				);
			}
		} finally {
			Marshal.FreeCoTaskMem(
				pointer
			);
		}
	}

	private static class NativeMethods {
		/// <summary>
		/// Calls the Linux <c>sethostname(2)</c> ABI, whose length parameter is <c>size_t</c>.
		/// </summary>
		/// <param name="name">Pointer to the UTF-8 host-name bytes.</param>
		/// <param name="length">Number of bytes in <paramref name="name"/>.</param>
		/// <returns>Zero on success, or <c>-1</c> with <c>errno</c> set on failure.</returns>
		[DllImport(
			"libc",
			EntryPoint = "sethostname",
			SetLastError = true
		)]
		internal static extern int SetHostNameLinux(
			IntPtr name,
			nuint length
		);

		/// <summary>
		/// Calls the Darwin/FreeBSD <c>sethostname(3)</c> ABI, whose length parameter is <c>int</c>.
		/// </summary>
		/// <param name="name">Pointer to the UTF-8 host-name bytes.</param>
		/// <param name="length">Number of bytes in <paramref name="name"/>.</param>
		/// <returns>Zero on success, or <c>-1</c> with <c>errno</c> set on failure.</returns>
		[DllImport(
			"libc",
			EntryPoint = "sethostname",
			SetLastError = true
		)]
		internal static extern int SetHostNameBsd(
			IntPtr name,
			int length
		);
	}
}
