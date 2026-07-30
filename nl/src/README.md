# nl source

- `Command.cs` owns option parsing, style compilation, numeric validation, diagnostics, usage/help/version output, and command exit status.
- `NlOptions.cs` contains the validated invocation model and section-style selection.
- `NlSection.cs` identifies logical-page sections.
- `NlSectionDelimiter.cs` parses and recognizes default, multibyte, disabled, and extended delimiters.
- `NlNumberingStyleKind.cs` and `NlNumberingStyle.cs` model all, nonempty, none, and GNU BRE pattern styles.
- `NlNumberFormat.cs` models left, right, and right-zero number fields.
- `NlProcessor.cs` owns persistent document state, operand iteration, numbering decisions, overflow, and exact-byte output.
