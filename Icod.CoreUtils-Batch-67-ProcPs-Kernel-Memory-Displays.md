# Batch 67 — ProcPs kernel-memory displays

Batch 67 introduces the suite-correct `Icod.ProcPs.HugeTop` and `Icod.ProcPs.SlabTop` projects and the reusable Linux kernel-memory observations they require.

## `hugetop`

`Icod.ProcPs.HugeTop` reports Linux huge-page pools and processes with hugetlb mappings. The system provider reads per-NUMA-node pools from `/sys/devices/system/node/node*/hugepages/hugepages-*kB`, while per-process shared and private hugetlb use is accumulated from detailed `/proc/PID/smaps` observations already owned by `Icod.ProcPs.Shared`.

The command supports procps-ng 4.0.6-style delay, NUMA, one-shot, human-readable, help, and version options. One-shot mode is ordinary batch output and is therefore valid with redirected standard output. Interactive mode uses the shared ProcPs full-screen lifecycle for geometry, refresh scheduling, resize handling, cancellation, suspension/resume, and restoration.

## `slabtop`

`Icod.ProcPs.SlabTop` reads `/proc/slabinfo` directly through a dedicated shared provider. The parser preserves active and total objects, object size, objects per slab, pages per slab, and active and total slab counts, including the `slabdata` values required for accurate active/total size reporting.

The command supports procps-ng 4.0.6-style one-shot output, refresh delay, human-readable sizes, and sort criteria for active objects, objects per slab, cache size, slab counts, active slabs, name, pages per slab, object size, utilization, and total objects. Interactive mode reuses the same full-screen lifecycle as `hugetop`.

## Platform boundary

These commands deliberately do not approximate Linux kernel allocator interfaces on other operating systems. `SystemProcHugePageProvider` and `SystemProcSlabProvider` return controlled unsupported observations outside Linux. Help and version remain portable, while data-reporting operations surface the unsupported capability clearly.

## Validation

Dedicated tests use injected kernel-memory providers, schedulers, terminal factories, signal sources, clocks, and standard streams. They cover batch and full-screen output, sorting, human-readable sizes, delay selection, resize behavior, redirected-output policy, cancellation, unsupported providers, option validation, and exact slabinfo parsing.

Repository build and required-runner validation remain pending for Batch 67 closure.
