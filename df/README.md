# DF(1)

## NAME

**df** — report filesystem space or inode usage

## SYNOPSIS

```text
df [OPTION]... [FILE]...
```

## DESCRIPTION

`Icod.CoreUtils.Df` is a managed .NET implementation of GNU Coreutils `df(1)`, modeled on GNU Coreutils 9.11.

For each named `FILE`, the command reports the filesystem containing that path. With no operands it reports the mounted filesystems returned by the shared filesystem-usage provider.

The default report shows filesystem source, total size, used space, available space, use percentage, and mount point. Inode mode replaces the byte-capacity fields with inode counts. Filesystem type filters, local-filesystem filtering, an explicit output-field list, and a grand-total row are also supported.

## OPTIONS

```text
-a, --all
    Include otherwise omitted filesystems.

-B, --block-size=SIZE
    Scale sizes by SIZE before printing.

-h, --human-readable
    Print powers of 1024.

-H, --si
    Print powers of 1000.

-i, --inodes
    Report inode information instead of block usage.

-k
    Use 1 KiB output blocks.

-l, --local
    Limit the report to local filesystems.

-P, --portability
    Use the POSIX output format.

-T, --print-type
    Include the filesystem type.

-t, --type=TYPE
    Include only filesystems of TYPE.

-x, --exclude-type=TYPE
    Exclude filesystems of TYPE.

--no-sync
    Do not synchronize filesystems before observing them. This is the default.

--output[=FIELD_LIST]
    Select output fields. Supported fields include source, fstype, itotal,
    iused, iavail, ipcent, size, used, avail, pcent, file, and target.

--sync
    Synchronize filesystems before observing them.

--total
    Append a grand-total row.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    The requested filesystem report was produced successfully.
1    An operational filesystem or synchronization error occurred.
2    Command-line or size-format usage was invalid.
```

## PLATFORM NOTES

Filesystem observations are obtained through the shared filesystem-usage and metadata abstractions. Size policy honors the command's explicit options and shared `DF_BLOCK_SIZE` environment policy.

`--sync` uses the native Unix `sync` entry point and is intentionally reported as unsupported on Windows. All ordinary reporting functionality remains provider-driven and cross-platform.
## AUTHORS

GNU `df` was written by Torbjörn Granlund, David MacKenzie, and Paul Eggert.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`df(1)`, `du(1)`, `stat(1)`
