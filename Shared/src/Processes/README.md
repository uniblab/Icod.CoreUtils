# Processes

The `Icod.CoreUtils.Shared.Processes` namespace supplies the cross-suite process execution and control foundation completed by Completion Gate F4.

## Responsibilities

- Model process identities with optional PID-reuse tokens.
- Model process, process-group, and session targets without overloading signed integers.
- Resolve executables against explicit working-directory and environment snapshots.
- Launch exact argument vectors with `UseShellExecute = false` and `ProcessStartInfo.ArgumentList`.
- Construct inherited, empty, and explicitly modified child environments.
- Forward or capture all three standard streams asynchronously.
- Expose the protected child identity at launch for concurrent signal or priority orchestration.
- Apply explicit cancellation, monotonic timeout, process-tree cleanup, and leave-running policies.
- Observe liveness and wait for arbitrary processes through controlled result contracts.
- Parse, list, translate, inspect, and deliver signals where the host exposes those operations.
- Read, set, and adjust portable nice values, with declared Windows priority-class substitutions.
- Translate exits, signals, timeouts, cancellation, not-found versus cannot-invoke launch failures, and vanished targets to command-facing status models.

## Provider boundaries

`IProcessExecutor`, `IExecutableLocator`, `IProcessInspector`, `IProcessSignalProvider`, and `IProcessPriorityProvider` are injectable. Their system implementations return `ProcessOperationResult` values for ordinary races, permission failures, unsupported capabilities, and PID reuse instead of requiring commands to catch platform exceptions.

Linux uses `/proc/<pid>/stat` start time as the strongest available PID-reuse token and `/proc/<pid>/status` for signal dispositions. Other hosts use process start time when available. POSIX signal and priority operations are isolated behind native calls. Windows exposes only defensible substitutions: signal zero becomes a liveness probe, TERM and KILL may become immediate process termination, and nice values map approximately to process priority classes. Process-group, session, queued-signal, or disposition semantics that cannot be represented honestly return `Unsupported`.

## Design notes

This infrastructure is for commands whose documented behavior requires executing or controlling another program. It must not be used to delegate implementation to the native equivalent of the command being implemented. The contracts remain physically in `Icod.CoreUtils.Shared` during incubation but are classified as candidates for the future neutral `Icod.CommandFramework` package.
