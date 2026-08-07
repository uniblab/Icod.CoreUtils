# Processes

The `Icod.CoreUtils.Shared.Processes` namespace supplies the cross-suite process execution and control foundation completed by Completion Gate F4.

## Responsibilities

- Model process identities with optional PID-reuse tokens.
- Model process, process-group, and session targets without overloading signed integers.
- Resolve executables against explicit working-directory and environment snapshots.
- Launch exact argument vectors with `UseShellExecute = false` and `ProcessStartInfo.ArgumentList`, plus a narrow POSIX `posix_spawn` path when a distinct native `argv[0]` is required.
- Construct inherited, empty, and explicitly modified child environments.
- Forward or capture all three standard streams asynchronously, with an explicit POSIX launch mode for write-only `/dev/null` standard input when a child must observe read failure rather than EOF.
- Expose the protected child identity at launch for concurrent signal or priority orchestration.
- Apply explicit cancellation, monotonic timeout, process-tree cleanup, and leave-running policies.
- Observe liveness and wait for arbitrary processes through controlled result contracts.
- Parse, list, translate, inspect, and deliver signals where the host exposes those operations, observe Linux blocked masks, and apply requested POSIX signal dispositions or masks atomically around child creation.
- Read, set, and adjust portable nice values for processes and process groups, and expose priority-specific process/group/user selectors, with declared Windows priority-class substitutions.
- Translate exits, signals, timeouts, cancellation, setup versus not-found versus cannot-invoke launch failures, and vanished targets to command-facing status models.

## Provider boundaries

`IProcessExecutor`, `IExecutableLocator`, `IProcessInspector`, `IProcessSignalProvider`, `IProcessPriorityProvider`, and `IProcessPrioritySelectorProvider` are injectable. Their system implementations return `ProcessOperationResult` values for ordinary races, permission failures, unsupported capabilities, and PID reuse instead of requiring commands to catch platform exceptions.

Linux uses `/proc/<pid>/stat` start time as the strongest available PID-reuse token and `/proc/<pid>/status` for signal dispositions. Other hosts use process start time when available. POSIX signal and priority operations are isolated behind Shared native calls; priority selectors preserve `getpriority(2)`/`setpriority(2)` process, process-group, user-ID, and selector-zero semantics. Windows exposes only defensible substitutions: signal zero becomes a liveness probe, TERM and KILL may become immediate process termination, and nice values map approximately to process priority classes.

Signal/session semantics and priority target classes that cannot be represented honestly return `Unsupported`; on Windows, group/user priority selectors are not approximated.

## Design notes

This infrastructure is for commands whose documented behavior requires executing or controlling another program. It must not be used to delegate implementation to the native equivalent of the command being implemented. The contracts remain physically in `Icod.CoreUtils.Shared` during incubation but are classified as candidates for the future neutral `Icod.CommandFramework` package.
