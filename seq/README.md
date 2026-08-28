# SEQ(1)

## NAME

**seq** — print a sequence of numbers

## SYNOPSIS

```text
seq [OPTION]... LAST
seq [OPTION]... FIRST LAST
seq [OPTION]... FIRST INCREMENT LAST
```

## DESCRIPTION

`Icod.CoreUtils.Seq` is a managed .NET implementation of GNU Coreutils `seq(1)`, modeled on GNU Coreutils 9.11.

The command prints numbers from `FIRST` through `LAST` using `INCREMENT`. When omitted, `FIRST` and `INCREMENT` default to 1.

The implementation uses invariant numeric parsing, preserves decimal arithmetic when the operands can be represented exactly as decimals, and supports a printf-style floating-point format for customized output.

## OPTIONS

```text
-f, --format=FORMAT
    Use a printf-style floating-point FORMAT.

-s, --separator=STRING
    Use STRING between output values instead of a newline.

-w, --equal-width
    Equalize output width by padding with leading zeroes.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

`--format` and `--equal-width` cannot be used together. A zero increment is rejected.

## EXIT STATUS

```text
0    The requested sequence was written successfully.
1    Usage, numeric parsing, formatting, or output failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Numeric parsing and formatting use invariant culture, so decimal points and command syntax do not vary with the host locale. The default separator is the host environment's newline sequence.
## AUTHORS

GNU `seq` was written by Ulrich Drepper.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`seq(1)`, `printf(1)`
