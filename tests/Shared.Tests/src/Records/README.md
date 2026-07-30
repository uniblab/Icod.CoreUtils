# Byte-record tests

- `SegmentedRecordTests.cs` covers line-feed and NUL records, empty and unterminated records, exact buffer boundaries, bounded large-record segmentation, cancellation, construction, disposal, and caller-owned input streams.
- `RecordWriterAndCompatibilityTests.cs` covers explicit materialized-record metadata, explicit writer termination, independent content/separator writes, caller-owned output streams, and the preserved behavior of `Icod.CoreUtils.Shared.IO.DelimitedByteRecordReader`.
- `RecordFailureTests.cs` covers read and write failure propagation plus cancellation without partial output.

The compatibility tests characterize the pre-C3 public API: returned materialized records continue to include a separator when one was present and preserve a final unterminated record exactly.
