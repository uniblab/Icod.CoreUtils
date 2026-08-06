# Batch 47 — Filesystem Usage Reporting

## Scope

Batch 47 replaces the seed `df` and `du` implementations with command front ends over shared filesystem-usage policy and accounting contracts.

## Shared boundary

`Icod.CoreUtils.Shared.FileSystem.Usage` owns behavior reused by both commands or required to keep accounting independent from presentation:

- GNU block-size environment precedence and binary/decimal formatting;
- injectable filesystem-capacity observations;
- explicit inode availability;
- postorder allocated/apparent/inode accounting over the Completion Gate E1 traversal and E3 metadata providers;
- hard-link identity deduplication across operands;
- symbolic-link, filesystem-boundary, exclusion, and timestamp policy.

`df` retains filesystem-row selection and tabular field presentation. `du` retains operand acquisition, output-depth, summarize, threshold, timestamp formatting, and diagnostics.

## Platform policy

- Allocated-byte reporting uses authoritative metadata when available and falls back to logical size only when the host cannot expose allocation.
- Capacity, filesystem type, mount point, and volume identity use the shared filesystem-information provider.
- POSIX inode pools use `statvfs`. Windows reports inode columns as unavailable rather than inventing counts.
- `df --sync` invokes the POSIX filesystem synchronization primitive; Windows reports a controlled unsupported diagnostic rather than silently ignoring the request.
- Link following and mount-boundary behavior is delegated to the shared traversal engine, including cycle and boundary events.
- Unsupported or failed native observations remain explicit metadata states and are rendered as controlled `-` fields or command diagnostics.

## Validation

The batch adds dedicated `Df.Tests` and `Du.Tests` projects plus Shared tests for unit policy, apparent-size accounting, and exclusion behavior. Final acceptance remains the repository-wide Debug and Release build-and-test matrix on Windows, Ubuntu, and macOS.
