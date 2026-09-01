# DU(1)

## NAME

**du** — estimate file and directory usage

## SYNOPSIS

```text
du [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. With no operands, the current directory remains the implicit operand. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

Names read through `--files0-from=FILE` remain literal list entries and are not recursively glob-expanded. A `**` pattern only selects initial operands; `du`'s own directory traversal remains governed by its normal traversal options and semantics.

## DESCRIPTION

`Icod.CoreUtils.DU` is a managed .NET implementation of GNU Coreutils `du(1)`, modeled on GNU Coreutils 9.11.

The command recursively calculates allocated size, apparent size, or inode usage through the shared filesystem metadata and traversal providers. With no operands it reports the current directory.

Hard-link accounting, symbolic-link traversal, one-filesystem traversal, directory aggregation, depth filtering, exclusion patterns, thresholds, timestamp reporting, NUL-delimited pathname input, and NUL-delimited output are supported.

## OPTIONS

```text
-0, --null
    End each output record with NUL.

-a, --all
    Write counts for files as well as directories.

--apparent-size
    Report logical size rather than allocated size.

-B, --block-size=SIZE
    Scale sizes by SIZE before printing.

-b, --bytes
    Report apparent size in one-byte units.

-c, --total
    Produce a grand total.

-d, --max-depth=N
    Print directory totals only through depth N.

-h, --human-readable
    Print powers of 1024.

--inodes
    Report inode usage rather than byte usage.

-k
    Use 1 KiB output blocks.

-m
    Use 1 MiB output blocks.

-H, -D, --dereference-args
    Follow symbolic links named as command-line roots.

-L, --dereference
    Follow all symbolic links.

-P, --no-dereference
    Never follow symbolic links.

-l, --count-links
    Count multiply linked files once for each name encountered.

-S, --separate-dirs
    Do not include descendant directory totals in parent totals.

--si
    Print powers of 1000.

-s, --summarize
    Display only a total for each argument.

-t, --threshold=SIZE
    Exclude entries outside the signed threshold.

-x, --one-file-system
    Skip directories on filesystems different from the starting point.

-X, --exclude-from=FILE
    Read exclusion patterns from FILE.

--exclude=PATTERN
    Exclude matching paths.

--files0-from=F
    Read NUL-terminated input pathnames from F; `-` selects standard input.

--time[=WORD]
    Show the selected latest timestamp. Supported explicit selectors are
    atime/access/use and ctime/status; without WORD, modification time is used.

--time-style=STYLE
    Format reported timestamps as full-iso, long-iso, iso, or +FORMAT.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

`--summarize` conflicts with `--all` and with a positive explicit `--max-depth`. Ordinary pathname operands cannot be combined with `--files0-from`.

## EXIT STATUS

```text
0    All requested usage calculations completed successfully.
1    One or more inputs could not be traversed or reported.
2    Command-line or size-format usage was invalid.
```

## PLATFORM NOTES

Traversal is performed through the repository's filesystem abstractions rather than an external `du` executable. Allocated-size availability therefore follows the metadata capabilities of the active platform provider.

Unless inode mode is selected, output scaling follows the shared `DU_BLOCK_SIZE` policy when no explicit size option is supplied.

## AUTHORS

GNU `du` was written by Torbjörn Granlund, David MacKenzie, Paul Eggert, and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`du(1)`, `df(1)`, `stat(1)`
