# Icod.ProcPs.Shared

`Icod.ProcPs.Shared` is the suite-specific class library for the selected
procps-ng 4.0.6 command set in this repository. It owns process enumeration,
procps field semantics, Linux `/proc` parsing, native Windows and macOS
observation providers, conservative fallback observations for other platforms,
process selection, system metrics, vmstat-specific cumulative counters and disk observations, sampling calculations, personalities,
sorting, and reusable screen-state models.

Cross-suite mechanics remain in `Icod.CoreUtils.Shared`: process identities and
reuse tokens, process/process-group/session targets, launching, arbitrary
waiting, signal delivery (including queued values), priority changes, monotonic
clocks, periodic scheduling, status translation, processor-resource facts, and
terminal primitives. ProcPs code must consume those contracts rather than
creating a parallel process-control layer.

Linux `/proc` is the authoritative procps-ng data source. The neutral models do
not require non-Linux systems to pretend that Linux-only counters exist:
Linux-specific CPU, `/proc/loadavg`, `vmstat`, slab, huge-page, namespace, and
container fields remain separately available where applicable, while common CPU
activity (including native counter width), load averages, memory, swap, commit,
uptime, and session observations carry their own provenance and
`ObservationFidelity`.

The primary provider matrix is:

| Area | Linux | Windows | macOS |
|---|---|---|---|
| Process detail | `/proc` + shared identity provider | .NET process data augmented by Tool Help and Terminal Services session APIs | .NET process data augmented by Darwin `libproc` and POSIX APIs |
| Memory / swap | `/proc/meminfo` | `GetPerformanceInfo` + `EnumPageFilesW` | Mach VM statistics + `hw.memsize` + `vm.swapusage` |
| vmstat paging / block I/O | `/proc/vmstat` + `/proc/diskstats` + sysfs partition identity | explicitly unavailable when no defensible native equivalent is exposed | Mach page/swap counters; Linux disk modes remain unsupported |
| CPU activity | `/proc/stat` | `GetSystemTimes` | Mach `host_statistics` |
| Load average | `/proc/loadavg` | unsupported: no native Unix load-average metric | `getloadavg()` |
| Uptime | `/proc/uptime` | `GetTickCount64` | `kern.boottime` |
| Logged-in users | libc `utmpx` | Windows Terminal Services sessions | libc `utmpx` |

Windows Remote Desktop/Terminal Services session identifiers are deliberately
exposed as `PlatformSessionId`; they are not POSIX process-session identifiers.
Likewise, a Windows load average is not synthesized from CPU utilization or
another unrelated counter. Unsupported or unavailable values remain explicit
instead of being invented as zero.

A final portable provider remains for platforms without one of the dedicated
backends. It intentionally exposes only observations whose semantics are
portable enough to defend.
