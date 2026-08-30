# SORT(1)

## NAME

**sort** — sort, merge, or check records

## SYNOPSIS

```text
sort [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

Names read through `--files0-from=FILE` remain literal list entries and are not recursively glob-expanded. The `--files0-from` option value itself is likewise a literal control pathname.

## DESCRIPTION

`Icod.CoreUtils.Sort` is a managed .NET implementation of GNU Coreutils `sort(1)`, modeled on GNU Coreutils 9.11.

The production path is byte preserving and supports external sorting. Bounded in-memory runs are written to secure temporary storage and merged with bounded fan-in. The same engine supports ordinary sorting, merging already sorted inputs, and checking whether input is ordered.

## ORDERING OPTIONS

```text
-b, --ignore-leading-blanks
-d, --dictionary-order
-f, --ignore-case
-g, --general-numeric-sort
-h, --human-numeric-sort
-i, --ignore-nonprinting
-M, --month-sort
-n, --numeric-sort
-R, --random-sort
-r, --reverse
-V, --version-sort
-k, --key=KEYDEF
-t, --field-separator=CHAR
    --sort=WORD
```

Only compatible ordering modes may be combined within one global or key-local scope.

## OPERATION AND RESOURCE OPTIONS

```text
-c
-C
    --check[=diagnose-first|quiet|silent]  check ordering instead of sorting
-m, --merge                               merge already sorted files
-o, --output=FILE                         write output to FILE
-s, --stable                              stabilize equal records
-S, --buffer-size=SIZE                    set the in-memory run limit
-T, --temporary-directory=DIR             add a temporary-directory candidate
-u, --unique                              output the first record of an equal run
-z, --zero-terminated                     use NUL-delimited records
    --batch-size=N                        limit merge fan-in
    --files0-from=FILE                    read NUL-terminated input names
    --random-source=FILE                  source bytes for random ordering
    --help                                display command help and exit
    --version                             display version information and exit
```

## EXIT STATUS

```text
0    Sorting/merging succeeded, or checked input was ordered.
1    Check mode found input out of order.
2    Usage or operational sorting failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

External runs use the shared temporary-workspace infrastructure. Collation and specialized ordering modes are implemented through managed/shared repository services rather than shelling out to a platform `sort`.

## AUTHORS

GNU `sort` was written by Mike Haertel and Paul Eggert.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`sort(1)`, `uniq(1)`, `comm(1)`, `join(1)`, `shuf(1)`
