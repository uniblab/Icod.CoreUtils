# fmt source

- `Command.cs` owns option parsing, diagnostics, usage/help/version output, stream adaptation, and command exit status.
- `FmtOptions.cs` contains the validated invocation model.
- `FmtPrefix.cs` normalizes the required prefix, leading indentation, and byte lengths.
- `FmtInputLine.cs` analyzes byte-preserving logical lines, indentation, prefix eligibility, tabs, words, and source spacing.
- `FmtWord.cs` retains exact word bytes and paragraph-breaking metadata.
- `FmtSpacing.cs` writes normalized indentation and reintroduces tabs only where GNU permits an equivalent tab stop.
- `ParagraphFormatter.cs` performs GNU-style dynamic-programming line-break optimization and generated output. It documents the inclusive maximum-width rule and the fact that every eligible paragraph, including a split-only single-line paragraph, uses the same cost model.
- `FmtProcessor.cs` owns operand iteration and default, split, crown, and tagged paragraph recognition. In split-only mode it deliberately stops paragraph collection at the current input line, then still submits that one-line paragraph to `ParagraphFormatter`.
