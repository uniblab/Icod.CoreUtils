# Unexpand tests

- `AssemblyInfo.cs` disables intra-project test parallelism so locale-environment fixtures remain deterministic.
- `CommandTests.cs` covers initial and all-line modes, `--first-only` precedence, modern and obsolete tab options, finite-list exhaustion, repeated and recurring stops, existing tabs, GNU pending-blank behavior, locale blanks, help, version, diagnostics, cancellation, and the synchronous wrapper.
- `BinaryAndOperandTests.cs` covers BOM, malformed-byte, NUL, and final-termination fidelity; backspace repositioning; logical-line and pending-run continuation across operands; missing-file continuation; file-only contexts; caller-owned streams; and controlled read and write failures.

Binary assertions use `MemoryStream` so pending blanks and all untouched input are verified as exact bytes.
