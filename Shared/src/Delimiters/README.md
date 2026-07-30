# Delimiter and separator infrastructure

This directory contains Completion Gate C3 byte delimiter and separator primitives.

- `ByteDelimiter.cs` represents a required nonempty byte sequence used for matching.
- `ByteSeparator.cs` represents an arbitrary byte sequence that may be empty.
- `SeparatorCycle.cs` and `SeparatorCycleCursor.cs` represent deterministic repeating output-separator lists, including empty elements required by GNU `paste`.
- `ByteSequenceMatcher.cs` incrementally matches a multibyte delimiter across arbitrary input-buffer boundaries and supports overlapping patterns.

The distinct delimiter and separator types are intentional. A field delimiter must contain bytes so it can be found; an output or `paste` separator may legitimately contain no bytes.
