# nl tests

- `CommandTests.cs` covers defaults, signed arithmetic, field formats, blank grouping, GNU BRE styles, diagnostics, cancellation, and the synchronous wrapper.
- `SectionAndStylesTests.cs` covers default, disabled, multibyte, and extended delimiters; section resets; no-renumber; and independent section styles.
- `BinaryAndOperandTests.cs` covers exact bytes, GNU normalization of missing final line feeds, multiple files as one document, missing operands, repeated standard input, deferred overflow, stream ownership, and controlled read and output failures.
- `OptionEdgeTests.cs` covers GNU numeric grammar, last-option precedence, simple-style suffixes, empty BRE matching, blank grouping across sections, and locale-sensitive delimiter character counting.
- `AssemblyInfo.cs` disables test parallelization because locale environment variables are process-wide.
