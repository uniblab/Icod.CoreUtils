# Icod.ProcPs.Shared

`Icod.ProcPs.Shared` is the suite-specific class library for the selected
procps-ng 4.0.6 command set in this repository. It owns process enumeration,
procps field semantics, Linux `/proc` parsing, capability-driven non-Linux
observations, process selection, system metrics, sampling calculations,
personalities, sorting, and reusable screen-state models.

Cross-suite mechanics remain in `Icod.CoreUtils.Shared`: process identities and
reuse tokens, process/process-group/session targets, launching, arbitrary
waiting, signal delivery (including queued values), priority changes, monotonic
clocks, periodic scheduling, status translation, processor-resource facts, and
terminal primitives. ProcPs code must consume those contracts rather than
creating a parallel process-control layer.

Linux `/proc` is the authoritative procps-ng data source. Windows, macOS, and
BSD providers expose only values with defensible semantics, and every observed
value carries both a ProcPs-specific source and the neutral
`ObservationFidelity` established at Completion Gate P1. Unsupported or
unavailable fields are represented explicitly rather than synthesized as zero.
