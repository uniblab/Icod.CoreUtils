# Completion Gate P1 — ProcPs classification and provider foundation

## Decision summary

Completion Gate P1 freezes the architectural boundary that the procps-ng work beginning in Batch 56 must consume. The authoritative upstream baseline is **procps-ng 4.0.6**. `Icod.ProcPs.Shared` is deliberately not created by this gate; Batch 56 owns its creation. P1 instead records the exact selected inventory, prevents cross-suite process abstractions from being copied into that new library, establishes Linux and portability policy, and closes one already-proven cross-suite signal gap in the current Shared incubation layer.

## Pinned procps-ng 4.0.6 inventory

The 4.0.6 `Makefile.am` installs the following relevant programs on non-Cygwin builds. The repository selects 17 commands for the ProcPs block:

| Repository command | Upstream build/install identity | Upstream condition | Planned batch |
|---|---|---|---:|
| `ps` | `src/ps/pscommand`, transformed to `ps` | base non-Cygwin | 62 |
| `free` | `src/free` | base | 57 |
| `pgrep` | `src/pgrep` from `src/pgrep.c` | base | 59 |
| `pkill` | `src/pkill` from `src/pgrep.c` | base | 59 |
| `pidwait` | `src/pidwait` from `src/pgrep.c` | `BUILD_PIDWAIT` | 59 |
| `pmap` | `src/pmap` | base | 61 |
| `pwdx` | `src/pwdx` | base non-Cygwin | 60 |
| `tload` | `src/tload` | base non-Cygwin | 65 |
| `uptime` | `src/uptime` | base | 57 |
| `vmstat` | `src/vmstat` | base | 58 |
| `sysctl` | `src/sysctl` | base non-Cygwin, installed as sbin | 64 |
| `pidof` | `src/pidof` | `BUILD_PIDOF` | 60 |
| `w` | `src/w` | `BUILD_W` | 63 |
| `watch` | `src/watch` | `WITH_NCURSES` | 66 |
| `top` | `src/top/top` | `WITH_NCURSES` | 68 |
| `slabtop` | `src/slabtop` | `WITH_NCURSES`, non-Cygwin | 67 |
| `hugetop` | `src/hugetop` | `WITH_NCURSES`, non-Cygwin | 67 |

The selected inventory deliberately excludes procps-ng `kill`, `skill`, and `snice`. The repository has one `kill`, the util-linux 2.42.2 profile in `Icod.UtilLinux.Kill`, and one `renice`, the util-linux 2.42.2 profile in `Icod.UtilLinux.Renice`.

### `pidwait` versus `pwait`

Procps-ng NEWS records **“Rename pwait to pidwait”** in 4.0.0. The 4.0.6 build installs `src/pidwait` when `BUILD_PIDWAIT` is enabled and contains no `pwait` program or install alias. The 4.0.6 `pgrep.c` dispatcher recognizes the `pidwait` executable identity. Therefore the repository will implement only `Icod.ProcPs.PidWait`; **there is no `Icod.ProcPs.PWait` project or compatibility launcher in the pinned profile**.

## F2–F4 cross-suite contract audit

The rule is ownership by semantic layer, not by the first command that happened to need a feature.

