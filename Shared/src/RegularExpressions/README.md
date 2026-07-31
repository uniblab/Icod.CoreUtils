# Shared GNU Regular Expressions

`Icod.CoreUtils.Shared.RegularExpressions` is the managed, command-neutral GNU regular-expression foundation introduced by Completion Gate C1 and completed for GNU Basic and Extended syntax by Completion Gate R1. It is not a pattern translator over `System.Text.RegularExpressions`. The same parser, syntax tree, and leftmost-longest matcher serve BRE, ERE, and the GNU Emacs profile used by `ptx`.

The directory is provisionally classified as a cross-suite `Icod.CommandFramework` candidate. During incubation it remains in the current Shared project and is consumed through project references. Grep-, Sed-, Ed-, Expr-, and Csplit-specific pattern sourcing, empty-pattern reuse, match iteration, replacement grammar, binary policy, diagnostics presentation, and command state remain in their owning suite or command engines.

## Authoritative behavior

The implementation is pinned to:

- GNU Coreutils 9.11 and its associated Gnulib regular-expression implementation for the established `expr` and `ptx` consumers;
- GNU grep 3.12 and its associated Gnulib behavior for the first full BRE/ERE search consumer;
- POSIX.1-2024 Issue 8 for Basic and Extended regular expressions, bracket expressions, captures, back-references, locale concepts, and leftmost-longest selection.

The immutable upstream identities and source links belong in `Icod.CoreUtils-Upstream-Version-Ledger.md`.

## Syntax profiles

`RegularExpressionOptions.Syntax` is explicit and defaults to `GnuRegularExpressionSyntax.Basic`, preserving source compatibility for existing callers.

| Provider/profile | Operator spelling | Compatibility policy |
|---|---|---|
| `GnuBasicRegularExpressionProvider` | Escaped `\(`, `\)`, `\|`, `\+`, `\?`, and `\{m,n\}` | Strict GNU/POSIX Basic by default; `GnuExprCompatibility` enables the established Coreutils `expr` duplicate/reverse-range behavior |
| `GnuExtendedRegularExpressionProvider` | Unescaped `(`, `)`, `|`, `+`, `?`, and `{m,n}` | Strict GNU/POSIX Extended by default; `GnuExtendedCompatibility` enables GNU search-consumer leading/duplicate-operator behavior while retaining invalid-range diagnostics |
| `GnuEmacsRegularExpressionProvider` | Unescaped `+` and `?`; escaped grouping, alternation, and intervals | Uses the permissive Gnulib Emacs profile required by Coreutils `ptx` |

ERE is parsed directly. Escaped ERE metacharacters are literals, unmatched opening parentheses are diagnosed, and an unmatched closing parenthesis is ordinary outside a subexpression in the GNU profile. GNU-compatible malformed ERE brace text such as `a{` or `a{word}` remains literal; a syntactically recognized interval with invalid bounds still produces `InvalidInterval`.

## Public API

- `IRegularExpressionProvider` compiles patterns through synchronous and TAP-shaped asynchronous methods.
- `ICompiledRegularExpression` performs reusable string and byte-preserving searches.
- `RegularExpressionCompileResult`, `RegularExpressionMatchResult`, and `RegularExpressionByteMatchResult` distinguish successful no-match results from controlled errors.
- `RegularExpressionDiagnostic` exposes a stable code, message, and UTF-16 pattern index where applicable.
- `IRegularExpressionCharacterClassProvider` isolates classification, scalar comparison, collation, and case-equivalence policy.
- `RegularExpressionOptions` controls syntax, case, line sensitivity, compatibility policy, nesting limits, and match-state limits.
- `RegularExpressionMatchOptions` addresses .NET strings by UTF-16 index.
- `RegularExpressionInputOptions` selects byte-valued or UTF-8 decoding and an explicit malformed-input policy.
- `RegularExpressionByteMatchOptions` addresses authoritative byte input by source-byte offset.

The CPU-bound asynchronous members do not call `Task.Run`. They preserve a consistent TAP-facing command API, honor cancellation throughout parsing and matching, and normally return an already-completed `ValueTask`.

## Implemented grammar and matching behavior

| Area | Supported behavior |
|---|---|
| Ordinary atoms | Unicode scalars and literal escaped characters |
| Any-character operator | Basic and Extended exclude NUL and optionally newline; GNU Emacs includes NUL and excludes newline |
| Bracket expressions | Matching and nonmatching lists, literal first `]`, literal first/last `-`, ranges, the 12 standard POSIX named classes, single-scalar collating symbols, and single-scalar equivalence classes; the Emacs profile treats backslash as an ordinary list character |
| Repetition | BRE `*`, `\+`, `\?`, `\{m,n\}`; ERE `*`, `+`, `?`, `{m,n}`; Emacs hybrid forms; GNU interval limit 32,767; profile-controlled adjacent operators |
| Grouping and alternation | BRE/Emacs escaped forms and ERE unescaped forms, with opening-order capture numbering |
| Back-references | GNU `\1` through `\9`; a following digit remains ordinary |
| Anchors and assertions | Contextual `^` and `$`, GNU whole-input `\`` and `\'`, word boundaries, word starts/ends, and GNU word/space classes |
| Selection | Leftmost-longest whole match; equal-endpoint paths retain deterministic GNU/Gnulib greedy order |
| Repeated captures | Last successful register values retained by repeated and nested captures |
| Locale behavior | Literal, range, class, equivalence, case, and back-reference comparisons use the injected provider |
| Operational controls | Cancellation during compile/decode/match, bounded syntax nesting, and a controlled match-state limit diagnostic |

