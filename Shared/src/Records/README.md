# Byte-record infrastructure

This directory contains the Completion Gate C3 byte-record model.

- `RecordSeparator.cs` represents the one-byte separator used by line-delimited and NUL-delimited GNU record modes.
- `ByteRecord.cs` represents independently owned record content plus explicit termination metadata.
- `ByteRecordReader.cs` materializes that model over the bounded segmented engine for callers that need a complete record.
- `ByteRecordSegment.cs` represents one bounded, independently owned segment of a record.
- `DelimitedByteRecordSegmentReader.cs` reads arbitrarily large records without requiring one whole-record input buffer. The caller owns the source stream.
- `DelimitedByteRecordWriter.cs` writes content and separators separately so each command can preserve, omit, replace, or synthesize record terminators according to its own GNU contract.

The segmented reader excludes the separator from segment data and reports termination explicitly. Empty records, consecutive separators, NUL records, embedded carriage returns, and final unterminated records therefore remain distinguishable without text decoding or newline normalization.

`ByteRecordReader` is the preferred materializing API because it keeps content and termination metadata separate. `Icod.CoreUtils.Shared.IO.DelimitedByteRecordReader` remains the compatibility API; it now delegates to `ByteRecordReader` and continues returning arrays that include a present separator.
