# fmt source

- `Command.cs` owns option parsing, diagnostics, usage/help/version output, stream adaptation, and command exit status.
- `FmtOptions.cs` contains the validated invocation model.
- `FmtPrefix.cs` normalizes the required prefix, leading indentation, and byte lengths.
- `FmtInputLine.cs` analyzes byte-preserving logical lines, indentation, prefix eligibility, tabs, words, and source spacing.
- `FmtWord.cs` retains exact word bytes and paragraph-breaking metadata.
- `FmtSpacing.cs` writes normalized indentation and reintroduces tabs only where GNU permits an equivalent tab stop.
- `ParagraphFormatter.cs` performs GNU-style dynamic-programming line-break optimization and generated output.
- `FmtProcessor.cs` owns operand iteration and default, split, crown, and tagged paragraph recognition.
