# COMM(1)

## NAME

**comm** — compare two sorted files record by record

## SYNOPSIS

```text
comm [OPTION]... FILE1 FILE2
```

## PATHNAME GLOBBING

`FILE1` and `FILE2` are independent Class B singular pathname slots. Each wildcard-bearing slot is expanded separately and must resolve to at most one pathname; matches from one slot never spill into the other. An unmatched pattern remains literal, while multiple matches for either slot are an error. `-` retains its standard-input meaning.

## DESCRIPTION

`Icod.CoreUtils.Comm` is a managed .NET implementation of GNU Coreutils `comm(1)`, modeled on GNU Coreutils 9.11.

The command compares two sorted byte-record streams and normally writes three columns: records unique to `FILE1`, records unique to `FILE2`, and records common to both. Exactly two inputs are required and both cannot simultaneously be standard input.

## OPTIONS

```text
-1                          suppress records unique to FILE1
-2                          suppress records unique to FILE2
-3                          suppress records common to both files
    --check-order           require correctly sorted input
    --nocheck-order         do not check input ordering
    --output-delimiter=STR  separate columns with STR; empty STR selects NUL
    --total                 append first-only, second-only, and common counts
-z, --zero-terminated       use NUL-terminated records
    --help                  display command help and exit
    --version               display version information and exit
```

## EXIT STATUS

```text
0    Comparison completed successfully.
1    Usage, collation, ordering, input, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Records are preserved as bytes. Comparison uses the shared managed collation environment, giving the command one ordering architecture across Windows, Linux, and macOS.

## AUTHORS

GNU `comm` was written by Richard M. Stallman and David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`comm(1)`, `join(1)`, `sort(1)`, `uniq(1)`
