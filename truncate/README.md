# TRUNCATE(1)

## NAME

**truncate** — shrink or extend files to a specified size

## SYNOPSIS

```text
truncate [OPTION]... FILE...
```

## PATHNAME GLOBBING

Target `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The reference pathname supplied by `--reference=RFILE` remains literal and is not glob-expanded.

## DESCRIPTION

This directory implements GNU `truncate` behavior for Icod.CoreUtils.

## AUTHORITATIVE BASELINE

The command is based on GNU Coreutils 9.11, pinned at tag `v9.11`, commit
`c01fd163a47468a8296fb369f5233853bb551bb6`.  The primary behavioral sources
are the GNU Coreutils 9.11 manual and `src/truncate.c` from that release.  The
Linux man-pages rendering of `truncate(1)` is used as a secondary synopsis.

## IMPLEMENTED INTERFACE

- `-c`, `--no-create`
- `-o`, `--io-blocks`
- `-r`, `--reference=RFILE`
- `-s`, `--size=SIZE`
- `--help`
- `--version`
- multiple target files with continuation after per-file failures
- absolute, relative (`+` and `-`), at-most (`<`), at-least (`>`), round-down
  (`/`), and round-up (`%`) size modes
- binary suffixes `K` through `Q`, decimal suffixes `KB` through `QB`, and IEC
  suffixes `KiB` through `QiB`
- GNU-compatible suffix-only forms such as `K`, plus lowercase `k`, `m`, `g`,
  and `t` forms including `kB` and `kiB`
- reference-based sizing and per-target I/O-block multiplication
- checked arithmetic, zero-divisor rejection, and negative-result clamping
- sparse-aware extension through the Gate B filesystem capability layer
- internal wildcard expansion for target operands using `*`, `?`, and `**`

## WILDCARD OPERAND EXPANSION

Icod.CoreUtils extends GNU `truncate` with internal pathname expansion for
target operands.  This behavior is implemented by the command rather than
being delegated exclusively to the invoking shell.

Supported wildcard forms are:

- `*` matches zero or more characters within one path segment.
- `?` matches one character within one path segment.
- `**` matches zero or more directory levels recursively.

A single `*` or `?` never crosses a directory separator.  `**` is the form
used when recursive directory traversal is intended.  Matches are processed
in deterministic ordinal order, and an unmatched pattern is preserved and
processed as a literal operand.

Because expansion is performed internally, shell quoting does not necessarily
suppress wildcard expansion.  For example, an operand such as `'*.dat'` that
reaches `truncate` as the literal text `*.dat` is still eligible for Icod
wildcard expansion.  This is an intentional cross-platform Icod extension and
is not strict GNU `truncate` behavior.

## PLATFORM NOTES

The ordinary byte-size modes use .NET file lengths on every supported runtime.
Sparse extension is delegated to `SystemFileSystemOperations`; unsupported
sparse marking falls back to a normal logical-length extension because the
command's defining contract is the resulting file length and zero-filled read
semantics.

`--io-blocks` requires a preferred per-file I/O block size:

| Platform | Source |
|---|---|
| Windows | allocation-unit size from `GetDiskFreeSpaceW` |
| Linux | `statx.stx_blksize` for the open file descriptor |
| macOS | `fstat().st_blksize` using the Darwin ABI |
| FreeBSD | best-effort `fstat().st_blksize` using the contemporary ABI in FreeBSD `sys/sys/stat.h` |

Windows does not expose a direct `st_blksize` equivalent.  Its filesystem
allocation-unit size is the closest stable native quantity and is documented
here as an intentional cross-platform interpretation.  FreeBSD support is
best effort because it is not part of the project's current CI matrix.

Regular seekable files are the conformance target.  GNU opens special-file
operands with `O_NONBLOCK`; .NET `FileStream` does not expose one portable
open mode with identical FIFO and device semantics.  Such operands therefore
use runtime-native `FileStream` behavior and produce controlled diagnostics
when their length cannot be queried or changed.

## TESTS

`tests/Truncate.Tests` contains command-level grammar, arithmetic, creation,
reference, error-continuation, cancellation, output-failure,
platform-injection, and native I/O-block-size tests.  CI coverage is expected on `windows-latest`,
`ubuntu-latest`, and `macos-latest`.

## AUTHORS

GNU `truncate` was written by Pádraig Brady.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`truncate(1)`, `stat(1)`
