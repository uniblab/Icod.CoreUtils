# CKSUM(1)

## NAME

**cksum** — compute or check checksums and message digests

## SYNOPSIS

```text
cksum [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition, and unmatched patterns are preserved as literal operands. The operand `-` is preserved and retains its standard-input meaning.

With `--check`, this expansion applies to checksum-list operands supplied on the command line. File names read from checksum records are data from the checksum list and are not recursively reinterpreted as glob patterns.

## DESCRIPTION

`Icod.CoreUtils.CkSum` is a managed .NET implementation of GNU Coreutils `cksum(1)`, modeled on GNU Coreutils 9.11.

The command computes a checksum or message digest for each `FILE`. If no file is specified, or if a file operand is `-`, input is read from standard input. Without `--algorithm`, the traditional CRC checksum is used.

Unlike the dedicated digest commands, `cksum` can select among several checksum and digest algorithms and can emit tagged, untagged, Base64, or raw representations where supported. With `--check`, checksum-list files are verified against their referenced data.

## ALGORITHMS

The `--algorithm=TYPE` option accepts the following implemented names and aliases:

```text
crc, crc32, bsd
    Traditional CRC checksum. This is the default.

sysv
    System V checksum.

md5
    MD5.

sha1, sha-1
    SHA-1.

sha224, sha-224
    SHA-224.

sha256, sha-256
    SHA-256.

sha384, sha-384
    SHA-384.

sha512, sha-512
    SHA-512.

blake2b, b2
    BLAKE2b.

sm3
    SM3.

sha3, sha3-256
    SHA3-256.

sha3-224
    SHA3-224.

sha3-384
    SHA3-384.

sha3-512
    SHA3-512.
```

## OPTIONS

```text
-a, --algorithm=TYPE
    Select the checksum or digest algorithm.

--base64
    Encode a digest using Base64 rather than hexadecimal notation.

-c, --check
    Read checksum records from the named files and verify the referenced data.

--ignore-missing
    In --check mode, do not fail or report status for referenced files that are
    missing.

-l, --length=BITS
    Select a digest length when the chosen algorithm supports variable lengths.

--quiet
    In --check mode, do not print an OK line for each successfully verified file.

--raw
    Write the checksum or digest as raw bytes. Filenames are not written in this
    mode.

--status
    In --check mode, suppress normal verification output and use only the exit
    status.

--strict
    In --check mode, fail if a checksum list contains improperly formatted lines.

--tag
    Use tagged output for algorithms that support it.

--untagged
    Request untagged output.

-w, --warn
    In --check mode, warn about improperly formatted checksum lines.

-z, --zero
    End each generated checksum record with NUL instead of a newline.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

The specialized output controls `--tag`, `--untagged`, `--base64`, `--raw`, and `--length` require an explicit `--algorithm`. `--base64` and `--raw` are mutually exclusive. Tagged and untagged output cannot be selected together, and `--zero` cannot be combined with tagged output.

The check-only options `--ignore-missing`, `--quiet`, `--status`, `--strict`, and `--warn` require `--check`. Raw, Base64, tagged, and untagged output are not verification modes and cannot be combined with `--check`.

## CHECKING

With `--check`, each operand names a checksum-list file; `-` selects standard input. The selected algorithm determines how checksum records are parsed and verified. A successful verification requires every applicable checksum to match.

`--status` is intended for scripts, while `--quiet` suppresses successful per-file reports without hiding failures.

## EXIT STATUS

```text
0    Checksums were computed successfully, or every applicable checked checksum
     matched.
1    Invalid arguments, an unsupported option combination, an I/O error, a
     malformed checksum list under the selected policy, or a mismatch occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

The project targets .NET 10 and is intended for Windows, Linux, and macOS. File data is processed as bytes, so host text line-ending conventions do not alter the data being checksummed. Raw output writes digest bytes directly to standard output.

## AUTHORS

GNU `cksum` was written by Pádraig Brady and Q. Frank Xia.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`cksum(1)`, `sum(1)`, `b2sum(1)`, `md5sum(1)`, `sha1sum(1)`, `sha256sum(1)`, `sha512sum(1)`