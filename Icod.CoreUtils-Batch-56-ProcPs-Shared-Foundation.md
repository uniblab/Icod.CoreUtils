# Batch 56 — Icod.ProcPs.Shared provider foundation

## Authority and scope

Batch 56 establishes the suite-specific library required by Completion Gate P1
for the selected procps-ng 4.0.6 command set. The project is a DLL/class library
at `Icod.ProcPs.Shared/Icod.ProcPs.Shared.csproj`, deliberately patterned after
`Icod.DiffUtils.Shared`, and references only the current Shared incubation
project.

The upstream field inventory in libproc2's `pids.h` treats PID/PPID/PGRP/session,
UID/GID variants, TTY, command/command line, namespaces, cgroups/container IDs,
CPU ticks, nice/thread state, RSS/VSZ, signals and related process observations
as reusable library fields. The managed foundation follows that architectural
shape without copying libproc2's ABI or leaking native constants into commands.

## Ownership boundary

`Icod.ProcPs.Shared` owns:

- procps-ng process observation models and capability flags;
- Linux procfs parsing and process/system providers;
- capability-driven Windows/macOS/BSD observations using defensible .NET
  process semantics;
- ProcPs-specific provenance paired with P1's neutral `ObservationFidelity`;
- process selection and reusable adapters over shared signal, wait and priority
  contracts;
- process maps and system memory/swap/CPU/load/uptime/vmstat/slab/hugepage
  models;
- counter wraparound, sampling windows and fixed-rate refresh composition;
- ProcPs field catalogs, sorting, personalities, display configuration and
  immutable screen frames.

The current `Icod.CoreUtils.Shared` project continues to own process identities
and reuse tokens, process/process-group/session targets, launch mechanics,
arbitrary waits, signal delivery including queued values, priority operations,
monotonic clocks, periodic scheduling, common status translation and terminal
primitives. Batch 56 does not introduce parallel versions of those APIs.

## Linux observation policy

Linux `/proc` is authoritative. A process snapshot begins by obtaining the
shared `ProcessIdentity`, reads the required procfs files, then observes identity
again. When a reuse token is available and changes during the read window, the
snapshot is rejected as `Reused` rather than attaching stale fields to a new
process occupying the same PID.

The initial parser surface covers:

- `/proc/PID/stat`, including command names containing `)`;
- `/proc/PID/status` UID/GID and nested `NSpid` values;
- NUL-delimited `cmdline` vectors;
- cgroup paths and conservative container-ID derivation;
- namespace symlink identifiers;
- `/proc/PID/maps` entries;
- `/proc/stat` aggregate CPU counters;
- `/proc/meminfo`, `/proc/loadavg`, `/proc/uptime`, `/proc/vmstat`, and
  `/proc/slabinfo`.

Physical-memory values using the procfs `kB` suffix are normalized to bytes;
raw CPU/process tick counters remain counters so later consumers can apply the
appropriate procps-ng sampling policy without losing precision.

## Non-Linux policy

The portable process provider uses `System.Diagnostics.Process` and the shared
process inspector. Values such as process name, session ID, CPU duration,
working set, virtual memory and thread count are labeled `Equivalent` when the
semantics are defensible. Base priority is labeled `Approximated` rather than
pretending it is a POSIX nice value. Linux-only fields such as UID/GID,
namespaces, cgroups and `/proc/PID/maps` are explicitly unavailable.

Portable system metrics currently expose system uptime through
`Environment.TickCount64` as an equivalent observation. Linux-specific memory,
load, slab, hugepage and vmstat semantics remain explicitly unsupported on
non-Linux hosts until a platform provider can define and test equivalent fields.
On Linux, user-session counts follow procps-ng's documented libc fallback by
enumerating utmpx records and counting only `USER_PROCESS` entries. Other hosts
report user-session observation unsupported; process enumeration is never used
as a substitute for login sessions.

## Sampling and presentation

`ProcSampler` consumes `IMonotonicClock` and `IPeriodicScheduler` directly.
Unsigned counter deltas support caller-selected wrap widths, CPU helpers avoid
double-counting Linux guest fields, and fixed-rate refresh uses the shared
scheduler rather than command-local timers.

The first field catalog supplies PID, PPID, PGID, SID, EUID, TTY, state, nice,
thread count, RSS, VSZ, command and full-command fields. Stable multi-key sorting,
Linux/POSIX/BSD/SunOS/Digital/HP/AIX personality resolution, display
configuration and immutable screen-frame models are reusable by `ps`, `top`,
`watch`-style refresh consumers and later ProcPs commands.

## Tests

`tests/ProcPs.Shared.Tests` is fixture-driven for procfs formats, so Linux text
parsing is tested identically on all CI runners. Tests cover stat/status/cmdline,
meminfo and maps parsing, selection semantics, stable sorting, personalities,
screen construction, counter wraparound, CPU/load parsing, a synthetic procfs
process snapshot rooted in a temporary directory, and observation of the current
real process through the system provider.

Batch 57 can therefore begin with `uptime` and `free` as consumers of a
suite-owned, testable provider layer rather than embedding procfs parsing or
platform conditionals in either command.
