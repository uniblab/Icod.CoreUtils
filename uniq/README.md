# UNIQ(1)

## NAME

**uniq** — report or omit adjacent repeated records

## SYNOPSIS

```text
uniq [OPTION]... [INPUT [OUTPUT]]
```

## PATHNAME GLOBBING

Only `INPUT` is a Class B singular pathname slot. A wildcard-bearing input must resolve to at most one pathname; an unmatched pattern remains literal and multiple matches are rejected. `OUTPUT` is a destination pathname and always remains literal. `-` retains its standard-stream meaning.

## DESCRIPTION

`Icod.CoreUtils.Uniq` is a managed .NET implementation of GNU Coreutils `uniq(1)`, modeled on GNU Coreutils 9.11.

Only adjacent records are compared. By default one representative of each adjacent equal group is written. Comparison can skip fields and characters, limit comparison width, ignore case, and use newline- or NUL-delimited records.

If input and output identify the same path, output is first written to a secure temporary workspace and then copied back, preventing destructive truncation.

## OPTIONS

```text
-c, --count              prefix output records with occurrence counts
-d, --repeated           output one representative of repeated groups
-D, --all-repeated[=METHOD] output every record in repeated groups
-f, --skip-fields=N      ignore the first N fields
    --group[=METHOD]     show all records separated into equal groups
-i, --ignore-case        ignore case during comparison
-s, --skip-chars=N       ignore the first N characters after skipped fields
-u, --unique             output only groups occurring once
-w, --check-chars=N      compare no more than N characters
-z, --zero-terminated    use NUL-terminated records
    --help               display command help and exit
    --version            display version information and exit
```

`--group` is incompatible with count/repeated/unique selection options.

## EXIT STATUS

```text
0    Adjacent-record processing completed successfully.
1    Usage, locale, input, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Records remain byte preserving. Character-sensitive skip/width and case behavior use the active managed locale profile.

## AUTHORS

GNU `uniq` was written by Richard M. Stallman and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`uniq(1)`, `sort(1)`, `comm(1)`
