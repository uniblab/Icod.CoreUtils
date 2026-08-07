# Shared host and processor resources

`Icod.CoreUtils.Shared.Host` is the Completion Gate F2 factual provider layer. It is provisionally classified as an `Icod.CommandFramework` candidate because Coreutils and the later ProcPs family consume the same host and processor facts.

## Contract

`IHostResourceProvider` exposes independently injectable host-identifier and processor-resource observations plus one combined snapshot. Every optional fact uses `HostResourceValue<T>` so consumers can distinguish:

- an available value;
- a temporarily unavailable value;
- a platform-unsupported concept; and
- a concept that does not apply to the current process.

Every observation also carries `HostResourceProvenance`. Consumers must not silently replace unsupported topology, affinity, or quota data with zero. `ObservationFidelity` is the neutral semantic-quality vocabulary established by Completion Gate P1 for later cross-platform observation models: `Exact`, `Equivalent`, `Approximated`, `Synthesized`, or `Unavailable`. Source provenance and semantic fidelity are separate concerns and must both remain visible where a consumer exposes platform-derived fields.

The provider reports facts only. GNU `nproc` handling of `OMP_NUM_THREADS`, `OMP_THREAD_LIMIT`, `--all`, `--ignore`, minimum output, diagnostics, and statuses remains in the Batch 49 command project.

## Platform profile

| Fact | Windows | Linux | macOS | Other/BSD fallback |
|---|---|---|---|---|
| Host identifier | Stable MachineGuid folded to 32 bits | Native `gethostid`, with machine-id/host-name fallback | Native `gethostid`, with host-name fallback | Native `gethostid` where available, then stable text fallback |
| Configured processors | Maximum processor-group capacity | `sysconf(_SC_NPROCESSORS_CONF)` | `hw.logicalcpu_max`/`hw.ncpu` | Explicitly unsupported |
| Installed processors | Active processors across groups | sysfs `present`, falling back to configured | Configured logical processors | Explicitly unsupported |
| Online processors | Active processors across groups | sysfs `online`, falling back to `sysconf` | `hw.logicalcpu`/`hw.ncpu` | Explicitly unsupported |
| Process-available processors | `Environment.ProcessorCount` | `Environment.ProcessorCount` | `Environment.ProcessorCount` | `Environment.ProcessorCount` |
| Affinity / processor set | Default CPU sets, then process-group mask | `sched_getaffinity`; the effective mask includes cpuset restrictions | Explicitly unsupported | Explicitly unsupported |
| Hard CPU quota | Job-object CPU hard or maximum rate | cgroup v2 `cpu.max` or v1 CFS quota | Explicitly unsupported | Explicitly unsupported |
| Topology / NUMA | Group-aware logical processor information | sysfs package/core/node directories | package/core/logical sysctls; NUMA unsupported | Logical count only |

Windows CPU-set values are labeled as opaque CPU-set identifiers rather than logical indices. Affinity masks that cover only the current processor group are marked incomplete. Relative Windows job weights are reported as unavailable rather than misrepresented as a hard quota. Linux cgroup paths are rooted and containment-checked before controller files are read.

## Host-ID normalization

Native signed host IDs are normalized to their low unsigned 32 bits. Stable textual machine identifiers are trimmed, decoded as bytes when hexadecimal, and otherwise lowercased and encoded as UTF-8 before deterministic FNV-1a folding. Raw MachineGuid and machine-id values are never exposed by the public snapshot.

## ProcPs boundary from Completion Gate P1

ProcPs may consume these processor counts, topology, affinity, quota, and host facts directly. It must not duplicate them inside `Icod.ProcPs.Shared`. Linux `/proc` and related kernel interfaces remain authoritative for ProcPs-only memory, CPU, process, map, slab, hugepage, user-session, and kernel-parameter observations; equivalent providers on other hosts must attach explicit semantic fidelity rather than substituting plausible zeros.
