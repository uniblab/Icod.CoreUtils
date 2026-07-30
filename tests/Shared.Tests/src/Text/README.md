# Shared text tests

- `TextUnitReaderTests.cs` verifies byte iteration, incremental UTF-8 scalar decoding, exact source-byte retention, malformed-input policies, byte offsets, cancellation, and stream ownership.
- `TextLocaleAndWidthTests.cs` verifies C/POSIX and deterministic Unicode blank classification together with zero-, one-, two-, and indeterminate-column widths.
- `TextLocaleEnvironmentTests.cs` verifies `LC_ALL`, `LC_CTYPE`, and `LANG` precedence and deterministic C/POSIX versus UTF-8 profile selection.
- `TabStopParserTests.cs` verifies GNU tab-list grammar, repeated specifications, `/N`, `+N`, legacy parser edge cases, structured diagnostics, and overflow handling.
- `TabStopAndColumnTests.cs` verifies recurring and finite tab-stop lookup, maximum configured distances, checked columns, backspace, carriage return, and cursor recalculation.
- `TextLineReaderTests.cs` verifies terminated, empty, unterminated, Unicode, malformed-byte, cancellation, and stream-ownership behavior for logical lines.
