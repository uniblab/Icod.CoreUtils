# Batch 60 — ProcPs process lookup and working directories

Batch 60 adds the procps-ng 4.0.6 `pidof` and `pwdx` profiles as
`Icod.ProcPs.PidOf` and `Icod.ProcPs.Pwdx`.

## Shared path observation

`Icod.ProcPs.Shared` now contains `IProcProcessPathProvider` and the system
`SystemProcProcessPathProvider`. Path observations are made before and after a
shared `IProcessInspector` identity check so a PID reuse race cannot silently
attach executable/root/CWD data from a replacement process to an older ProcPs
snapshot.

Platform behavior is deliberately asymmetric where the operating systems are:

- Linux reads `/proc/PID/exe`, `/proc/PID/root`, and `/proc/PID/cwd` symlink
  targets and reports exact Linux-procfs provenance.
- macOS obtains executable paths with `proc_pidpath` and root/CWD paths with
  `proc_pidinfo(PROC_PIDVNODEPATHINFO)`, reporting Darwin-libproc provenance.
- Windows obtains the executable path from the process API but marks POSIX-style
  process root and arbitrary-process CWD unsupported. No directory is inferred
  from image location, the caller's CWD, environment variables, or other
  unrelated data.

## `pidof`

The shared `ProcProcessLookupCommand` implements the pinned 4.0.6 matching
profile: argv0/base-name/full-name identity, `/proc/PID/exe` identity, login
shell argv0 handling, setproctitle fallback, `-x` script matching, `-w` worker
matching, `-t` Linux lightweight tasks, `-c` privileged process-root filtering,
`-o` omission lists including `%PPID`, `-S`/compatibility `-d` separators,
`-s`, `-q`, compatibility `-n`/`-m`, descending PID presentation, and exit 1 on
no match. On Windows/macOS, where the process provider may be unable to expose
argv, an unavailable command line is distinguished from an observed empty
command line so executable identity remains usable without misclassifying Linux
kernel workers. The root check is enabled only for a privileged Unix caller, matching
upstream behavior; unsupported Windows POSIX-root semantics are therefore not
fabricated.

## `pwdx`

`pwdx` accepts one or more positive PID operands in either `PID` or `/proc/PID`
form, preserves the user's operand in output, and obtains the CWD through the
same reuse-protected path provider. Invalid PID syntax is fatal immediately,
while vanished, access-denied, or unsupported per-process observations are
reported and processing continues for later operands. Windows therefore gives a
controlled unsupported result rather than an unrelated path.

## Tests and validation

Dedicated command tests pin executable matching, descending output, single-shot
and custom separators, script matching, omission and `%PPID`, privileged root
filtering, lightweight-task selection, quiet/no-match behavior, host newline
normalization, multi-target `pwdx`, `/proc/PID` operands, partial vanished
processes, unsupported path observations, and invalid targets. ProcPs Shared
also gains live OS-gated tests for current-process executable and CWD capability
policy.

The solution includes both commands and tests in Debug, Staging, and Release.
All delivered source/text files are UTF-8, BOM-free, and LF-only. Runtime output
uses `Environment.NewLine`; raw-string help text first canonicalizes CRLF/lone CR
to LF before applying the host newline so Windows checkout conversion cannot
produce `\r\r\n`.

The implementation is ready for the repository's required Windows, Ubuntu, and
macOS validation. This execution environment does not provide the .NET SDK, so
runner validation remains the closure step.
