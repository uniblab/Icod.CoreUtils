# cut tests

- `CommandTests.cs` covers positional range grammar, complement, adjacent output boundaries, multibyte characters, `--no-partial`, NUL records, and control paths.
- `FieldTests.cs` covers explicit, empty, whitespace, TAB-versus-space output defaults, trimmed long-option syntax, multibyte, shorthand, suppression, and record-separator field delimiters.
- `BinaryAndOperandTests.cs` covers malformed bytes, files, repeated standard input, cancellation, stream ownership, and controlled read/write failures.
- `AssemblyInfo.cs` disables parallel execution because locale environment state is process-wide.
