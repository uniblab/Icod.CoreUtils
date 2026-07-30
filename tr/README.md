# `tr`

`Icod.CoreUtils.Tr` is the GNU Coreutils 9.11-compatible byte transformation command for Batch 23.

The command is intentionally byte-oriented. It translates, deletes, and squeezes every possible input byte without line decoding, including NUL, newline, carriage return, and other delimiter bytes. The implementation supports ranges, Shared low-level byte escapes, repeated-byte constructs, POSIX character classes, GNU equivalence classes, complementing, target padding, and `--truncate-set1`.

The project references only `Icod.CoreUtils.Shared`. Shared owns the reusable command context, option parser, diagnostics, byte-stream adapters, and low-level `tr` escape parser. The complete set-expression grammar and transformation policy remain command-local because they are specific to `tr`.
