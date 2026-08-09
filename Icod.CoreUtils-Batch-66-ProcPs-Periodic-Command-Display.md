# Batch 66 — ProcPs periodic command display

## Scope

Batch 66 adds `Icod.ProcPs.Watch` with assembly name `watch` and its dedicated xUnit project. The implementation targets the procps-ng 4.0.6 `watch` behavior required by the project roadmap while reusing the neutral child-process executor and the ProcPs full-screen lifecycle introduced by Batch 65.

## Execution and cadence

The default command form runs through the host command shell (`/bin/sh -c` on POSIX hosts and `cmd.exe /D /S /C` on Windows), matching the upstream shell-oriented interface. `-x`/`--exec` bypasses the shell and passes every remaining argument through `ProcessRunOptions.Arguments`, preserving argument boundaries without manual quoting. Child standard output and standard error are redirected to one synchronized capture stream so both are presented on the watched screen.

The default interval is two seconds. `WATCH_INTERVAL` and `-n`/`--interval` accept decimal seconds, including a comma decimal separator for procps compatibility, and clamp values to the upstream 0.1-second through 31-day range. Ordinary mode waits the complete interval after a child finishes. `-p`/`--precise` instead schedules the next start against the monotonic timestamp of the original refresh loop so command runtime counts toward the interval and accumulated drift is avoided.

## Presentation and change semantics

The Batch 65 `IProcFullScreenTerminal` and signal-source contracts provide terminal classification, geometry, frame writes, resize notification, suspend restoration, resume re-entry, termination cancellation, and final restoration. `watch` requires an interactive standard-output terminal and rejects redirected output rather than emitting an unbounded refresh stream into a file or pipe.

Captured output is converted into a fixed visible screen model before comparisons. Tabs are expanded, nonprinting control characters are stripped, optional ANSI SGR sequences are either preserved (`--color`) or removed (`--no-color`), long lines wrap unless `--no-wrap` is selected, and output outside the visible body is discarded. `--chgexit` and `--equexit` therefore compare what the user can actually see, not hidden bytes beyond the viewport. A resize resets the comparison/difference baseline so geometry alone cannot be mistaken for a command-output change. `--no-rerun` redraws the last captured output at the new geometry without launching a child solely because of the resize.

`--differences` highlights cells changed from the prior visible refresh. `--differences=permanent` accumulates those highlights until a resize resets the screen. The standard two-line title displays interval, command, host/time, child elapsed time, and exit status; `--no-title` dedicates the full terminal to child output.

## Exit and lifecycle behavior

A non-zero child status can ring the terminal bell with `--beep`. `--errexit` returns the portable child status after displaying the failed refresh, including the shared process layer's conventional signal and launch-failure mappings. `--chgexit` and `--equexit` finish successfully when their visible-output conditions are met. Process-executor management exceptions return status 2. Cancellation returns the suite's full-screen cancellation status 130.

Terminal restoration is attempted on normal completion, option-driven exit, cancellation, process/terminal failure, and before POSIX suspension. Resume requests re-enter full-screen presentation before the next frame. Cleanup exceptions are best-effort and do not replace the command's primary result.

## Deliberate Batch 66 boundary

procps-ng 4.0.6 also accepts `--follow` and `--shotsdir`. `--follow` is accepted only when difference, chgexit, and equexit comparison options are absent, preserving the upstream conflict contract; Batch 66 retains full-screen refresh presentation rather than adding a separate keyboard-driven scrolling subsystem. `--shotsdir` is parsed for command-line compatibility but screenshot capture is deferred because the roadmap does not yet introduce an injectable interactive keyboard contract. No test writes directly to the test runner's standard output or standard error.

## Validation

The dedicated tests cover shell/direct execution, exact `--exec` argument boundaries, fixed and precise timing, `WATCH_INTERVAL`, differences, ANSI color, beep/error status propagation, equexit, visible-only chgexit, resize baseline reset, no-rerun, merged child output, titles, redirected output, cancellation, suspend/resume, help/version, and controlled option conflicts. Required `windows-latest`, `ubuntu-latest`, and `macos-latest` solution validation remains the repository closure step.
