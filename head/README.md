# HEAD(1)

## NAME

**head** — output the first part of files

## SYNOPSIS

```text
head [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

`Icod.CoreUtils.Head` is a managed .NET implementation of GNU Coreutils `head(1)`, modeled on GNU Coreutils 9.11.

By default the first ten newline-delimited records of each input are written. The command can instead count bytes, use NUL-delimited records, or omit a requested number of records or bytes from the end.

## OPTIONS

```text
-c, --bytes=NUM         output the first NUM bytes; leading - means all but last NUM
-n, --lines=NUM         output the first NUM records; leading - means all but last NUM
-q, --quiet, --silent   never print file-name headers
-v, --verbose           always print file-name headers
-z, --zero-terminated   use NUL rather than newline as the record delimiter
    --help              display command help and exit
    --version           display version information and exit
```

The historical `-NUM` spelling is accepted as a line count.

## EXIT STATUS

```text
0    All requested input was processed successfully.
1    Usage, input, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Output is byte preserving. Seekable files use direct positioning; forward-only sources use bounded buffering and, when an end-relative operation requires it, a temporary spool.

## AUTHORS

GNU `head` was written by David MacKenzie and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`head(1)`, `tail(1)`, `cat(1)`
