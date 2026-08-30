# SUM(1)

## NAME

**sum** — checksum and count blocks in files

## SYNOPSIS

```text
sum [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition, and unmatched patterns are preserved as literal operands. The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

`Icod.CoreUtils.Sum` is a managed .NET implementation of GNU Coreutils `sum(1)`, modeled on GNU Coreutils 9.11.

The command computes a traditional numeric checksum and block count for each `FILE`. If no file is specified, or if a file operand is `-`, input is read from standard input.

The default is the BSD rotating checksum with 1 KiB blocks. The System V algorithm can be selected with `--sysv`; it reports 512-byte blocks.

## OPTIONS

```text
-r
    Use the BSD checksum algorithm with 1 KiB blocks. This is the default.

-s, --sysv
    Use the System V checksum algorithm with 512-byte blocks.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## OUTPUT

For each input, `sum` writes the numeric checksum followed by the block count. When multiple files are supplied, the filename is also written so the records can be distinguished.

## EXIT STATUS

```text
0    Every requested checksum was computed successfully.
1    Invalid arguments or an I/O error occurred for one or more inputs.
130  The operation was cancelled.
```

## PLATFORM NOTES

The project targets .NET 10 and is intended for Windows, Linux, and macOS. Input is processed as bytes, so checksum results do not depend on host text line-ending translation.

## AUTHORS

GNU `sum` was written by Kayvan Aghaiepour and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`sum(1)`, `cksum(1)`