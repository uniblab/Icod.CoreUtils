# BASE64(1)

## NAME

**base64** — encode or decode data using Base64

## SYNOPSIS

```text
base64 [OPTION]... [FILE]
```

## PATHNAME GLOBBING

`FILE` is a Class B singular pathname slot. An unexpanded `*`, `?`, or exact-component `**` pattern is accepted only when it resolves to exactly one pathname. An unmatched pattern remains literal, while a pattern matching more than one pathname is rejected rather than changing command arity. `-` remains the standard-input sentinel.

## DESCRIPTION

`Icod.CoreUtils.Base64` is a managed .NET implementation of GNU Coreutils `base64(1)`, modeled on GNU Coreutils 9.11.

The command encodes binary input using the RFC 4648 Base64 representation or decodes Base64 data back to its original bytes. If `FILE` is omitted or is `-`, input is read from standard input. Results are written to standard output.

Encoded output is wrapped at 76 characters by default. Wrapping can be changed or disabled with `--wrap`. Decoding normally rejects characters outside the Base64 alphabet; `--ignore-garbage` permits non-alphabet characters to be skipped.

## OPTIONS

```text
-d, --decode
    Decode Base64 input instead of encoding it.

-i, --ignore-garbage
    When decoding, ignore characters outside the Base64 alphabet.

-w, --wrap=COLS
    Wrap encoded output after COLS characters. The default is 76.
    Specify 0 to disable wrapping.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

`COLS` must be a nonnegative decimal integer.

## OPERANDS

`FILE` names the input file. At most one file operand is accepted. If `FILE` is omitted or is `-`, `base64` reads from standard input.

## EXIT STATUS

```text
0    Encoding or decoding completed successfully.
1    Invalid arguments, invalid encoded input, or an I/O error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

The project targets .NET 10 and is intended for Windows, Linux, and macOS. Command data is processed through binary streams, so input and decoded output are byte-preserving and are not subject to host text line-ending translation.

## AUTHORS

GNU `base64` was written by Simon Josefsson.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`base64(1)`, `base32(1)`, `basenc(1)`