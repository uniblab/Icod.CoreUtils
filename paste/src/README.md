# paste source

- `Command.cs` provides synchronous and asynchronous entry points, option validation, and dedicated usage/help/version writers.
- `PasteOptions.cs` stores the record separator, generated terminator, delimiter cycle, mode, and operands.
- `PasteInput.cs` combines one opened input source with the shared bounded record-segment reader and translates input read failures without taking ownership of injected streams.
- `PasteInputException.cs` identifies operand read failures separately from output failures.
- `PasteProcessor.cs` implements ordered parallel rows and serial per-operand joining without `Task.Run` or output-reordering parallelism. GNU-compatible read errors mark the final status as failed but do not prevent remaining columns or operands from being processed.

The implementation consumes Completion Gate C3 delimiter cycles and segmented byte records. It intentionally shares a single `PasteInput` for repeated standard-input operands so buffering cannot hide records from a later `-` column.
