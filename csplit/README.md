# CSPLIT(1)

## NAME

**csplit** — split a file into sections determined by context lines

## SYNOPSIS

```text
csplit [OPTION]... FILE PATTERN...
```

## PATHNAME GLOBBING

Only the initial `FILE` operand is a Class B singular pathname slot. It may resolve from an unexpanded wildcard pattern only when exactly one pathname matches; an unmatched pattern remains literal and multiple matches are rejected. Every following `PATTERN` operand belongs to `csplit`'s own control language and is never pathname-expanded. `-` remains the standard-input sentinel.

## DESCRIPTION

`Icod.CoreUtils.CSplit` is a managed .NET implementation of GNU Coreutils `csplit(1)`, modeled on GNU Coreutils 9.11.

The command indexes the input by logical line, applies numeric and regular-expression split controls, and writes each selected piece to a numbered output file. `FILE` may be `-` for standard input. Input bytes are spooled and indexed so pattern matching does not sacrifice byte-for-byte output fidelity.

Unless quiet mode is selected, the byte count of each created piece is written to standard output.

## OPTIONS

```text
-b, --suffix-format=FORMAT  use printf-style integer FORMAT instead of %02d
-f, --prefix=PREFIX         use PREFIX instead of xx
-k, --keep-files            keep files already created if a later error occurs
    --suppress-matched      omit the matched line from output pieces
-n, --digits=DIGITS         use DIGITS suffix digits instead of 2
-s, -q, --silent, --quiet   do not print piece byte counts
-z, --elide-empty-files     do not create empty output files
    --help                  display command help and exit
    --version               display version information and exit
```

Controls include line numbers, `/REGEXP/[OFFSET]`, `%REGEXP%[OFFSET]`, finite repetitions such as `{COUNT}`, and indefinite repetition with `{*}`.

## EXIT STATUS

```text
0    All requested pieces were created successfully.
1    Usage, pattern evaluation, input, or output processing failed.
130  The operation was cancelled.
```

Without `--keep-files`, files created by the current invocation are removed when a later split failure requires rollback.

## PLATFORM NOTES

The implementation uses temporary spools and the shared managed GNU basic-regular-expression engine, so seekable source input and a native regex library are not required.

## AUTHORS

GNU `csplit` was written by Stuart Kemp and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`csplit(1)`, `split(1)`, `sed(1)`
