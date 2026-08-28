# WC(1)

## NAME

**wc** — print newline, word, character, byte, and line-width counts

## SYNOPSIS

```text
wc [OPTION]... [FILE]...
```

## DESCRIPTION

`Icod.CoreUtils.WC` is a managed .NET implementation of GNU Coreutils `wc(1)`, modeled on GNU Coreutils 9.11.

Each input is processed in one streaming pass. With no count-selection option, newline, word, and byte counts are printed. Character, word, and maximum-line-width analysis uses incremental UTF-8 decoding while raw byte counts remain independent.

Invalid UTF-8 bytes contribute to byte counts and word-state handling but are not counted as decoded characters.

## OPTIONS

```text
-c, --bytes            print byte counts
-m, --chars            print decoded character counts
-l, --lines            print newline counts
-L, --max-line-length  print maximum display width
-w, --words            print word counts
    --files0-from=F    read NUL-terminated input file names from F
    --total=WHEN       WHEN is auto, always, only, or never
    --debug            emit implementation diagnostics
    --help             display command help and exit
    --version          display version information and exit
```

`--files0-from` cannot be combined with ordinary file operands.

## EXIT STATUS

```text
0    All requested counts were produced successfully.
1    Usage, input, or output processing failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Byte input is never newline-normalized before counting. Display-width calculation handles tabs at eight-column stops, carriage returns, and Unicode scalar widths in managed code.

## AUTHORS

GNU `wc` was written by Paul Rubin and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`wc(1)`, `cat(1)`