## String and authoritative-byte contract

The two matching surfaces are intentionally distinct.

### .NET string input

String matching decodes input into Unicode scalars for evaluation. Public match and capture indices and lengths remain UTF-16 values so callers can slice the original .NET string. An unpaired UTF-16 code unit is evaluated as U+FFFD, while returned `Value` strings retain the original source code units.

### Byte-preserving input

Byte matching always retains the original `ReadOnlyMemory<byte>` as authoritative data. Public match and capture positions are zero-based source-byte offsets, and `Value` properties are exact slices of the original bytes.

`RegularExpressionInputOptions.DecodingMode` has two modes:

- `Bytes`: every source byte is one matching unit. This is the natural pairing for a C/POSIX byte-locale provider.
- `Utf8`: valid UTF-8 becomes Unicode-scalar matching units while every unit retains its exact starting and ending byte boundary.

A UTF-8 `StartByteOffset` must fall on one of those boundaries; splitting a multibyte scalar returns `InvalidStartByteOffset`.

Malformed UTF-8 follows `InvalidEncodingPolicy` exactly:

- `PreserveBytes`: each malformed source byte is one opaque matching unit. It can be consumed by dot or a suitable negated expression, but it is not silently converted into U+FFFD. Returned bytes remain exact.
- `Replace`: each malformed source byte is evaluated as U+FFFD while the public match still returns that original byte.
- `Throw`: matching throws `DecoderFallbackException` at the first malformed byte, consistent with the Shared text-unit contract.

The regular-expression engine never chooses a locale from the input. Callers explicitly pair byte/UTF-8 decoding with an injected `IRegularExpressionCharacterClassProvider`. `PosixCLocaleRegularExpressionCharacterClassProvider` gives deterministic ASCII classification and ordinal collation; `UnicodeRegularExpressionCharacterClassProvider` gives the selected .NET culture profile.

### Replacement output

R1 does not define a cross-command replacement language. Sed, Ed, and other consumers own replacement parsing, occurrence selection, empty-match iteration, and output-encoding policy. A byte-preserving consumer should copy unmatched source slices verbatim, interpret captures through the exact byte slices returned here, and encode replacement literals according to its own explicit policy. This keeps regex selection common without moving command-specific substitution state into Shared.

## Leftmost-longest and capture policy

The engine does not stop after the first viable whole match. It enumerates viable states for each possible starting unit, selects the earliest start, and selects the greatest ending position at that start. When multiple paths reach the same greatest endpoint, ordered alternation and greedy repetition traversal preserve deterministic register values compatible with the Gnulib `re_match` behavior used by existing consumers.

The matcher operates over one unit sequence regardless of whether those units came from UTF-16 scalars, UTF-8 scalars, byte-valued units, or preserved malformed bytes. Source-coordinate conversion occurs only when constructing the public match result.

## Locale and classification providers

Two fully managed providers are included. Both expose only the 12 standard POSIX bracket-class names (`alnum`, `alpha`, `blank`, `cntrl`, `digit`, `graph`, `lower`, `print`, `punct`, `space`, `upper`, and `xdigit`); GNU `\w` uses the separate word-character method and does not make `[[:word:]]` a valid POSIX class.

- `UnicodeRegularExpressionCharacterClassProvider` uses `Rune` categories and a supplied `CultureInfo.CompareInfo`.
- `PosixCLocaleRegularExpressionCharacterClassProvider` implements deterministic ASCII classification and ordinal collation.

The BCL does not expose a locale's complete collating-element inventory. Single-scalar collating symbols and equivalence classes are supported. Multi-scalar collating elements produce `UnsupportedCollatingElement` rather than silently changing semantics.

## Differences from `System.Text.RegularExpressions`

1. BRE and ERE have profile-specific operator spelling rather than .NET syntax.
2. POSIX/GNU requires leftmost-longest selection and observable capture-register behavior.
3. GNU invalid-duplicate, interval, empty-range, word, and whole-input behavior is profile-driven.
4. Classification and collation are delegated to an injectable locale policy.
5. Matching coordinates may be UTF-16 indices or exact source-byte offsets.
6. Malformed input policy is explicit and can preserve source bytes.
7. Compile and match failures use stable diagnostics rather than `RegexParseException` or an exposed internal resource exception.
8. Perl/.NET-only constructs such as lookaround, lazy quantifiers, inline options, named groups, and replacement syntax are not interpreted as GNU BRE/ERE features.

## Resource, cancellation, and platform policy

`MaximumNestingDepth` bounds parenthesized syntax and nested adjacent repetition. `MaximumMatchStates` bounds one search; exceeding it returns `MatchResourceLimitExceeded`. Cancellation uses the normal `OperationCanceledException` contract during parsing, decoding, state expansion, and back-reference comparison.

The implementation is fully managed and follows the same path on `windows-latest`, `ubuntu-latest`, and `macos-latest`. Native regex libraries are not invoked, and production consumers must surface controlled diagnostics rather than silently substituting another engine.
