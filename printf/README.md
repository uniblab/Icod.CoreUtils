# printf

`printf` implements the GNU Coreutils 9.11 command-line formatter. It supports reusable format strings, positional operands, dynamic width and precision, C-style escapes, `%b`, shell-quoted `%q`, integer, character, string, and floating conversions.

The implementation is managed and platform-neutral. Windows, Linux, and macOS are the required validation platforms; BSD-family behavior is best effort and should be identical except where host locale data differs.
