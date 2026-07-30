# Input and output

The `Icod.CoreUtils.Shared.IO` namespace contains reusable streaming, record, token, pathname-expansion, and temporary-spooling primitives.

## Responsibilities

- Adapt text readers and writers to byte-oriented command implementations.
- Read and write delimited text or byte records incrementally.
- Read byte tokens incrementally using an explicit set of separator bytes.
- Open file operands while preserving the conventional `-` standard-input marker.
- Expand `*`, `?`, and recursive `**` pathname patterns under explicit policies.
- Copy, compare, skip, and limit streams with bounded memory use.
- Spool data when an operation requires replay without assuming seekable input.

## Design notes

APIs are TAP-oriented where I/O is naturally asynchronous, honor cancellation, and do not take ownership of injected standard streams unless an API explicitly says otherwise. `ByteTokenReader` is encoding-agnostic, returns independently owned nonempty tokens, and deliberately has no command-specific pair or graph semantics.

## Record API boundaries

`DelimitedRecordReader` and `DelimitedRecordWriter` are decoded-text conveniences. They operate after a `TextReader` or `TextWriter` has chosen an encoding and may omit, synthesize, or normalize delimiters according to their documented text contract. They cannot recover malformed source bytes or a consumed byte-order mark.

`DelimitedByteRecordReader` is the compatibility whole-record byte API. It continues returning independently owned arrays that include a present separator and preserve a final unterminated record. Its framing now delegates to `Icod.CoreUtils.Shared.Records.ByteRecordReader`, which uses `DelimitedByteRecordSegmentReader` internally.

New byte-sensitive commands should use the `Records` namespace directly. `ByteRecordReader` materializes content plus explicit termination metadata when a complete record is required. The segmented reader excludes separators from segment data, reports termination explicitly, bounds each returned segment, and never normalizes carriage returns, line feeds, NUL bytes, encodings, or malformed input.
