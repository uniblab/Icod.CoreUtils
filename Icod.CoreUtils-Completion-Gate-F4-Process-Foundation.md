# Completion Gate F4 — Process Execution and Control Foundation

Completion Gate F4 establishes the process mechanics required by Coreutils Batches 52 through 54 and by the later ProcPs, Tar, and LineEditor consumers. The contracts are implemented in `Icod.CoreUtils.Shared` during incubation and remain candidates for extraction into `Icod.CommandFramework` after their cross-suite consumers prove the final boundary.

## Contract map

| Area | Contract | System implementation |
|---|---|---|
| Child execution | `IProcessExecutor` | `SystemProcessExecutor` |
| Executable lookup | `IExecutableLocator` | `SystemExecutableLocator` |
| Identity, liveness, wait | `IProcessInspector` | `SystemProcessInspector` |
| Signal control | `IProcessSignalProvider` | `SystemProcessSignalProvider` |
| Priority control | `IProcessPriorityProvider` | `SystemProcessPriorityProvider` |
| Monotonic time | `IMonotonicClock` | `SystemMonotonicClock` |
| Periodic refresh | `IPeriodicScheduler` | `MonotonicPeriodicScheduler` |

All process-control providers return `ProcessOperationResult` or `ProcessOperationResult<T>`. The status distinguishes success, vanished targets, PID reuse, access denial, unsupported capability, invalid requests, cancellation, timeout, and other controlled failure. Commands therefore translate provider outcomes into their own GNU diagnostics and exit statuses without treating normal process races as exceptional control flow.

## Identity and target safety

`ProcessIdentity` combines a positive PID with an optional opaque `ProcessReuseToken`. Linux prefers `/proc/<pid>/stat` field 22, the kernel start-time counter. Other hosts use the BCL process start time where permission and platform support allow it. Providers compare a supplied token immediately before destructive or mutating operations. A mismatch returns `Reused` and does not act on the new process.

`ProcessTarget` explicitly distinguishes a process, process group, and session. Integer sign conventions remain confined to the POSIX provider boundary. A target may carry a protected `ProcessIdentity` for single-process operations.

## Execution and environment

`SystemProcessExecutor` preserves the existing `ProcessRunner.RunAsync` facade while making the executor itself injectable. It uses `ProcessStartInfo.ArgumentList`, never constructs a shell command line, and always sets `UseShellExecute` to false. `ProcessEnvironmentBuilder` constructs inherited or empty snapshots with operating-system-appropriate variable-name comparison. An exact snapshot may be combined with final set/remove changes in `ProcessRunOptions`.

Standard input, output, and error are forwarded concurrently. Output and error may independently be forwarded, captured, or both. The runner records the child identity and monotonic elapsed time. `ProcessRunOptions.ProcessStarted` exposes that protected identity immediately after launch so higher-level orchestration can deliver signals or apply other control while `RunAsync` supplies the child wait. Cancellation policy explicitly selects process-tree termination, immediate-child termination, or stream detachment with the child left running. Timeout delay and elapsed-time classification both use the injected monotonic clock and remain separate from caller cancellation.

Launch failures preserve the historical throwing behavior by default. Consumers that require command-layer controlled diagnostics set `ReturnLaunchFailureResult`; the result then has `Started == false` and a `LaunchFailed` termination.

## Waiting and termination

The executor provides child waiting. `IProcessInspector.WaitAsync` provides the arbitrary-process wait capability needed later by `pidwait`, including reuse checks and controlled vanished, denied, and canceled results.

`ProcessTermination` represents normal exit, signal termination, timeout, cancellation, launch failure, vanished process, and unknown termination. Launch failures distinguish executable-not-found from found-but-not-invokable so GNU-facing commands can preserve statuses 127 and 126 respectively. `ToPortableExitCode` supplies the common mapping used by higher-level commands while allowing those commands to override documented statuses such as `timeout --preserve-status`.

## Signals

The signal catalog accepts names with or without `SIG`, decimal numbers, aliases such as `IOT`, `CLD`, and `POLL`, and Linux `RTMIN+n` / `RTMAX-n` notation. The system provider supplies signal listing and translation, POSIX process and process-group delivery, and Linux disposition inspection through `SigIgn` and `SigCgt` masks.

Queued signal values and atomic session delivery remain controlled unsupported operations until a provider can implement their exact semantics. On Windows, signal zero is a liveness probe. TERM and KILL may use immediate process termination and report that a platform substitution was used. Other signals return `Unsupported` rather than claiming POSIX behavior.

## Priority

POSIX providers use `getpriority` and `setpriority` for process and process-group targets. Windows maps the portable -20 through 19 nice range to process priority classes and marks observed values as approximations. Session priority operations remain unsupported because there is no portable atomic equivalent.

## Monotonic scheduling

`IMonotonicClock` supplies provider-defined timestamps, elapsed-time calculation, and cancellable delay. `MonotonicPeriodicScheduler` calculates each deadline from the original start timestamp rather than sleeping once per loop, preventing accumulated handler and scheduling drift. ProcPs refresh loops and Batch 54 timeout logic can inject deterministic clocks in tests.

## Test policy

The F4 test set includes pure model and parser tests, isolated executable-lookup fixtures, deterministic fake-clock scheduling tests, current-process identity/signal-zero/priority probes, and three-platform child integration tests through `ProcessTestHost`. The integration tests verify exact argument boundaries, environment and working-directory construction, identity capture, output forwarding, timeout classification, and cleanup without invoking the native implementation of any command under development.
