# Batch 53 — util-linux `kill`

## Authority and ownership

Batch 53 implements `kill` as `Icod.UtilLinux.Kill`, pinned to util-linux 2.42.2. It deliberately does not implement the procps-ng `kill` profile, a shell builtin, or the historical GNU Coreutils implementation. No `Icod.ProcPs.Kill` project is introduced.

The general process identity, signal parsing, signal disposition, target, status, positive-PID delivery, and negative process-group delivery contracts remain in the current Shared incubation project and are treated as future `Icod.CommandFramework` candidates. The `kill` project owns only policy and host operations that are specific to the pinned util-linux command profile: exact command-name discovery, same-UID filtering, native PID `0`/`-1` conventions, queued signal payloads, Linux `/proc` signal-state presentation, and pidfd-protected timeout/inode semantics.

## Implemented command surface

The command supports the util-linux 2.42.2 signal grammar and target model: default `TERM`; `-SIGNAL`, `-s`, and `--signal`; numeric and named signals; signal zero; Linux `RT<N>`, `RTMIN+N`, and `RTMAX-N` forms; positive PIDs, zero, negative process groups, and `-1`; `PID:PIDFD_INODE`; and mixed command-name and PID operands.

Name lookup scans Linux `/proc` using the process `comm` name. By default it restricts matches to the invoking real UID; `-a`/`--all` removes that restriction. On non-Linux hosts, same-user command-name lookup is reported as unsupported rather than silently broadening the search. `--all` may use the host process API where available.

Presentation and selection include `-p`/`--pid`, `-l`/`--list`, `-L`/`--table`, shell-status number conversion, hexadecimal signal-mask decoding, `-r`/`--require-handler`, `--verbose`, and `-d`/`--show-process-state`. Linux process-state output decodes the `SigPnd`, `ShdPnd`, `SigBlk`, `SigIgn`, and `SigCgt` masks from `/proc/PID/status`.

`-q`/`--queue` uses `sigqueue(3)` for ordinary native targets and, as in util-linux, takes precedence over standalone pidfd-inode delivery when no timeout sequence is present. Repeated `--timeout MILLISECONDS SIGNAL` stages use Linux pidfds: the pidfd is opened before the initial signal, each delay polls that same descriptor, and a follow-up is sent only while the original process remains represented by it. When `PID:PIDFD_INODE` is supplied, Linux 6.9 or later is required and the pidfd inode is checked before delivery. This preserves the util-linux guarantee that delayed follow-up signals cannot be redirected to a recycled PID.

## Portability policy

Ordinary positive-PID signals and negative process-group targets flow through the F4 `IProcessSignalProvider`, including the existing controlled Windows substitutions. Linux-only extensions (`/proc` state, same-user name lookup, pidfds, pidfd inode validation) return controlled unsupported results elsewhere. Negative process groups continue through the F4 target model and therefore receive its controlled Windows unsupported result. Native PID `0` and `-1` forms are likewise rejected on Windows rather than being reinterpreted as unrelated process operations.

The command preserves util-linux aggregate statuses: `0` when every attempted target succeeds, `1` when every attempted target fails (or no target is actually eligible), and `64` for partial success across multiple targets.

## Tests

`tests/Kill.Tests` exercises default and explicit signal delivery, signal zero, negative process groups, signal-list translation, hexadecimal masks, mixed name/PID lookup, `--all`, PID-only mode, handler requirements, queued values and their precedence over standalone pidfd-inode delivery, repeated timeout stages, pidfd-inode forwarding, realtime-boundary grammar, partial-success status, Linux signal-state formatting, and the util-linux `-v` version alias.
