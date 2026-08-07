# Completion Gate F2 tests

These tests exercise the host and processor-resource provider independently of Batch 49 command policy.

Coverage includes:

- explicit availability, source-provenance, and Completion Gate P1 semantic-fidelity states;
- native and textual host-ID normalization;
- CPU-list and affinity-mask parsing;
- cgroup v1 and v2 quota parsing;
- deterministic provider injection;
- capability-report construction; and
- controlled host observations without assuming that affinity, quotas, topology, or NUMA are available on every platform.

The system-provider test asserts only invariants that hold on the required Windows, Linux, and macOS runners. Platform-specific native observations are allowed to report `Unavailable`, `Unsupported`, or `NotApplicable` with provenance rather than fabricating values.
