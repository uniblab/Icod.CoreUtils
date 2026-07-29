# Fold tests

- `AssemblyInfo.cs` disables intra-project test parallelism so locale-environment fixtures remain deterministic.
- `CommandTests.cs` covers default, modern, and obsolete widths; byte, character, and display-column modes; last-option precedence; blank-aware folding; long words; tabs; carriage returns; backspaces; zero-width scalars; help; version; diagnostics; cancellation; and the synchronous wrapper.
- `BinaryAndOperandTests.cs` covers BOM, malformed-byte, NUL, and final-termination fidelity; incremental scalar decoding; over-wide scalars; bounded zero-width buffering; per-operand columns and GNU last-character-width persistence; missing-file continuation; file-only contexts; caller-owned streams; and controlled read and write failures.

Generated fold separators use `Environment.NewLine`; untouched input newlines and all other source bytes remain exact.
