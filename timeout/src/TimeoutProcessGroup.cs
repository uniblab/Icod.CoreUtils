namespace Icod.CoreUtils.Timeout;

using System.Runtime.InteropServices;

/// <summary>Owns the POSIX process-group boundary used by the standalone <c>timeout</c> monitor.</summary>
internal static class TimeoutProcessGroup {
	private static int _ownsCurrentProcessGroup;

	/// <summary>Gets whether this process successfully established itself as the monitored process-group leader.</summary>
	internal static bool OwnsCurrentProcessGroup => 0 != Volatile.Read(
		ref _ownsCurrentProcessGroup
	);

	/// <summary>Attempts to place the current POSIX process in a new process group led by itself.</summary>
	internal static bool TryCreateForCurrentProcess() {
		if ( OperatingSystem.IsWindows() ) {
			return false;
		}
		if ( 0 != SetProcessGroupId(
			0,
			0
		) ) {
			return false;
		}
		Volatile.Write(
			ref _ownsCurrentProcessGroup,
			1
		);
		return true;
	}

	[DllImport(
		"libc",
		EntryPoint = "setpgid",
		SetLastError = true
	)]
	private static extern int SetProcessGroupId(
		int processId,
		int processGroupId
	);
}
