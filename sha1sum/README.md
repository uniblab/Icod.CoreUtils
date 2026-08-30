# SHA1SUM(1)

## NAME

**sha1sum** — compute or check SHA-1 message digests

## SYNOPSIS

```text
sha1sum [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition, and unmatched patterns are preserved as literal operands. The operand `-` is preserved and retains its standard-input meaning.

With `--check`, this expansion applies to checksum-list operands supplied on the command line. File names read from checksum records are data from the checksum list and are not recursively reinterpreted as glob patterns.

## DESCRIPTION

`Icod.CoreUtils.Sha1Sum` is a managed .NET implementation of GNU Coreutils `sha1sum(1)`, modeled on GNU Coreutils 9.11.

The command computes a SHA-1 message digest for each `FILE` and writes a checksum record to standard output. If no file is specified, or if a file operand is `-`, input is read from standard input. The command produces a 160-bit SHA-1 digest.

With `--check`, operands are interpreted as checksum-list files. Each listed digest is recomputed and compared with the recorded value.

## OPTIONS

```text
-b, --binary
    Read files in binary mode and use the binary checksum-record marker.

-c, --check
    Read checksum records from the named files and verify the referenced data.

--ignore-missing
    In --check mode, do not fail or report status for referenced files that are
    missing.

--quiet
    In --check mode, do not print an OK line for each successfully verified file.

--status
    In --check mode, suppress normal verification output; indicate success or
    failure through the exit status.

--strict
    In --check mode, fail if a checksum list contains improperly formatted lines.

-t, --text
    Read files in text mode and use the text checksum-record marker.

-w, --warn
    In --check mode, warn about improperly formatted checksum lines.

-z, --zero
    End each generated checksum record with NUL instead of a newline and disable
    filename escaping.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

The check-only options `--ignore-missing`, `--quiet`, `--status`, `--strict`, and `--warn` require `--check`.

## CHECKING

Checksum lists are read from each operand supplied with `--check`; `-` selects standard input. A successful verification requires every applicable checksum record to match. Malformed records and missing files are handled according to the selected checking options.

`--status` is useful for scripts because verification results are reflected only in the process exit status. `--quiet` suppresses successful per-file reports while retaining failures.

## EXIT STATUS

```text
0    Digests were computed successfully, or every applicable checked digest
     matched.
1    Invalid arguments, an I/O error, a malformed checksum list under the
     selected policy, or a checksum mismatch occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

The project targets .NET 10 and is intended for Windows, Linux, and macOS. File data is processed through binary streams so the bytes being digested are not rewritten for host line-ending conventions. `--binary` and `--text` control checksum-record mode and markers rather than translating the input file contents.

## AUTHORS

GNU `sha1sum` was written by Ulrich Drepper, Scott Miller, and David Madore.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`sha1sum(1)`, `cksum(1)`, `b2sum(1)`, `md5sum(1)`, `sha256sum(1)`, `sha512sum(1)`