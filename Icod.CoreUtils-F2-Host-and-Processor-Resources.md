# Completion Gate F2 — host and processor resources

## Purpose

Completion Gate F2 establishes a factual, injectable host-resource provider before `hostid` and `nproc` are implemented. The same contracts are intended for later use by `Icod.ProcPs.Shared`, `ps`, `top`, `vmstat`, and other process-observation consumers. They are therefore provisional `Icod.CommandFramework` candidates even though they remain physically in `Icod.CoreUtils.Shared` during incubation.

The provider deliberately does **not** implement GNU `nproc` command policy. OpenMP environment variables, `--all`, `--ignore`, minimum-result handling, diagnostics, formatting, and exit status remain in Batch 49.

## Public model

`IHostResourceProvider` combines two independently consumable contracts:

- `IHostIdentifierProvider` returns a normalized 32-bit host identifier with source provenance;
- `IProcessorResourceProvider` returns configured, installed, online, and process-available counts, process affinity, hard CPU quota, optional topology, and a capability report.

Every optional fact is a `HostResourceValue<T>`. Its state is `Available`, `Unavailable`, `Unsupported`, or `NotApplicable`, and every observation identifies its provenance. This prevents command and ProcPs consumers from treating zero as either a real count or an unsupported result.

## Host identifiers

On Unix-like systems the provider first uses `gethostid` and normalizes the low unsigned 32 bits. On Windows it reads the stable MachineGuid and deterministically folds it to 32 bits. Stable machine-id files and the normalized host name are controlled fallbacks. Raw MachineGuid and machine-id text is never returned.

Text normalization removes common hexadecimal separators and decodes hexadecimal identifiers before applying FNV-1a. Nonhexadecimal text is trimmed, lowercased invariantly, encoded as UTF-8, and folded by the same algorithm. Batch 49 owns final `hostid` formatting and any user-visible explanation of non-POSIX sources.

## Processor facts

### Linux

- configured processors: `sysconf(_SC_NPROCESSORS_CONF)`;
- installed processors: sysfs `present`, falling back to configured;
- online processors: sysfs `online`, falling back to `sysconf(_SC_NPROCESSORS_ONLN)`;
- process affinity: `sched_getaffinity`, whose effective mask incorporates scheduler and cpuset restrictions;
- hard quota: cgroup v2 `cpu.max`, then cgroup v1 CFS quota/period;
- topology: sysfs package/core identifiers and NUMA node directories.

Cgroup membership paths are resolved beneath fixed controller roots and rejected if canonical combination would escape the root.

### Windows

- configured and active counts: processor-group APIs across all groups;
- process restrictions: default CPU sets when present, otherwise the process affinity mask;
- hard quota: current-job CPU hard-cap or maximum-rate information;
- topology and NUMA: group-aware logical processor information.

Windows CPU-set values are labeled as opaque CPU-set identifiers rather than logical indices. A legacy affinity mask on a multi-group host is marked incomplete. Relative job weights are not converted into a fictitious processor quota.

### macOS

Configured, online, package, physical-core, and logical counts use `sysctlbyname`. The provider explicitly reports process affinity, hard container quota, and NUMA inventory as unsupported through the current stable boundary rather than inferring them.

### Other/BSD

The portable adapter reports the managed process-available logical count and controlled unsupported results for host-wide configured, installed, and online counts, affinity, and quota. Native `gethostid` remains attempted where the ABI provides it. BSD-specific refinements remain best effort and may be added without changing the public model.

## Testing boundary

Pure parsers cover CPU lists, native bit masks, and cgroup quota text. An injected provider proves deterministic consumption without host access. The system-provider test checks only positive-count and capability-state invariants so Windows, Ubuntu, and macOS can expose different supported facts without platform-conditioned expected values.

The current execution environment is offline, so this gate is packaged with static validation and deterministic source tests. The repository's permanent Debug/Release and three-runner matrix remains the integration acceptance check when testing becomes available again.
