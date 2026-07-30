# Positional range infrastructure

This directory contains the Completion Gate C3 positional range-list model shared by byte, character, field, and general index consumers.

- `InclusiveRange.cs` represents closed and open-ended unsigned ranges.
- `RangeSet.cs` sorts and normalizes ranges, performs membership checks, and computes complements.
- `RangeCursor.cs` provides efficient monotonically increasing selection and range-start checks.
- `RangeMatch.cs` returns membership and boundary state together.
- `RangeListParser.cs` parses GNU-style `N`, `N-`, `N-M`, and `-M` forms separated by commas, ASCII spaces, or horizontal tabs.
- `RangeListParserOptions.cs` selects the numeric domain, optional bare-hyphen behavior, open-range grammar, and complement mode.
- `RangeParseResult.cs`, `RangeParseError.cs`, and `RangeParseErrorCode.cs` provide stable command-neutral diagnostics.

## Deliberate normalization rule

Overlapping ranges are merged, but adjacent ranges are not. Thus `1-2,2-4` becomes `1-4`, while `1-2,3-4` retains two ranges. Ordinary membership is identical in the adjacent case, but the boundary remains observable for GNU operations such as `cut --output-delimiter`, which may emit a separator at the start of each requested range.

The command-line grammar deliberately treats only ASCII space and horizontal tab as blank separators, keeping results deterministic across managed hosts. The parser does not produce command names or final user-facing wording. Commands map the structured error and source position to their own GNU diagnostic contract.
