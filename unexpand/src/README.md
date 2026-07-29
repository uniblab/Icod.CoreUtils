# unexpand source

- `Command.cs` owns argument parsing, option precedence, diagnostics, usage/help/version output, and stream adaptation.
- `UnexpandOptions.cs` carries validated invocation state.
- `PendingBlankBuffer.cs` incrementally retains exact blank bytes only until the next replacement decision is known.
- `UnexpandProcessor.cs` performs the streaming blank-to-tab transformation.

Logical-line state intentionally continues across consecutive operands when an earlier file lacks a newline.
