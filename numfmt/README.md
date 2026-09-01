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

## PATHNAME GLOBBING

`numfmt` does not perform `Icod.CommandFramework` pathname glob expansion. Its operands are numeric data and formatting controls rather than pathname selectors, so `*`, `?`, and `**` are not interpreted as pathname patterns by `numfmt`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `numfmt` was written by Assaf Gordon.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`numfmt(1)`, `printf(1)`
