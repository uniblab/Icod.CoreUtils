# Icod.ProcPs.Shared three-platform provider correction

## Purpose

This correction strengthens the Batch 56 ProcPs observation foundation before
later sampled and per-process tools build on it. Linux `/proc` remains the
canonical procps-ng 4.0.6 source, but Windows and macOS now use their own native
APIs for concepts those systems can answer honestly instead of routing almost
everything through the minimal portable fallback.

## Provider selection

`SystemProcProcessProvider` now selects `LinuxProcProcessProvider`,
`WindowsProcProcessProvider`, or `MacOsProcProcessProvider` on the three primary
platforms. `DotNetProcProcessProvider` remains the conservative fallback for
other hosts.

`SystemProcSystemMetricsProvider` similarly selects dedicated Linux, Windows,
or macOS implementations before falling back to
`PortableProcSystemMetricsProvider`.

## Neutral versus Linux-specific observations

The Linux records remain available for commands that genuinely require Linux
semantics. In particular, `ProcCpuTimes`, `ProcLoadAverage`, raw meminfo fields,
Linux `vmstat`, slab, huge pages, namespaces, and container metadata are not
redefined to mean something different on another operating system.

The shared snapshot additionally exposes:

- `ProcCpuActivity` for common user/system/idle activity with optional
  nice/wait/other counters and an explicit native counter width for wraparound;
- `ProcLoadAverages` for the common one-, five-, and fifteen-minute load values;
- neutral byte-valued properties on `ProcMemoryInfo` for physical memory, cache,
  swap/pagefile, and commit information.

Every observation still carries source provenance and `ObservationFidelity`.

## Windows

The Windows system provider uses documented Win32 APIs:

- `GetPerformanceInfo` for physical memory, immediately reusable memory,
  system cache, and commit totals/limits;
- `EnumPageFilesW` for pagefile capacity and current use;
- `GetSystemTimes` for aggregate user/kernel/idle counters;
- `GetActiveProcessorCount(ALL_PROCESSOR_GROUPS)` to identify the documented
  greater-than-64-processor limitation of `GetSystemTimes`;
- `GetTickCount64` for system uptime;
- Windows Terminal Services enumeration for logged-in user sessions.

The Windows process provider retains the portable .NET process observations and
augments them with Tool Help parent PIDs and `ProcessIdToSessionId`.
`ProcessIdToSessionId` is represented as `PlatformSessionId`, not `SessionId`,
because an RDS/desktop session is not a POSIX process session.

No Unix load average is synthesized on Windows.

## macOS

The macOS system provider uses Darwin/Mach/POSIX sources:

- `hw.memsize`, Mach VM statistics, and `vm.swapusage` for memory and swap;
- Mach host CPU statistics for aggregate CPU activity;
- `getloadavg()` for native one-, five-, and fifteen-minute load averages;
- `kern.boottime` for uptime, with Darwin-sysctl provenance;
- `utmpx` `USER_PROCESS` records for logged-in user sessions.

The macOS process provider augments portable process data with
`proc_pidinfo(PROC_PIDTBSDINFO)`, `proc_pidinfo(PROC_PIDTASKINFO)`, and
`getsid()` to provide parent PID, process group, POSIX session, UID/GID, TTY,
nice value, process state, memory, CPU counters, start counter, and thread
count where available.

## Command consumers

`free` now reads neutral memory properties first and preserves raw Linux
meminfo-field fallbacks for the authoritative Linux path and fixtures.

`uptime` now reads the neutral load-average observation first and preserves the
Linux detailed-load fallback. Consequently normal macOS `uptime` can use the
native Darwin load averages. Windows still reports the load average as
unsupported instead of substituting an unrelated CPU percentage.

## Validation status

The correction adds OS-gated Windows and macOS provider tests plus neutral-model
coverage. The artifact-generation environment does not provide a .NET SDK, so
no local `dotnet build` or xUnit run was possible. The repository's Debug and
Release builds and all three required CI runners remain the closure authority.

## Intentional non-equivalents

The correction does not force platform concepts into Linux fields when the
semantics do not match. Windows therefore leaves POSIX process groups,
POSIX process sessions, Unix UID/GID values, Unix load average, Linux
namespaces/cgroups, and Linux `/proc` memory-map semantics unavailable. The
Windows Terminal Services identifier is exposed separately as
`PlatformSessionId`.

macOS supplies the POSIX process/session/user concepts and native load averages,
but Linux-only namespace/cgroup, slab, huge-page, and detailed `/proc/vmstat`
fields remain unavailable. Process command-line and memory-map enrichment on
non-Linux hosts remain separate future provider work rather than being inferred
from unrelated APIs.

These are deliberate capability boundaries, not silent zero-valued substitutes.
