# SHUF(1)

## NAME

**shuf** — generate random permutations

## SYNOPSIS

```text
shuf [OPTION]... [FILE]
shuf -e [OPTION]... [ARG]...
shuf -i LO-HI [OPTION]...
```

## DESCRIPTION

`Icod.CoreUtils.Shuf` is a managed .NET implementation of GNU Coreutils `shuf(1)`, modeled on GNU Coreutils 9.11.

Input can come from one file or standard input, command-line records with `--echo`, or an inclusive integer range with `--input-range`. Records are preserved as bytes.

## OPTIONS

```text
-e, --echo                treat each ARG as an input record
-i, --input-range=LO-HI   treat each integer LO through HI as an input record
-n, --head-count=COUNT    output at most COUNT records
-o, --output=FILE         write result to FILE instead of standard output
    --random-source=FILE  obtain random bytes from FILE
-r, --repeat              allow output records to repeat
-z, --zero-terminated     use NUL-terminated records
    --help                display command help and exit
    --version             display version information and exit
```

`--echo` and `--input-range` are mutually exclusive. Ordinary file mode accepts at most one file operand.

## EXIT STATUS

```text
0    Randomized output completed successfully.
1    Usage, randomness, input, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Randomness and record handling are managed and platform-neutral. `--random-source` can provide deterministic or externally supplied random bytes.

## AUTHORS

GNU `shuf` was written by Paul Eggert.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`shuf(1)`, `sort(1)`, `head(1)`
