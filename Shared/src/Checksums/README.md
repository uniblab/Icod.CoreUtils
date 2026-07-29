# Checksums

The `Icod.CoreUtils.Shared.Checksums` namespace contains reusable checksum and digest infrastructure shared by checksum-oriented command projects.

## Responsibilities

- Stream input through incremental checksum accumulators without loading entire files into memory.
- Normalize algorithm selection, digest lengths, hexadecimal and Base64 output, and manifest parsing.
- Implement the common command behavior used by `cksum`, `sum`, and the GNU digest utilities.
- Return deterministic diagnostics and exit statuses through the shared command abstractions.

## Design notes

Algorithm-specific accumulators remain internal. Public command facades accept injected streams through `CommandContext`, honor cancellation, and do not take ownership of caller-supplied streams. Cryptographic algorithms use the BCL where it provides the required implementation; managed fallbacks cover algorithms or output forms that are not directly available.
