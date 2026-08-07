namespace Icod.CoreUtils.Shared.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Contains isolated POSIX process-control entry points used by system providers.
/// </summary>
internal static class ProcessNative {
	/// <summary>Gets the POSIX permission-denied error number.</summary>
	internal const int PermissionDenied = 1;
	/// <summary>Gets the POSIX no-such-process error number.</summary>
	internal const int NoSuchProcess = 3;
	/// <summary>Gets the POSIX access-denied error number.</summary>
	internal const int AccessDenied = 13;
	/// <summary>Gets the POSIX invalid-argument error number.</summary>
	internal const int InvalidArgument = 22;

	/// <summary>Invokes POSIX kill.</summary>
	[DllImport(
		"libc",
		EntryPoint = "kill",
		SetLastError = true
	)]
	internal static extern int Kill(
		int processId,
		int signal
	);

	/// <summary>Invokes POSIX getpriority.</summary>
	[DllImport(
		"libc",
		EntryPoint = "getpriority",
		SetLastError = true
	)]
	internal static extern int GetPriority(
		int which,
		int who
	);

	/// <summary>Invokes POSIX setpriority.</summary>
	[DllImport(
		"libc",
		EntryPoint = "setpriority",
		SetLastError = true
	)]
	internal static extern int SetPriority(
		int which,
		int who,
		int priority
	);

	/// <summary>Maps a POSIX error number to a controlled process-operation status.</summary>
	internal static ProcessOperationStatus MapErrno(
		int error
	) => error switch {
		NoSuchProcess => ProcessOperationStatus.Vanished,
		PermissionDenied or AccessDenied => ProcessOperationStatus.AccessDenied,
		InvalidArgument => ProcessOperationStatus.InvalidArgument,
		_ => ProcessOperationStatus.Failed
	};
}
