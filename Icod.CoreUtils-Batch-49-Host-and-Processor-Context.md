# Batch 49 — Host and processor context

## Purpose

Batch 49 adds `hostid` and `nproc` as the first command consumers of the
Completion Gate F2 host and processor-resource provider. The provider remains a
factual, policy-neutral boundary. Reproducible presentation and GNU command
policy remain in the executable projects.

## `hostid`

`hostid` requests a `HostResourceValue<HostIdentifier>` from an injectable
`IHostIdentifierProvider`. On success it prints `HostIdentifier.Hexadecimal`,
which is always the low normalized 32 bits formatted as eight lowercase
hexadecimal digits. The command never prints the raw Windows `MachineGuid`,
Linux `machine-id`, host name, or another stable textual source used by the
provider.

Only `--help`, `--version`, and the option terminator are recognized. Every
operand is invalid. An unavailable observation, provider exception, or canceled
observation produces a controlled diagnostic and nonzero status.

## `nproc` precedence

`nproc` receives one `ProcessorResourceSnapshot` from an injectable
`IProcessorResourceProvider` and resolves it using command-local policy:

1. `--all` ignores OpenMP variables and quota facts. It selects the installed
   processor count, then the configured count, with controlled fallbacks when a
   platform cannot distinguish those concepts.
2. Without `--all`, a valid positive `OMP_NUM_THREADS` supplies the count. A
   comma-separated OpenMP place list contributes its first value. A valid
   `OMP_THREAD_LIMIT` may reduce that explicit value. Host affinity and quotas
   do not reduce an explicit OpenMP thread count.
3. Without `OMP_NUM_THREADS`, current-process affinity and the managed
   process-available count are both process-scoped. When both are available,
   the smaller value is selected. The command then falls back through online,
   installed, and configured counts.
4. A valid `OMP_THREAD_LIMIT` may reduce the process-derived count.
5. An available cgroup, container, processor-set, or job-object hard quota may
   reduce the process-derived count. Fractional capacity is rounded to the
   nearest integral processor, with half values rounded upward and a minimum of
   one.
6. `--ignore=N` is applied last. It subtracts `N` only as far as possible while
   preserving the required result of at least one.

Unset, empty, zero, malformed, signed, or overflowing OpenMP values are ignored.
Malformed `--ignore` values are command-line errors and prevent provider access.
Repeated `--ignore` options use the last supplied value.

## Availability and exit status

The F2 provider reports individual facts as available, unavailable,
unsupported, or not applicable. `nproc` can therefore fall back deterministically
without turning one absent optional fact into command failure. If every count is
unavailable, the GNU-required minimum of one is printed successfully. A failure
of the provider operation itself, cancellation, or invalid command syntax
returns a nonzero status and a diagnostic on standard error.

## Test boundary

`HostId.Tests` injects deterministic available, unavailable, and throwing
providers. `NProc.Tests` constructs snapshots without querying the live host and
covers process-scoped selection, OpenMP precedence, quota rounding, all mode,
ignore saturation, count fallbacks, and command diagnostics. System-provider
ABI tests remain in the Completion Gate F2 Shared test suite.
