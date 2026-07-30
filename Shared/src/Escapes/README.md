# Escape parsing infrastructure

This directory contains Completion Gate C3 command-neutral scanning mechanics and explicit GNU grammar profiles.

- `EscapeSequenceScanner.cs` and `EscapeSequence.cs` identify a backslash and its following managed source position without choosing command semantics.
- `EscapeDiagnostic.cs`, `EscapeDiagnosticCode.cs`, and `EscapeDiagnosticSeverity.cs` provide stable warnings and errors with source offsets.
- `PasteDelimiterParser.cs` parses GNU `paste --delimiters` syntax into a `SeparatorCycle`. In this grammar `\0` is an empty separator element, unknown escapes lose the backslash, and a trailing backslash is an error.
- `TrByteEscapeParser.cs` parses the low-level named and one-to-three-digit octal byte escapes needed by future GNU `tr` set parsing. It retains whether each byte was escaped and reproduces GNU's deterministic warning behavior for trailing backslashes and overflowing three-digit octal escapes.
- `EscapedByte.cs`, `PasteDelimiterParseResult.cs`, and `TrByteEscapeParseResult.cs` expose immutable results.

There is deliberately no universal escape decoder. GNU formatting, `paste`, and `tr` assign different meanings to identical source text. `Icod.CoreUtils.Shared.Formatting.GnuEscapeDecoder` keeps its established formatting grammar while delegating only neutral backslash scanning to this directory.

Full `tr` character classes, equivalence classes, repetitions, and set ranges remain command-specific grammar because they are not positional range lists.

The managed parsers accept an optional stateless `Encoding` for ordinary Unicode scalars and default to deterministic UTF-8. They do not claim exact support for stateful legacy command-line encodings, because .NET exposes command arguments only after host decoding.
