# expand source

- `Command.cs` owns argument parsing, diagnostics, usage/help/version output, stream adaptation, and exit-status translation.
- `ExpandOptions.cs` carries the validated invocation state.
- `ExpandProcessor.cs` performs streaming byte-preserving tab expansion over the shared text-unit and display-column model.

The processor deliberately preserves logical-line state across consecutive operands, matching GNU `expand` when an input file does not end in a newline.
