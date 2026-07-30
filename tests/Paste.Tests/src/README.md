# paste tests

- `CommandTests.cs` covers parallel and serial modes, uneven inputs, delimiter cycles, empty delimiter slots, NUL records, and control paths.
- `BinaryAndOperandTests.cs` covers repeated standard input, empty and unterminated input, arbitrary bytes, multibyte delimiters, large records, operand failures, cancellation, ownership, and controlled read/write failures.
- `AssemblyInfo.cs` disables parallel execution for deterministic process-wide tests.
