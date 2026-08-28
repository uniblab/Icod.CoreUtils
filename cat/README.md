# CAT(1)

## NAME

**cat** — concatenate files and write them to standard output

## SYNOPSIS

```text
cat [OPTION]... [FILE]...
```

## DESCRIPTION

`Icod.CoreUtils.Cat` is a managed .NET implementation of GNU Coreutils `cat(1)`, modeled on GNU Coreutils 9.11.

With no display transformation selected, input is copied as bytes without decoding or newline normalization. With no operands, or for an operand named `-`, the command reads standard input. Transformation state continues across operands so numbering and blank-line squeezing treat the concatenation as one logical stream.

## OPTIONS

```text
-A, --show-all          equivalent to -vET
-b, --number-nonblank   number nonempty output lines; takes precedence over -n
-e                      equivalent to -vE
-E, --show-ends         display $ at the end of each line
-n, --number            number all output lines
-s, --squeeze-blank     suppress repeated empty output lines
-t                      equivalent to -vT
-T, --show-tabs         display TAB characters as ^I
-u                      accepted for GNU compatibility and otherwise ignored
-v, --show-nonprinting  use visible notation for nonprinting bytes
    --help              display command help and exit
    --version           display version information and exit
```

## EXIT STATUS

```text
0    Every requested input was copied successfully.
1    Usage, input, or output processing failed.
130  The operation was cancelled.
```

A failure opening one operand does not prevent later operands from being attempted.

## PLATFORM NOTES

Production execution uses binary standard streams. Unmodified input bytes, embedded NULs, original line endings, and an unterminated final record are preserved exactly.
## AUTHORS

GNU `cat` was written by Torbjörn Granlund and Richard M. Stallman.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`cat(1)`, `tac(1)`, `tee(1)`, `head(1)`, `tail(1)`
