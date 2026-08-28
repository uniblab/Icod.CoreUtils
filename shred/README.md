# SHRED(1)

## NAME

**shred** — overwrite files to make data recovery harder

## SYNOPSIS

```text
shred [OPTION]... FILE...
```

## DESCRIPTION

`Icod.CoreUtils.Shred` is a managed .NET implementation of GNU Coreutils `shred(1)`, modeled on GNU Coreutils 9.11.

The command repeatedly overwrites each target with generated data. By default three overwrite iterations are performed. An optional final zero pass can be added, and files can be truncated and removed after overwriting.

An operand of `-` sends overwrite bytes to standard output. Because that path can be non-seekable, an explicit `--size` is required when the output length cannot otherwise be determined.

## OPTIONS

```text
-f, --force
    Change permissions to allow writing when necessary.

-n, --iterations=N
    Perform N overwrite iterations instead of the default 3.

--random-source=FILE
    Obtain random bytes from FILE.

-s, --size=N
    Shred exactly the selected byte count.

-u, --remove[=HOW]
    Truncate and remove after overwriting. HOW may be unlink, wipe, or wipesync;
    wipesync is the default for -u.

-v, --verbose
    Report overwrite progress.

-x, --exact
    Do not round file sizes up to a full block.

-z, --zero
    Add a final overwrite pass containing zeros.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    Every requested overwrite completed successfully.
1    Usage, random-source, file, overwrite, removal, output, or cancellation
     processing failed.
```

Unlike most commands in the suite, cancellation is currently reported as command failure after the diagnostic `operation canceled`, rather than as exit status 130.

## DATA-ERASURE LIMITATIONS

`shred` can overwrite only the storage blocks exposed through the selected file path. It cannot guarantee physical erasure on copy-on-write or journaled filesystems, SSDs with remapping or wear leveling, snapshots, backups, RAID/controller caches, remote storage, or other layers that retain historical copies.

For highly sensitive data, storage encryption and device-specific secure-erasure procedures are normally stronger guarantees than pathname-level overwrite alone.

## PLATFORM NOTES

Production standard output is binary when `-` is used, so overwrite bytes are not passed through text encoding or newline conversion. Random generation and overwrite sequencing are implemented in managed code over host file streams.

## AUTHORS

GNU `shred` was written by Colin Plumb.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`shred(1)`, `rm(1)`, `sync(1)`
