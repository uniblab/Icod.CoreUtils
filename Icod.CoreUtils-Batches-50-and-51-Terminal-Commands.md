# Batches 50 and 51 — Terminal commands

## Scope

Batches 50 and 51 are the first command consumers of Completion Gate F3. They deliberately keep endpoint discovery, native mode retrieval, serialization, and mutation in `Icod.CoreUtils.Shared.Terminal`, while retaining GNU command-line policy inside the `tty` and `stty` projects.

## Batch 50 — `tty`

`tty` always inspects standard input. It reports the pathname or stable Windows console alias returned by the provider, prints `not a tty` for an available nonterminal observation, and implements `-s`, `--silent`, and `--quiet` without changing the status calculation. The command preserves GNU's distinct statuses for terminal input, nonterminal input, invalid usage, output failure, and an indeterminate terminal name.

## Batch 51 — `stty`

`stty` reads a complete immutable snapshot before reporting or editing. `-a` produces a human-readable complete report and `-g` delegates to the Gate F3 machine codec. `-F` and `--file` select an owned named-device endpoint; otherwise standard input remains a borrowed descriptor.

The pure command-local editor applies settings in argument order. It supports sane/raw/cooked profiles, common POSIX input/output/control/local flags and aliases, named control characters, line discipline where available, Linux and macOS native speed maps, bare numeric speed changes, separate `ispeed` and `ospeed`, and the reporting-only `speed` operand. POSIX mutations default to applying after output drains; `-drain` requests immediate application.

On Windows, complete console mode values are preserved. Defensible input settings (`isig`, `icanon`, and `echo`) and output settings (`opost` and `onlcr`) are mapped to native console-mode bits, and sane/raw profiles are available for console input. POSIX-only speed, parity, control-character, line-discipline, and drain behavior is rejected explicitly rather than approximated.

## Validation boundary

Dedicated command projects and test projects cover option parsing, injected-provider behavior, status boundaries, save/restore, reports, profiles, speed rules, selected devices, mutation timing, and Windows capability limits. Permanent validation remains the repository's Debug and Release builds and xUnit runs on Windows, Ubuntu, and macOS.
