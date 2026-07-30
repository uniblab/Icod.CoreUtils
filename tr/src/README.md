# `tr` source organization

- `Command.cs` owns command-line parsing, diagnostics, control paths, and stream orchestration.
- `TrOptions.cs` contains the validated command option model.
- `TrSetParser.cs` layers ranges, classes, equivalence classes, and repeated-byte constructs over `Icod.CoreUtils.Shared.Escapes.TrByteEscapeParser`.
- `TrSetElement.cs`, `TrSetExpression.cs`, and `TrSetCursor.cs` model and lazily traverse parsed byte arrays without requiring large repeat counts to be materialized.
- `TrCharacterClass.cs` and `TrByteLocale.cs` implement POSIX byte-class membership and locale-sensitive case conversion.
- `TrTransformPlan.cs` validates operation-specific grammar and compiles translation, deletion, and squeeze tables.
- `TrEngine.cs` applies those tables to an unbounded byte stream with bounded pooled buffers.

All code in this directory is command-local `tr` policy. Reusable command, diagnostic, stream, and escape facilities remain in `Icod.CoreUtils.Shared`; no individual utility project is referenced.
