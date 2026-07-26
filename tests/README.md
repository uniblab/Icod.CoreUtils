# Automated tests

This directory is intentionally distinct from `/test`, which implements the Unix `test(1)` command.

- `Shared.Tests` verifies the Batch 0 Shared infrastructure.
- `ProcessTestHost` supplies deterministic child-process behavior for process-runner tests.

## Test matrix

- Command-line parsing: short clusters, aliases, attached and separate values, optional values, `--`, ordering modes, unique and ambiguous long abbreviations, duplicate options, errors, and legacy rewrites.
- Streaming I/O: LF, CRLF, NUL records, empty and unterminated records, records larger than the buffer, short byte reads, bounded copies, skipping, ranges, and cancellation.
- Temporary spooling: rewind behavior and cleanup.
- Numeric operands: GNU count suffixes, signs, invalid suffixes, overflow rejection/clamping, floating durations, exponents, and culture independence.
- Processes: exact argument preservation, stdin/stdout/stderr forwarding, capture, exit codes, concurrent stream drainage, and cancellation.
- Platform capabilities: controlled unsupported results, hard links, symbolic-link inspection, and Unix mode access where available.
- Compatibility: the existing `SharedUtils` public contract.

Run the Batch 0 suite with:

```text
dotnet test tests/Shared.Tests/Icod.CoreUtils.Shared.Tests.csproj -c Debug
```

Run the complete Batch 0 build sequence with `build.cmd` on Windows or `./build.sh` on Unix-like systems.
