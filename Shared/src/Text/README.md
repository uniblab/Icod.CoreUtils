# Icod.CoreUtils.Shared.Text

This namespace now contains only the Coreutils-specific GNU tab-stop grammar retained after the general text substrate moved to the standalone `Icod.CommandFramework` package.

General byte-preserving text units, logical lines, UTF-8 decoding policy, locale classification, display-width calculation, display-column tracking, and tab-stop models are owned by `Icod.CommandFramework.Text`.

## Coreutils-owned tab-stop grammar

`TabStopParser` parses the GNU tab-list syntax used by commands such as `expand` and `unexpand`. It accepts values separated by commas, spaces, or horizontal tabs and combines repeated specification strings in encounter order.

- One unprefixed value means a globally aligned recurring interval.
- Two or more unprefixed values are explicit stops.
- A final `/N` continues at global multiples of `N`.
- A final `+N` continues at offsets of `N` from the final explicit stop, or from column zero when no explicit stop exists.
- An explicit list without continuation is exhausted after its final stop.
- Empty specifications, redundant separators, prefix-only specifications, and zero-valued prefixed intervals retain the intended GNU compatibility behavior.

The parser returns `TabStopParseResult` and structured `TabStopParseError` values. Successful results contain the reusable `Icod.CommandFramework.Text.TabStopSet` model.

## Ownership boundary

The following types remain in `Icod.CoreUtils.Shared.Text` because they encode GNU/Coreutils parsing policy:

- `TabStopParser`
- `TabStopParseResult`
- `TabStopParseError`
- `TabStopParseErrorCode`

The reusable mechanisms consumed by those types belong to `Icod.CommandFramework.Text`.
