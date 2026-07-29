# Input and output

The `Icod.CoreUtils.Shared.IO` namespace contains reusable streaming, record, pathname-expansion, and temporary-spooling primitives.

## Responsibilities

- Adapt text readers and writers to byte-oriented command implementations.
- Read and write delimited text or byte records incrementally.
- Open file operands while preserving the conventional `-` standard-input marker.
- Expand `*`, `?`, and recursive `**` pathname patterns under explicit policies.
- Copy, compare, skip, and limit streams with bounded memory use.
- Spool data when an operation requires replay without assuming seekable input.

## Design notes

APIs are TAP-oriented where I/O is naturally asynchronous, honor cancellation, and do not take ownership of injected standard streams unless an API explicitly says otherwise.
