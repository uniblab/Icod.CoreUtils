# Batch 55 — GNU `timeout`

## Authority and scope

Batch 55 pins `Icod.CoreUtils.Timeout` to GNU Coreutils 9.11.  The command remains a Coreutils project; it does not shell to the host `timeout` utility and does not acquire ProcPs-specific dependencies.

The implementation covers the GNU option surface used by `timeout(1)`: `--foreground`, `--kill-after`, `--preserve-status`, `--signal`, `--verbose`, help/version, GNU long-option abbreviations, and option parsing that stops before the duration/command operands.  Duration parsing accepts nonnegative decimal and C-style hexadecimal floating-point values, leading whitespace, infinity and the optional lowercase `s`, `m`, `h`, or `d` suffix.  A numeric zero disables the corresponding timer; positive values too small for `TimeSpan` are rounded up rather than silently disabling the timeout, and values beyond the managed interval are saturated.

## Completion Gate F4 consumption

`timeout` owns timeout policy, but not process mechanics.  It consumes:

- `IProcessExecutor` and `ProcessRunOptions` for exact argument-vector launch and 125/126/127 launch translation;
- `ProcessIdentity` for the protected child identity published at launch;
- `IProcessSignalProvider` and explicit process/process-group `ProcessTarget` values for timeout, continuation, and escalation delivery;
- `IMonotonicClock` for both the primary timeout and `--kill-after`; and
- `ProcessTermination` for normal, signaled, launch-failure, and cancellation status translation.

Batch 55 adds one narrow F4 launch capability: `ProcessRunOptions.CreateProcessGroup`.  On POSIX, the executor selects its existing `posix_spawn` path and applies `POSIX_SPAWN_SETPGROUP` atomically so the child is the group leader before user code can run.  The opaque libc spawn-attribute storage remains isolated in Shared and is not exposed to command projects.  On Windows, .NET 10's `ProcessStartInfo.CreateNewProcessGroup` is used.

The POSIX group is rooted at the child PID, rather than placing the managed `timeout` monitor itself in the group.  This preserves GNU's externally observable descendant-timeout behavior while avoiding a managed signal-handler requirement merely to keep the monitor from receiving its own group signal.

## Timeout and signal policy

The primary duration starts only after F4 publishes the child identity.  A zero duration means no primary timer.  At expiration, the configured signal (TERM by default) is delivered directly to the protected child and, unless `--foreground` was requested, to the new child process group.  For signals other than KILL and CONT, a CONT is then delivered to the same targets, matching the GNU monitor's stopped-child recovery behavior.

`--kill-after=DURATION` arms at most one second-stage timer after the primary timeout signal.  If the command is still live when that duration expires, KILL is delivered once.  A zero kill-after duration disables escalation.  Caller cancellation performs KILL cleanup and returns the command-internal status rather than leaving a supervised child behind.

`--verbose` diagnoses each timeout-generated signal once even though non-foreground POSIX delivery intentionally addresses both the child and its group.  On POSIX, HUP, INT, QUIT, TERM, and a catchable explicitly selected timeout signal received by the managed monitor are intercepted with the BCL `PosixSignalRegistration` facility and forwarded through the same F4 signal-delivery path; the command project does not call native `kill(2)` directly.

## Exit status

The command preserves GNU's principal boundaries:

- `124` when a timeout occurred and `--preserve-status` was not requested;
- the monitored command's translated status when no timeout occurred;
- the monitored command's translated status after timeout when `--preserve-status` was requested;
- `137` when timeout escalation (or an explicitly selected timeout signal) ends in KILL;
- `125` for `timeout`'s own parsing, setup, cancellation, or unsupported-control failures;
- `126` when the command is found but cannot be invoked; and
- `127` when it cannot be found.

## Platform behavior

Linux and macOS use the POSIX process-group/signal path.  Windows creates a new process group when requested, but F4 intentionally does not pretend that arbitrary POSIX group signals exist there.  For non-foreground TERM/KILL timeouts, the executor's declared process-tree cancellation substitution is used so descendants are not orphaned.  Other unsupported signal operations are surfaced as capability failures unless a later KILL escalation supplies a safe cleanup path.

## Tests

`Icod.CoreUtils.Timeout.Tests` covers normal propagation, zero-duration disabling, process-plus-group delivery, `--foreground`, `--preserve-status`, KILL escalation, verbose diagnostics, strict signed-signal rejection, C-style hexadecimal duration parsing, GNU long-option abbreviations, and a real `ProcessTestHost` timeout through `SystemProcessExecutor`.
