# NUMFMT(1)

## NAME

**numfmt** — convert numbers to or from human-readable strings

## SYNOPSIS

```text
numfmt [OPTION]... [NUMBER]...
numfmt [OPTION]... < INPUT
```

## DESCRIPTION

`numfmt` implements the GNU Coreutils 9.11 human-readable number converter. It supports SI and IEC input/output scales, exact decimal parsing, configurable rounding, field selection, headers, delimiters, padding, suffixes, custom `%f` formats, grouping, and NUL-delimited records.

The implementation is managed and platform-neutral. Windows, Linux, and macOS are the required validation platforms; BSD-family behavior is best effort and should be identical except where host locale data differs.

## AUTHORS

GNU `numfmt` was written by Assaf Gordon.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`numfmt(1)`, `printf(1)`
