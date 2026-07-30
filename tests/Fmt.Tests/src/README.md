# fmt tests

- `CommandTests.cs` covers default refill, optimized widths, split-only mode, spacing, obsolete syntax, diagnostics, cancellation, and the synchronous wrapper. Its split-only regression deliberately verifies that input lines remain separate paragraphs while each line still uses GNU's non-greedy cost optimizer and inclusive maximum-width rule.
- `ParagraphModeTests.cs` covers crown, tagged, prefix, prefix-only, goal-width, and sentence-spacing behavior.
- `BinaryAndOperandTests.cs` covers BOM and malformed-byte retention, unterminated copied lines, files and repeated standard input, missing operands, stream ownership, and controlled read and output failures.
- `AssemblyInfo.cs` disables test parallelization because locale environment variables are process-wide.
