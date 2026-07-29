# Icod.CoreUtils.Shared.Text

This namespace contains the shared Pre-16 Gate C2 text-unit and display-column foundation for `expand`, `unexpand`, and `fold`.

## Design rules

- Decode input only to make classification and width decisions; preserve the exact source bytes for reproduction.
- Treat a UTF-8 byte-order mark as ordinary input rather than metadata.
- Make malformed-input handling explicit: preserve each byte, replace while retaining the source byte, or throw at a stable byte offset.
- Keep locale blank classification and display-width calculation independently injectable.
- Resolve the process profile through `LC_ALL`, `LC_CTYPE`, then `LANG`, treating only `C` and `POSIX` as raw-byte locales.
- Use checked `ulong` display columns and recurring tab-stop arithmetic.
- Expose the maximum configured tab-stop distance so consumers can bound pending storage.
- Share mechanisms, not command semantics. Command projects retain ownership of option precedence, pending-blank buffers, fold buffers, file-boundary behavior, and output diagnostics.

## Portability profile

The initial implementation supports exact raw-byte iteration for the POSIX C locale and exact byte-preserving UTF-8 scalar iteration. Its deterministic Unicode blank profile recognizes horizontal breakable space separators and excludes U+00A0, U+2007, and U+202F; callers may inject another locale policy. It does not claim transparent compatibility with arbitrary stateful legacy encodings. The managed Unicode display-width provider is deterministic across operating systems, uses Unicode 16.0.0 East Asian Width data, assigns ambiguous-width scalars one column, and measures Unicode scalars rather than grapheme clusters.

## Tab-stop grammar

`TabStopParser` accepts values separated by commas, spaces, or horizontal tabs, and combines repeated specification strings in encounter order. Empty specifications, redundant separators, prefix-only specifications, and zero-valued prefixed intervals retain GNU's default-stop behavior.

- One unprefixed value means a globally aligned recurring interval.
- Two or more unprefixed values are explicit stops.
- A final `/N` continues at global multiples of `N`.
- A final `+N` continues at offsets of `N` from the final explicit stop, or from column zero when no explicit stop exists.
- An explicit list without continuation is exhausted after its final stop.

The parser returns structured errors rather than command-formatted diagnostics.
