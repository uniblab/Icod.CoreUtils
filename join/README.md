# JOIN(1)

## NAME

**join** — join lines of two sorted files on a common field

## SYNOPSIS

```text
join [OPTION]... FILE1 FILE2
```

## DESCRIPTION

`Icod.CoreUtils.Join` is a managed .NET implementation of GNU Coreutils `join(1)`, modeled on GNU Coreutils 9.11.

The command performs a relational join of two sorted record streams. By default field 1 is the join field in both files. Exactly two inputs are required and both cannot simultaneously be standard input.

## OPTIONS

```text
-a FILENUM               also print unpairable lines from file 1 or 2
-e STRING                replace missing output fields with STRING
-i, --ignore-case        ignore case when comparing join fields
-j FIELD                 use FIELD as the join field for both files
-1 FIELD                 use FIELD as FILE1's join field
-2 FIELD                 use FIELD as FILE2's join field
-o FORMAT                select output fields; -o auto derives an output layout
-t CHAR                  use CHAR as the field/output separator
-v FILENUM               print only unpairable lines from the selected file
    --check-order        require correctly sorted input
    --nocheck-order      disable input-order checking
    --header             treat the first record of each input as a header
-z, --zero-terminated    use NUL-terminated records
    --help               display command help and exit
    --version            display version information and exit
```

Output formats accept `0` for the join field and `1.N` / `2.N` for fields from each input.

## EXIT STATUS

```text
0    The join completed successfully.
1    Usage, ordering, collation, input, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Records remain byte preserving and ordering uses the shared managed collation infrastructure rather than an external `join` implementation.

## AUTHORS

GNU `join` was written by Mike Haertel.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`join(1)`, `comm(1)`, `sort(1)`, `uniq(1)`
