# BASENC(1)

## NAME

**basenc** — encode or decode data using a selected base encoding

## SYNOPSIS

```text
basenc ENCODING [OPTION]... [FILE]
```

## PATHNAME GLOBBING

`FILE` is a Class B singular pathname slot. An unexpanded `*`, `?`, or exact-component `**` pattern is accepted only when it resolves to exactly one pathname. An unmatched pattern remains literal, while a pattern matching more than one pathname is rejected rather than changing command arity. Encoding-selection and other option arguments are not pathname-expanded. `-` remains the standard-input sentinel.

## DESCRIPTION

`Icod.CoreUtils.BasEnc` is a managed .NET implementation of GNU Coreutils `basenc(1)`, modeled on GNU Coreutils 9.11.

The command encodes binary input using a selected binary-to-text representation or decodes that representation back to its original bytes. If `FILE` is omitted or is `-`, input is read from standard input. Results are written to standard output.

`ENCODING` denotes one of the required encoding-selection options listed below, such as `--base64` or `--base32`; it is not a separate positional operand.

Encoded output is wrapped at 76 characters by default. Wrapping can be changed or disabled with `--wrap`. When decoding, `--ignore-garbage` permits characters outside the selected encoding alphabet to be skipped where the selected codec supports that operation.

## ENCODINGS

```text
--base64
    RFC 4648 Base64.

--base64url
    File- and URL-safe Base64.

--base58
    Visually unambiguous Base58.

--base32
    RFC 4648 Base32.

--base32hex
    Extended-hex Base32.

--base16
    Hexadecimal Base16.

--base2msbf
    Bit-string representation, most-significant bit first.

--base2lsbf
    Bit-string representation, least-significant bit first.

--z85
    ZeroMQ Z85.
```

At least one encoding-selection option is required. If more than one is supplied, the last selected encoding is used.

## OPTIONS

```text
-d, --decode
    Decode input instead of encoding it.

-i, --ignore-garbage
    When decoding, ignore characters outside the selected alphabet.

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

`FILE` names the input file. At most one file operand is accepted. If `FILE` is omitted or is `-`, `basenc` reads from standard input.

## EXIT STATUS

```text
0    Encoding or decoding completed successfully.
1    Invalid arguments, a missing encoding selection, invalid encoded input,
     or an I/O error occurred.
130  The operation was cancelled.
```

## PLATFORM NOTES

The project targets .NET 10 and is intended for Windows, Linux, and macOS. Command data is processed through binary streams, so input and decoded output are byte-preserving and are not subject to host text line-ending translation.

## AUTHORS

GNU `basenc` was written by Simon Josefsson and Assaf Gordon.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`basenc(1)`, `base32(1)`, `base64(1)`