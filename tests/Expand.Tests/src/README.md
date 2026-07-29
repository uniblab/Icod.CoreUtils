# Expand tests

- `AssemblyInfo.cs` disables intra-project test parallelism so locale-environment fixtures remain deterministic.
- `CommandTests.cs` covers default, modern, obsolete, explicit, repeated, `/N`, and `+N` tab stops; initial-only behavior; display widths; backspace; help; version; diagnostics; cancellation; and the synchronous wrapper.
- `BinaryAndOperandTests.cs` covers BOM, malformed-byte, NUL, and final-termination fidelity; logical-line continuation across operands; missing-file continuation; file-only contexts; caller-owned streams; and controlled read and write failures.

Binary assertions use `MemoryStream` so untouched input is compared byte for byte rather than after text decoding.
