# SYNC(1)

## NAME

**sync** — synchronize cached writes to persistent storage

## SYNOPSIS

```text
sync [OPTION] [FILE]...
```

## DESCRIPTION

This project implements GNU Coreutils `sync` against the GNU Coreutils 9.11
manual and the pinned `v9.11` source revision recorded in
`Icod.CoreUtils-Upstream-Version-Ledger.md`.

## OPTIONS AND OPERANDS

- With no operands, request a flush of all mounted filesystems.
- With operands, flush each named file using data-and-metadata semantics.
- `-d`, `--data` requests data-only flushing and requires at least one operand.
- `-f`, `--file-system` flushes the filesystem containing each operand.
- `--help` and `--version` follow the shared command conventions.
- `--data` and `--file-system` are mutually exclusive.
- Pathname operands use the Shared `*`, `?`, and `**` expansion policy.
- Failures are reported per operand and later operands are still attempted.

## PLATFORM NOTES

| Platform | Default file flush | `--data` | `--file-system` | No operands |
|---|---|---|---|---|
| Windows | `FlushFileBuffers` | Unsupported | Unsupported | Unsupported |
| Linux | `fsync` | `fdatasync` | `syncfs` | `sync` |
| macOS | `fsync` | Unsupported | One global `sync` fallback | `sync` |
| FreeBSD | Best-effort `fsync` | Best-effort `fdatasync` | One global `sync` fallback | Best-effort `sync` |

GNU Coreutils falls back to one global `sync()` request when built without a
`syncfs` primitive. This implementation preserves that behavior when the
provider reports global flushing but not filesystem-specific flushing.

Windows does not expose a process-level equivalent of Unix `sync()` or
`syncfs()` through the supported Gate B provider. Those requests therefore
produce a controlled nonzero result rather than silently succeeding.

Pathname-specific Unix flushing opens operands nonblocking, retries write-only
access when read-only access fails, clears nonblocking mode before flushing,
and reports open, synchronization, and close failures. This permits directories
and special files where the host filesystem and permissions allow them.

Cancellation is cooperative. It is checked before and between native calls,
but an individual kernel flush cannot necessarily be interrupted after entry.

## AUTHORS

GNU `sync` was written by Jim Meyering and Giuseppe Scrivano.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`sync(1)`
