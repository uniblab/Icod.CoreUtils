# TSORT(1)

## NAME

**tsort** — perform a topological sort

## SYNOPSIS

```text
tsort [OPTION] [FILE]
```

## DESCRIPTION

`tsort` writes a total order that is consistent with precedence pairs read from a file or standard input. Batch 24 is audited against GNU Coreutils 9.11.

With no operand, or when `FILE` is `-`, input is read from standard input. The compatibility option `-w` is accepted and ignored. `--help` and `--version` are supported.

## INPUT MODEL

Input is tokenized as bytes. Only space (`0x20`), horizontal tab (`0x09`), and line feed (`0x0A`) separate tokens. Tokens are consumed in pairs: `A B` means that `A` precedes `B`. An `A A` pair declares `A` without creating a self-loop. Duplicate relations are retained, matching GNU behavior. Because GNU stores node names as C strings, a token containing NUL is canonically identified by its bytes preceding the first NUL; the implementation preserves that compatibility rule explicitly.

## ORDERING AND LOOPS

Nodes are compared bytewise. Zero-predecessor nodes are seeded in bytewise order and processed through a FIFO queue. Successors are visited in reverse relation-input order, matching GNU Coreutils 9.11. When a loop blocks progress, the command reports the loop, removes one relation, continues producing output, and ultimately returns failure because the input was cyclic.

## GNU 9.11 CONFORMANCE MATRIX

| Area | Implemented contract |
|---|---|
| Operands | Zero or one `FILE`; `-` selects standard input; extra operands fail |
| Options | `-w` is accepted and ignored; `--help`, `--version`, abbreviations, permutation, and `--` use the Shared GNU-style parser |
| Separators | Exactly space, horizontal tab, and line feed |
| Relations | Pairs may cross lines; equal pairs declare nodes; duplicates remain distinct |
| Identity | Unsigned bytewise comparison with explicit first-NUL canonicalization matching GNU C-string storage |
| Ordering | Bytewise zero-node scan, FIFO ready queue, reverse relation-input successor traversal |
| Cycles | Stable member diagnostics, removal of one relation, continued output, final failure status |
| I/O | Incremental binary input, byte-preserving output, TAP, cancellation, caller-owned stream preservation |
| Diagnostics | Controlled option, operand, file, read, write, odd-token, and loop failures |

## ARCHITECTURE

- `Icod.CoreUtils.Shared.IO.ByteTokenReader` supplies reusable cancellation-aware byte tokenization.
- The relation graph and GNU-compatible loop recovery remain command-local.
- File and standard-stream I/O use `CommandContext` and TAP; caller-owned streams remain open.
- No command project references another command project.

## VALIDATION

The dedicated `tests/TSort.Tests` project includes the GNU 9.11 fixture shapes, separator and self-pair semantics, deterministic ordering, duplicate relations, cycle recovery, command-line handling, cancellation, and stream-ownership checks.

## AUTHORS

GNU `tsort` was written by Mark Kettenis.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`tsort(1)`, `sort(1)`
