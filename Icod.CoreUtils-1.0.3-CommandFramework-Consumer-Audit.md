# Icod.CoreUtils 1.0.3 — Icod.CommandFramework 2.2.1 Consumer Audit

## Purpose

This audit records the `Icod.CoreUtils` review performed while advancing the repository-local Shared dependency from `Icod.CommandFramework 2.1.0` to `2.2.1`.

The review focuses on regex-heavy consumers that could plausibly benefit from the immutable prepared-byte-input API introduced by CommandFramework 2.2.x:

- `ptx`
- `tac`
- `csplit`
- `nl`
- `expr`
- `numfmt`

The ownership rule is unchanged: `Icod.CommandFramework` owns reusable neutral mechanism; `Icod.CoreUtils.Shared` owns demonstrated Coreutils-family reuse; individual commands own command-specific policy and optimization choices.

## `tac`

### Existing shape

Regex separators are searched repeatedly inside the same authoritative byte window while the search start advances.

### Decision

Adopt `RegularExpressionPreparedByteInput`.

Each regex window is now prepared once and reused while enumerating matches. The existing UTF-8 decoding policy, source-byte coordinates, window alignment, and reverse-index behavior remain unchanged.

### Rationale

This is the canonical prepared-input workload: repeated searches over one immutable authoritative byte record.

## `ptx`

### Existing shape

`ptx` historically used a Latin-1 bridge so every source byte mapped to exactly one managed character and therefore one index position. That behavior is semantically important because keyword spans, formatting boundaries, custom sentence separators, references, and output slicing are byte-coordinate operations.

### Decision

Adopt prepared byte input selectively with `TextDecodingMode.Bytes`.

Implemented in this release:

- custom word-expression discovery prepares a byte context once and reuses it while enumerating word matches;
- custom sentence-expression discovery prepares the complete authoritative input once and reuses it while advancing through separators;
- focused tests pin independent matching of UTF-8 continuation bytes and malformed high bytes.

Deliberately retained:

- `PtxPatterns.SkipSomething` keeps its bounded Latin-1 bridge.

### Rationale for the retained bounded path

`SkipSomething` receives an exclusive upper bound from the formatter. Matching a prepared full context could allow the regex engine to inspect bytes beyond that bound, potentially changing leftmost-longest selection or boundary behavior. The current public prepared-input API controls the start byte offset but does not provide an exclusive end bound.

Until a bounded prepared-match surface exists, retaining the bounded bridge is safer than either changing semantics or repeatedly preparing prefixes.

## `csplit`

### Existing shape

The regex search loop reads one candidate line, performs one byte-regex match against that line, then discards the line and advances to the next candidate.

### Decision

Do not introduce prepared input.

### Rationale

The prepared-input API owns a source snapshot and preparation cost. With one match per candidate line there is no preparation reuse to amortize that cost. The existing direct byte-match call is the correct fit.

Revisit only if profiling later identifies repeated matching of the same candidate line or another reusable record shape.

## `nl`

### Existing shape

Pattern numbering styles compile one GNU regular expression and evaluate it once per already-decoded logical line. Input is processed through `TextUnitReader`/`TextLineReader` under the active locale decoding policy, and the pattern decision receives `line.ToDecodedString()`.

### Decision

Keep the existing string-match path.

### Rationale

`nl` is not repeatedly matching one byte record. More importantly, the already-decoded logical line is the semantic input selected by the text pipeline. Introducing prepared authoritative-byte input here would either duplicate work or bypass the established locale/text-unit boundary.

The current abstraction is therefore both semantically appropriate and cheaper than creating a prepared byte snapshot per line.

## `expr`

### Existing shape

The `:` and `match` operations compile a GNU basic regular expression with `RegularExpressionOptions.GnuExprCompatibility` and then perform one anchored string match using `RegularExpressionMatchOptions.RequireMatchAtStart`.

Capture behavior, match length, locale-aware logical-character length, diagnostics, and GNU exit-status policy are handled by the existing evaluator.

### Decision

Keep the existing CommandFramework string-regex path.

### Rationale

Each `expr` regex operation is effectively single-shot. The dedicated GNU Expr compatibility profile and anchored string matcher directly represent the required semantics. Prepared byte input would add conversion/preparation machinery without providing reuse and could interfere with locale-aware string/capture behavior.

## `numfmt`

### Existing shape

`numfmt` uses a source-generated .NET regular expression only to parse its command-specific `--format` `%f` mini-language:

```text
%(flags)(width)(.precision)f
```

The regex is internal parser machinery and is not a user-selectable GNU/POSIX regular expression.

### Decision

Retain `System.Text.RegularExpressions.GeneratedRegex`.

### Rationale

Moving this parser to the GNU regex engine would conflate two unrelated contracts. CommandFramework regular expressions exist to provide GNU/POSIX regex semantics where those semantics are part of command behavior; `numfmt`'s `%f` parser is a fixed internal grammar and is well served by a compile-time generated .NET regex.

## Final disposition

| Consumer | 2.2.1 action | Result |
| --- | --- | --- |
| `tac` | Prepared byte input | Implemented |
| `ptx` word scan | Byte-mode prepared input | Implemented |
| `ptx` sentence scan | Byte-mode prepared input | Implemented |
| `ptx` bounded formatter scan | Keep bounded Latin-1 bridge | Intentional |
| `csplit` | Keep direct byte match | Intentional |
| `nl` | Keep decoded string match | Intentional |
| `expr` | Keep GNU Expr string match | Intentional |
| `numfmt` | Keep source-generated .NET regex | Intentional |

## Framework boundary conclusion

No new public `Icod.CommandFramework` API is required to complete the 1.0.3 consumer work.

The only remaining potentially useful framework enhancement suggested by this audit is an optional future prepared-match API with an exclusive byte end bound. That possibility is motivated by `ptx` formatter scanning, but it is not required for correctness and should not be added solely for this release.
