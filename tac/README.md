# TAC(1)

## NAME

**tac** — concatenate and print files in reverse record order

## SYNOPSIS

```text
tac [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

`Icod.CoreUtils.Tac` is a managed .NET implementation of GNU Coreutils `tac(1)`, modeled on GNU Coreutils 9.11.

For each input, records are located according to the selected separator and emitted in reverse order. With no operands, standard input is used. Fixed separators are matched as bytes; with `--regex`, the shared GNU-compatible regular-expression engine is used.

## OPTIONS

```text
-b, --before            attach the separator before the record
-r, --regex             interpret the separator as a regular expression
-s, --separator=STRING  use STRING instead of newline as the separator
    --help              display command help and exit
    --version           display version information and exit
```

An empty fixed separator selects NUL. An empty regular-expression separator is rejected.

## EXIT STATUS

```text
0    All requested inputs were reversed successfully.
1    Input or output processing failed.
2    Usage or regular-expression setup was invalid.
130  The operation was cancelled.
```

## PLATFORM NOTES

Seekable files are scanned directly; forward-only inputs are copied to a temporary spool to permit reverse access. Output bytes are not newline-normalized.

## AUTHORS

GNU `tac` was written by Jay Lepreau and David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`tac(1)`, `cat(1)`, `tail(1)`