| Current Shared contract/family | Proven current consumers | Planned ProcPs consumers | Other suite assessment | P1 classification |
|---|---|---|---|---|
| `IHostResourceProvider`, processor availability/topology/affinity/quota | Coreutils `hostid`, `nproc` foundation | `free`, `vmstat`, `ps`, `top`, system summaries | useful to later host-aware suites; no Tar/Ed ownership | future `Icod.CommandFramework` candidate |
| `ProcessIdentity`, `ProcessReuseToken`, `IProcessInspector` | util-linux `kill`; Coreutils `timeout` | `pgrep`, `pkill`, `pidwait`, `pidof`, `pwdx`, `pmap`, `ps`, `top` | general process mechanic | future `Icod.CommandFramework` candidate |
| `ProcessTarget` and process-group/session target model | util-linux `kill`; Coreutils `timeout` | selection adapters, `pkill`, `top` | general process mechanic | future `Icod.CommandFramework` candidate |
| `IExecutableLocator`, `ProcessEnvironment`, `IProcessExecutor`, `ProcessRunOptions` | Coreutils `env`, `nohup`, `nice`, `timeout` | `watch`; possible future command execution where upstream requires it | Tar external compressors and LineEditor shell escapes are candidate consumers, not owners | future `Icod.CommandFramework` candidate |
| arbitrary-process wait and `ProcessTermination` | F4 tests; child execution status consumers | `pidwait` and lifetime-sensitive selection | general process mechanic | future `Icod.CommandFramework` candidate |
| `IProcessSignalProvider`, signal catalog/disposition/mask | util-linux `kill`; Coreutils `timeout` | `pkill`, `top`, signal-aware selection | general process mechanic | future `Icod.CommandFramework` candidate |
| `IProcessPriorityProvider` / selector provider | Coreutils `nice`; util-linux `renice` | `top` interactive renice | general process mechanic | future `Icod.CommandFramework` candidate |
| `IMonotonicClock`, periodic scheduler | Coreutils `timeout` | `free` repeat mode, `vmstat`, `tload`, `watch`, `hugetop`, `slabtop`, `top` | general timing mechanic | future `Icod.CommandFramework` candidate |
| operation/status and launch-failure translation | Batches 52–55 | all ProcPs commands that act on processes or launch children | general command infrastructure | future `Icod.CommandFramework` candidate |
| shared terminal observation/control | Coreutils listing/terminal commands | `tload`, `watch`, `hugetop`, `slabtop`, `top` | Tar/LineEditor may consume neutral portions when needed | future `Icod.CommandFramework` candidate |

`Icod.ProcPs.Shared` must not define parallel identity, executable lookup, launch, signal, priority, monotonic-clock, terminal-handle, or generic result/status models merely to give them ProcPs names.

## Shared gap closed by P1: queued signal values

F4 already placed `int? queuedValue` on `IProcessSignalProvider.DeliverAsync`, but the system provider returned `Unsupported` for every queued value. That became proven cross-suite duplication when util-linux `kill --queue` required command-local `sigqueue(3)` and procps-ng 4.0.6 `pkill --queue` also required queued delivery.

P1 therefore adds `ProcessControlCapabilities.QueuedSignalDelivery` and implements Linux individual-process queued delivery through `sigqueue(3)` inside `SystemProcessSignalProvider`. Positive-PID util-linux `kill --queue` delivery is migrated to that Shared provider, while PID `0`/negative native conventions, pidfd-inode syntax, and repeated `kill --timeout` policy remain command-specific. Non-Linux hosts and group/session queued targets return controlled `Unsupported` results.

Pidfd is still treated as a **mechanism**, not a ProcPs domain abstraction. Batch 56/59 may harden the existing Shared arbitrary-wait and protected-signal implementations with pidfd where necessary for exact Linux semantics, but `Icod.ProcPs.Shared` must not expose a second public pidfd identity model. Pidfd inode syntax remains a util-linux `kill` command feature rather than a generic target syntax.

## ProcPs observation/provider boundary

### Linux

Linux is authoritative. `Icod.ProcPs.Shared` will own fixture-driven parsers for `/proc`, `/sys`, and other Linux interfaces needed by procps-ng rather than routing through external programs. Files that may disappear between enumeration and read are ordinary lifetime races, not exceptional test failures. Permission failures remain distinguishable from vanished processes.

The ProcPs layer owns:

- process enumeration and detailed snapshots;
- parent/child, session, process-group, terminal, user/group, namespace, container, cgroup, capability, and scheduling observations not already represented by neutral Shared facts;
- procps selection grammar and regex/name policy;
- memory, swap, CPU activity, load, uptime, virtual-memory, maps, slab, hugepage, user-session, and kernel-parameter models;
- field catalogs, aliases, personalities, sorting, display, and configuration;
- sampled-counter delta, wraparound, first-sample, interval, and refresh policy;
- full-screen state models and interaction policy.

### Windows

Use BCL and documented Win32 process/system APIs where they expose defensible equivalents. Do not invent Linux `/proc` fields. Process identity, launch, priority substitutions, terminal capability, and F2 processor facts remain in Shared. ProcPs fields absent from Windows APIs are `Unavailable`; semantically different Windows concepts may be `Approximated` only with an explicit explanation.

### macOS

Prefer documented `sysctl`, `libproc`, Mach, and BCL observations where available. Linux-only process-map, namespace, cgroup, slab, and procfs concepts remain unavailable unless a true semantic equivalent exists. POSIX signal and terminal mechanics stay in Shared.

### BSD

BSD support is best-effort and capability-driven. Use stable `sysctl`/kvm-style data only when the implementation can be tested without silently changing semantics. Otherwise expose `Unavailable` rather than zero or fabricated Linux-shaped records.

## Field provenance and fidelity

Completion Gate P1 adds the neutral `ObservationFidelity` vocabulary to Shared:

- `Exact` — authoritative source with matching semantics;
- `Equivalent` — different platform source, demonstrably equivalent semantics;
- `Approximated` — documented mapping that may differ from Linux/procps semantics;
- `Synthesized` — derived from other observations;
- `Unavailable` — no defensible observation under the current host/capability/permission context.

This fidelity is distinct from **source provenance** such as Linux procfs, sysfs, cgroup, Windows APIs, macOS sysctl, or a managed runtime. Batch 56 must attach both to every ProcPs field/value model that can vary by host. An unavailable field never becomes numeric zero simply to preserve table shape.

## Lifetime, namespaces, containers, affinity, and quota

- Enumeration returns identities/snapshots, not promises that a PID still exists.
- Any later destructive operation must revalidate or use the Shared protected-process mechanism; PID reuse is reported distinctly where detectable.
- ProcPs owns namespace/container interpretation because those values are observation fields and selection criteria; it does not replace `ProcessIdentity`.
- F2 affinity and hard-quota observations remain the neutral source for process-available CPU resources. ProcPs may interpret them for display and percentages but must retain provenance/completeness.
- Host/procfs access denied and process vanished are separate outcomes.
- Sampling must use the Shared monotonic clock. Counter wraparound/delta and first-sample policy belong to ProcPs.

## Terminal and output-directory policy

Shared owns terminal attachment, geometry, environment/capability detection, and mode/control primitives. ProcPs owns screen buffers, field layout, interaction, sorting/filtering, configuration, refresh decisions, and command-specific restoration policy.

During co-resident development every ProcPs executable is emitted beneath a suite-specific ProcPs output directory. This is mandatory for `Icod.ProcPs.Uptime`, which collides with the historical Coreutils-profile `uptime`; final package/launcher ownership remains a Completion Gate G decision.

## Test foundation required by Batch 56

`Icod.ProcPs.Shared.Tests` must begin with fixture-driven Linux parser tests and injectable provider tests. Fixtures cover normal records plus disappearing processes, denied files, malformed/partial records, long names, namespaces/cgroups, counter rollover, and optional kernel fields. Platform-provider tests assert capabilities and provenance/fidelity rather than expecting fabricated Linux fields.

The existing Shared process tests remain responsible for neutral identity, wait, signal, queued-signal, priority, launch, and status behavior. ProcPs tests should test adapters and suite policy, not duplicate those mechanics.

## Gate outcome

P1 is complete when Batch 56 can create `Icod.ProcPs.Shared` without inventing a second framework layer. The immediate next step is Batch 56: implement the ProcPs-specific provider/domain foundation over these frozen common contracts.
