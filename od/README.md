# od

`od` writes an unambiguous textual representation of binary input. This Batch 13 implementation is based on GNU Coreutils 9.11 (`src/od.c`, tag `v9.11`, commit `c01fd163a47468a8296fb369f5233853bb551bb6`).

Implemented features include:

- all documented modern options and traditional shorthands;
- the traditional offset and pseudo-address forms;
- concatenated file operands and binary standard input;
- decimal, octal, hexadecimal, or suppressed addresses;
- native, little-endian, and big-endian values;
- named-character, escaped-character, signed, unsigned, octal, hexadecimal, half, bfloat16, single, and double output;
- repeated-line suppression and `-v`;
- skip and read limits with GNU size suffixes;
- NUL-terminated printable-string discovery;
- configurable widths reconciled against the least common multiple of value sizes, with GNU-compatible warnings;
- printable `z` trailers; and
- central pathname expansion.

## Platform notes

Windows, Ubuntu, and macOS are the tested platforms. BSD behavior is **best effort**.

Integral aliases `C`, `S`, and `I` are 1, 2, and 4 bytes. Integral `L` is 4 bytes on Windows and pointer-width on Unix-like systems. Floating aliases `B`, `H`, `F`, and `D` represent bfloat16, IEEE half, single, and double values. Native 80-bit or 128-bit extended `long double` encodings that cannot be represented directly by .NET are rejected with a controlled diagnostic rather than silently decoded incorrectly.
