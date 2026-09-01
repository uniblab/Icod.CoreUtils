# PRINTF(1)

## NAME

**printf** — format and print data

## SYNOPSIS

```text
printf FORMAT [ARGUMENT]...
```

## DESCRIPTION

`printf` implements the GNU Coreutils 9.11 command-line formatter. It supports reusable format strings, positional operands, dynamic width and precision, C-style escapes, `%b`, shell-quoted `%q`, integer, character, string, and floating conversions.

The implementation is managed and platform-neutral. Windows, Linux, and macOS are the required validation platforms; BSD-family behavior is best effort and should be identical except where host locale data differs.

## PATHNAME GLOBBING

`printf` does not perform `Icod.CommandFramework` pathname glob expansion. Its operands are a format string and data arguments rather than pathname selectors, so `*`, `?`, and `**` remain command data when they reach `printf`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `printf` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`printf(1)`, `echo(1)`
