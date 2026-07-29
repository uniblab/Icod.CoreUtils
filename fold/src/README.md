# fold source

- `Command.cs` owns parsing, diagnostics, usage/help/version output, stream adaptation, and exit statuses.
- `FoldOptions.cs` and `FoldCountingMode.cs` model validated invocation state.
- `FoldBuffer.cs` retains exact bytes for the current movable segment.
- `FoldProcessor.cs` implements display-column, character, and byte counting; control-character movement; blank-aware breaks; and bounded zero-width buffering.

Unlike `expand` and `unexpand`, each operand starts with a fresh folding state.
