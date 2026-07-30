# `shuf` implementation structure

The command project depends only on `Icod.CoreUtils.Shared`.

- `Command.cs` owns GNU command-line parsing, diagnostics, help, version output, and exit-status policy.
- `ShufOptions.cs` contains the validated command-local option model.
- `ShufEngine.cs` owns execution policy for file, echo, range, repeat, and output modes.
- `RandomByteSource.cs` provides cryptographic or deterministic file-backed random bytes and variable-width rejection sampling for exact bounded selection.
- `SpoolRecordStore.cs` uses Shared record segmentation and temporary-spool ownership to preserve arbitrary input bytes, maintain a fixed-width external index, and perform partial Fisher-Yates selection with bounded memory.

Generic command context, option parsing, diagnostics, byte streams, record framing, and temporary-file lifecycle remain in `Icod.CoreUtils.Shared`. No individual command project is referenced.

Finite nonrepeat operations complete randomized selection before an output file is created or truncated. Repeat mode streams selections directly because its output may be intentionally unbounded.
