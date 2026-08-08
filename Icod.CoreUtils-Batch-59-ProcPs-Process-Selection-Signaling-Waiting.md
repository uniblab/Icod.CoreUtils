# Batch 59 — ProcPs process selection, signaling, and waiting

## Status

Implemented against the current `main` branch and pinned to procps-ng 4.0.6. Repository build and runner validation remain required before the batch is closed.

## Projects

Batch 59 adds three executable projects and their dedicated test projects:

- `Icod.ProcPs.Pgrep` (`pgrep`)
- `Icod.ProcPs.Pkill` (`pkill`)
- `Icod.ProcPs.PidWait` (`pidwait`)

Procps-ng 4.0.6 installs `pidwait`; it does not install a `pwait` alias. No `Icod.ProcPs.PWait` project is added.

## Shared family engine

The three executables are thin profiles over `Icod.ProcPs.Shared.ProcMatchCommand`. This keeps process selection, regular-expression matching, option interpretation, exit-status policy, and race handling in one family implementation rather than allowing the commands to diverge.

The engine consumes the existing cross-suite process-control foundation for:

- reuse-aware process identities;
- signal parsing and delivery;
- queued integer signal values;
- signal-disposition observation;
- arbitrary-process waiting; and
- controlled vanished, access-denied, unsupported, canceled, and failed outcomes.

GNU extended regular expressions are compiled by the existing managed GNU ERE provider rather than delegated to a host `pgrep` or regex executable.

## Selection behavior

The common selector implements OR semantics within each selector type and AND semantics between different selector types. Supported criteria include:

- PID and parent PID;
- process group and POSIX session;
- real/effective user and real group, including native Unix account-name resolution;
- controlling terminal;
- Linux run state;
- process age;
- cgroup v2 path;
- Linux namespace equality and namespace-list restriction;
- Linux process environment, including comma-separated environment selectors;
- PID files, including `-F -` from standard input and `-L` lock policy;
- installed signal handlers through the Shared signal-disposition provider;
- ancestor exclusion;
- newest and oldest process selection;
- short-name versus full-command-line matching;
- exact matching and case-insensitive matching; and
- Linux lightweight-task enumeration for `pgrep -w`.

Selector value `0` for `-g` and `-s` is resolved against the calling process's process group or POSIX session, matching procps-ng semantics when those observations are available.

The command itself is always excluded. `--ignore-ancestors` additionally excludes the caller's observed ancestor chain.

Newest/oldest selection uses start-time observations and follows procps-ng's PID tie-break: for equal start times, the larger PID wins `--newest` and the smaller PID wins `--oldest`.

## `pgrep`

`pgrep` supports PID-only output, name/full-command output, custom delimiters, counts, quiet mode, inverse matching, shell quoting, lightweight tasks, and the shared selector surface.

Shell quoting follows the procps-ng safe-token set and single-quote escaping model. Count mode returns status 1 when the count is zero while still printing `0`.

## `pkill`

`pkill` reuses the same selected process set and adds:

- `-<signal>` and `--signal` signal selection;
- queued integer values with `-q` / `--queue`;
- `-e` / `--echo`;
- `-m` / `--mrelease`;
- partial-failure handling; and
- count output over the selected target set.

Queued values are delivered through `IProcessSignalProvider`; Batch 59 does not introduce a second `sigqueue` abstraction.

Linux `process_mrelease` is used only where the syscall is supported. A vanished target (`ESRCH`) after successful signal delivery does not downgrade the command to failure, matching procps-ng 4.0.6. Other memory-release failures produce status 1. `--mrelease` may be combined with queued signal delivery as in upstream 4.0.6.

## `pidwait`

`pidwait` uses the existing PID-reuse-aware Shared arbitrary-process wait contract instead of introducing a second pidfd abstraction.

All selected waits are initiated before the command blocks awaiting individual completions, reducing the race in which a later selected process exits while an earlier process is still being awaited. Vanished targets are ignored like procps-ng's `ESRCH` pidfd-open race. The command succeeds when at least one selected process was actually waited, returns status 1 when no selected target was successfully waited, and returns fatal status 3 for cancellation.

`-c` count output and `-e` waiting announcements are emitted before the blocking wait phase, matching procps-ng 4.0.6 ordering.

## Cross-platform behavior

Linux remains the authoritative procps-ng profile and supplies environment, namespaces, lightweight tasks, POSIX IDs, signal dispositions, and other procfs-specific observations.

Windows and macOS consume the corrected three-platform `Icod.ProcPs.Shared` process providers. Selection criteria are applied only to observations those providers expose; unsupported Linux-only data is not synthesized. Unix account names are resolved through libc with `/etc/passwd` and `/etc/group` fallbacks. Windows accepts numeric user/group selectors but does not pretend Windows account names are POSIX UID/GID names.

`pkill` signal delivery and `pidwait` waiting inherit the explicit platform capability and substitution policies of the cross-suite Shared process-control providers.

## Provider correction

`DotNetProcProcessProvider` now records process start time in `StartTimeTicks`. This supplies a defensible newest/oldest ordering on fallback platforms; Linux and macOS continue to use their stronger native observations where available.

## Test coverage

Dedicated tests cover, among other cases:

- GNU ERE matching;
- equal-start-time newest PID tie-breaking;
- newest selection without a pattern;
- AND-between selector semantics;
- comma-separated environment OR semantics;
- age filtering;
- signal-handler selection;
- shell-quoted full output;
- zero process-group/session self resolution;
- PID-file input through stdin;
- zero-match count status;
- multiple-pattern syntax failure;
- signal spelling and queued values;
- `-m` memory release, including queue coexistence and vanished-release races;
- partial `pkill` signal failures and count semantics;
- successful, vanished, and canceled waits;
- `pidwait -e` text; and
- `pidwait -c` output ordering on cancellation.

## Upstream reference

Behavior was checked against procps-ng 4.0.6 `src/pgrep.c`:

- https://gitlab.com/procps-ng/procps/-/raw/v4.0.6/src/pgrep.c

The upstream source implements `pgrep`, `pkill`, and `pidwait` as modes of one engine, establishes exit statuses 0/1/2/3, defines the shared selector grammar, treats group/session selector zero as self, uses PID tie-breaking for equal newest/oldest start times, ignores ESRCH races during kill/wait/release operations, and emits pidwait count/echo output before blocking.

## Validation note

The implementation has been statically checked for project XML validity, solution project/configuration integration, UTF-8/LF repository formatting, and source delimiter balance. The execution environment used to prepare this batch does not contain a .NET SDK and cannot download one because external DNS is unavailable, so `dotnet build` and xUnit execution could not be performed here. The roadmap therefore marks Batch 59 implemented and awaiting repository/runner validation rather than closed.
