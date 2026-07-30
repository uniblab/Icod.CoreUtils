# cut source

- `Command.cs` provides the public synchronous wrapper, cancellation-aware TAP entry points, GNU-style option validation, and dedicated usage/help/version writers.
- `CutOptions.cs` stores validated immutable execution settings.
- `CutMode.cs` identifies byte, character, or field positions.
- `CutProcessor.cs` performs bounded byte selection and byte-preserving character selection.
- `CutFieldProcessor.cs` performs field extraction, whitespace-run parsing, undelimited-record handling, and the record-separator-as-field-delimiter edge case.
- `CutInputStream.cs` preserves caller ownership while translating input read failures into operand-specific exceptions, so output failures retain the command-level write diagnostic.

The command consumes Completion Gate C2 text units and Completion Gate C3 records and ranges. Command-specific field policy remains here rather than being pushed into Shared.

The `-w` and `-F` paths intentionally retain different default output delimiters: TAB for plain whitespace-delimited mode and space for the `-F` shorthand. The short `-w` spelling takes no attached argument; only `--whitespace-delimited=trimmed` selects trimming.
