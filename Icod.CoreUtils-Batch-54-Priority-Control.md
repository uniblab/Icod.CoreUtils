# Batch 54 — Priority Control

## Authority

- `Icod.CoreUtils.Nice` follows GNU Coreutils 9.11 `nice`.
- `Icod.UtilLinux.Renice` follows util-linux 2.42.2 `renice`.

The two commands share the Completion Gate F4 process-priority foundation but retain their upstream-specific command grammars, diagnostics, and exit-status policies.

## F4 priority selector extension

`ProcessTarget` remains the identity-oriented target used by signals, waits, and existing single-process/group operations. Batch 54 adds `ProcessPriorityTarget` for the selector semantics specific to `getpriority(2)` and `setpriority(2)`:

- process selectors;
- process-group selectors;
- user-ID selectors;
- nonnegative selector zero, whose POSIX meaning depends on the selector class.

`IProcessPrioritySelectorProvider` is a companion to the existing `IProcessPriorityProvider`, so the established F4 provider contract remains source-compatible for existing consumers. `SystemProcessPrioritySelectorProvider` supplies the selector-specific implementation in Shared: POSIX process, group, and user selectors are forwarded to the existing Shared native boundary, while Windows individual-process selectors compose the existing `SystemProcessPriorityProvider` approximation. Group and user selectors on Windows return `Unsupported` rather than pretending to have POSIX semantics. The existing signal-mask capability bit introduced by Batch 52 remains unchanged; user-priority targeting receives its own later capability bit.

## GNU nice

`nice` parses GNU 9.11 `-n`/`--adjustment`, `--help`, `--version`, and the historical `-10`, `--10`, and `-+10` adjustment forms. Adjustments are clamped to GNU's accepted `-39..39` range before application.

For command execution, `nice` adjusts its own F4 process priority before invoking the F4 process executor. POSIX children therefore inherit the priority at creation, avoiding the race inherent in starting a child first and renicing it afterward. A permission-only failure to improve niceness is diagnosed but does not suppress command execution, matching GNU behavior; other priority failures are command failures. Windows additionally applies the resulting approximate priority class through the F4 child-start callback because Windows inheritance rules are not equivalent to POSIX niceness.

The F4 executor owns executable lookup, argument vectors, launch classification, waiting, and termination translation. `nice` therefore preserves child statuses and the conventional 126/127 launch boundary without command-local process APIs.

## util-linux renice

`renice` implements the util-linux 2.42.2 profile:

- absolute priority by default;
- `--priority` for explicit absolute mode;
- `--relative` for explicit relative mode;
- `-n` as absolute unless `POSIXLY_CORRECT` is present, in which case it is relative;
- `-p`/`--pid`, `-g`/`--pgrp`, and `-u`/`--user`, with each selector affecting succeeding operands;
- username lookup before numeric UID parsing, matching `getpwnam(3)` precedence;
- multiple mixed target classes;
- POSIX selector zero;
- post-change priority observation and util-linux success text;
- continued processing after individual target failures and exit status 1 when any target fails.

Username resolution uses `Icod.CoreUtils.Shared.Platform.IIdentityProvider`; priority reads and writes use only `IProcessPrioritySelectorProvider`. No priority P/Invoke exists in the command project.

## Platform policy

POSIX privilege/resource-limit failures are returned through `ProcessOperationResult` and translated into command diagnostics. Vanished targets remain controlled failures. Windows process targets use the existing approximate priority-class mapping. Windows process-group and user targets are explicitly unsupported because changing arbitrary POSIX-style groups or all processes owned by a UID has no defensible one-to-one process-priority-class operation.
