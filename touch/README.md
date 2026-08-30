# TOUCH(1)

## NAME

**touch** — change file access and modification times

## SYNOPSIS

```text
touch [OPTION]... FILE...
```

## PATHNAME GLOBBING

Target `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands and then follow ordinary `touch` creation/update semantics.

`--reference=FILE` remains a literal control pathname and is not glob-expanded.

## DESCRIPTION

`Icod.CoreUtils.Touch` is a managed .NET implementation of GNU Coreutils `touch(1)`, modeled on GNU Coreutils 9.11.

By default both access and modification times are set to the current time. Missing files are created unless `--no-create` is selected. Time values can come from GNU-style date parsing, a reference file, or the compact timestamp syntax used by `-t`.

Timestamp observation and mutation use the shared filesystem metadata provider.

## OPTIONS

```text
-a, --access
    Change only the access time.

-c, --no-create
    Do not create missing files.

-d, --date=STRING
    Parse STRING and use it instead of the current time.

-f
    Accepted and ignored for compatibility.

-h, --no-dereference
    Affect a symbolic link itself instead of its referent.

-m, --modification
    Change only the modification time.

-r, --reference=FILE
    Use FILE's times instead of the current time.

--time=WORD
    Select access/atime/use or modify/mtime.

-t STAMP
    Use [[CC]YY]MMDDhhmm[.ss].

--help
    Display command help and exit.

--version
    Display version information and exit.
```

The explicit date-producing options are mutually constrained in the same command invocation. `-d` uses the shared GNU date parser; `-t` uses the compact touch timestamp parser.

## EXIT STATUS

```text
0    Every requested timestamp update completed successfully.
1    Usage, date parsing, file creation, metadata observation, or timestamp
     mutation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

A `FILE` operand of `-` refers to `/dev/stdout` on Unix-like hosts. This operation is explicitly unsupported on Windows because there is no equivalent pathname for standard output in the current implementation.

No-follow timestamp changes depend on the metadata provider's ability to mutate timestamps on pathname indirections.

## AUTHORS

GNU `touch` was written by Paul Rubin, Arnold Robbins, Jim Kingdon, David MacKenzie, and Randy Smith.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`touch(1)`, `date(1)`, `stat(1)`
