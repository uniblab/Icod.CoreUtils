# Positional range tests

- `RangeListParserTests.cs` covers every GNU positional range form, comma and blank separators, complement mode, bare-hyphen and zero-based profiles, normalization, overflow, malformed tokens, deterministic source offsets, and open-range restrictions.
- `RangeSetTests.cs` covers overlap-only merging, preserved adjacent boundaries, binary membership, range starts, bounded and unbounded complements, open-ended selections, cursor traversal, reset behavior, and model validation.

The adjacent-boundary assertions deliberately protect `cut --output-delimiter` semantics from a superficially attractive but incompatible adjacency merge.
