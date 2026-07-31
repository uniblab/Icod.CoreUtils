# Shared GNU Regular Expressions

`Icod.CoreUtils.Shared.RegularExpressions` is the common GNU regular-expression foundation introduced by Completion Gate C1 and extended in Batch 25 with the GNU Emacs profile required by `ptx`. It is intentionally independent of any one command. Batch 16 (`expr`) consumes it first; later batches can extend the same provider boundary for `grep`, `csplit`, and `ed` rather than introducing separate engines.

## Authoritative behavior

The implementation is pinned to:

- GNU Coreutils 9.11 for the first consumer, `expr`;
- the Gnulib regular-expression implementation associated with Coreutils 9.11, especially `RE_SYNTAX_POSIX_BASIC` and GNU operators;
- POSIX.1-2024 Issue 8 for basic regular expressions, bracket expressions, captures, back-references, locale concepts, and leftmost-longest selection.

The exact immutable identities and source links are recorded in `Icod.CoreUtils-Upstream-Version-Ledger.md`.

## Public API

- `IRegularExpressionProvider` compiles patterns through synchronous and TAP-shaped asynchronous methods.
- `ICompiledRegularExpression` performs reusable synchronous and asynchronous searches.
- `RegularExpressionCompileResult` and `RegularExpressionMatchResult` distinguish successful no-match results from controlled errors.
- `RegularExpressionDiagnostic` exposes a stable code, message, and UTF-16 pattern index where applicable.
- `IRegularExpressionCharacterClassProvider` isolates character classification, scalar comparison, collation, and case-equivalence policy.
- `GnuBasicRegularExpressionProvider` supplies GNU/POSIX basic syntax.
- `GnuEmacsRegularExpressionProvider` supplies the GNU Emacs profile used by Coreutils `ptx`.
- `RegularExpressionOptions` controls syntax, case, line-sensitive matching, repetition and empty-range compatibility, nesting limits, and match-state limits; it is an immutable record so a named profile can be refined with a `with` expression.
- `RegularExpressionMatchOptions` controls the UTF-16 start index and anchored-at-start matching.

Example:

```csharp
var provider = new GnuBasicRegularExpressionProvider(
	PosixCLocaleRegularExpressionCharacterClassProvider.Instance
);
var compiled = provider.Compile(
	@"\(ab*\)\1",
	new RegularExpressionOptions { MaximumMatchStates = 1_000_000 }
);
if ( !compiled.IsSuccess ) {
	Console.Error.WriteLine( compiled.Diagnostic!.Message );
	return;
}

var result = await compiled.Expression!.MatchAsync(
	"abbbabbb",
	new RegularExpressionMatchOptions { RequireMatchAtStart = true },
	cancellationToken
).ConfigureAwait( false );
```

The CPU-bound asynchronous members do not call `Task.Run`. They preserve a consistent TAP-facing command API, honor cancellation throughout parsing and matching, and normally return an already-completed `ValueTask`.

## Implemented GNU grammar

The parser and matcher support:

| Area | Supported behavior |
|---|---|
| Ordinary atoms | Unicode scalars and literal escaped characters |
| Any-character operator | Basic syntax excludes NUL and optionally newline; GNU Emacs syntax includes NUL and excludes newline, matching its Gnulib syntax bits |
| Bracket expressions | matching and nonmatching lists, literal first `]`, literal first/last `-`, ranges, the 12 standard POSIX named classes, single-scalar collating symbols, and single-scalar equivalence classes; the Emacs profile treats backslash as an ordinary list character |
| Repetition | basic `*`, `\+`, `\?`; Emacs `*`, `+`, `?`; `\{m\}`, `\{m,\}`, `\{,n\}`, `\{,\}`, and `\{m,n\}`, with the GNU limit of 32,767 and nested adjacent operators where the selected Gnulib profile permits them |
| Grouping | `\(` and `\)` with opening-order numbering |
| Alternation | GNU `\|` |
| Back-references | `\1` through `\9`; a following digit remains an ordinary character |
| Anchors | contextual `^` and `$`, plus GNU whole-input `\`` and `\'` |
| Word assertions | GNU `\<`, `\>`, `\b`, and `\B` |
| GNU classes | `\w`, `\W`, `\s`, and `\S` |
| Selection | leftmost-longest whole match; equal-endpoint paths retain deterministic GNU/Gnulib greedy order |
| Repeated captures | GNU `re_match` register behavior, including the last successful value retained by nested captures |
| Case-insensitive matching | literals, ranges, named classes, equivalence classes, and back-references use the injected provider |
| Line-sensitive matching | line-feed-aware anchors with option-sensitive dot and negated-list behavior; the Emacs profile's dot always excludes line feed |

The default compilation profile follows strict GNU/POSIX basic syntax: `*`, `\+`, and `\?` are ordinary when no repeatable expression precedes them, while an interval opener in that context and disallowed adjacent repetition operators produce `InvalidRepetitionOperator`. `RegularExpressionOptions.GnuExprCompatibility` reproduces Coreutils `expr`, which clears Gnulib's `RE_CONTEXT_INVALID_DUP` and `RE_NO_EMPTY_RANGES` bits; under that profile, otherwise-invalid repetition contexts are accepted and reverse-collating ranges are empty. Malformed interval bodies still produce `InvalidInterval`.


`GnuEmacsRegularExpressionProvider` selects `RE_SYNTAX_EMACS`-compatible operator behavior: `+` and `?` are unescaped operators, `\+` and `\?` are literals, grouping and alternation remain escaped, intervals retain escaped braces, dot excludes line feed but may match NUL, invalid adjacent repetition contexts are accepted, and reverse-collating ranges are empty. This is the profile used by GNU Coreutils `ptx` for `--word-regexp` and `--sentence-regexp`.
## Leftmost-longest and register policy

The engine does not stop after the first viable whole match. It enumerates viable states for each possible starting scalar, selects the earliest start, and selects the greatest ending position at that start. When multiple paths reach the same greatest endpoint, ordered alternation and greedy repetition traversal preserve the deterministic register values produced by the Gnulib `re_match` interface used by Coreutils `expr`. This includes retaining an earlier successful nested capture when a later outer repetition does not participate in that nested group. This is required for GNU behavior such as:

```text
\(ac*\)\(c*d[ac]*\)\1
```

matching all of `acdacaaa` while the first subexpression captures `a`.

Internal matching is scalar-based. Public indices and lengths are UTF-16 values so callers can safely slice normal .NET strings. An invalid UTF-16 input or pattern code unit is treated as U+FFFD for matching; returned `Value` strings retain the original source code units.

## Locale and classification providers

Two BCL-only providers are included. Both expose only the 12 standard bracket-class names (`alnum`, `alpha`, `blank`, `cntrl`, `digit`, `graph`, `lower`, `print`, `punct`, `space`, `upper`, and `xdigit`); GNU `\w` uses the separate word-character provider method and does not make `[[:word:]]` a valid POSIX class.

- `UnicodeRegularExpressionCharacterClassProvider` uses `Rune` Unicode categories and a supplied `CultureInfo.CompareInfo` for scalar collation, culture-aware case comparison, and a BCL approximation of single-scalar collation-equivalence classes.
- `PosixCLocaleRegularExpressionCharacterClassProvider` implements deterministic ASCII classification and ordinal collation for the POSIX C locale. It is useful for differential tests, invariant command modes, globalization-restricted deployments, and best-effort ports.

Tools may inject another provider for a host locale implementation or a deterministic test double. The engine itself never calls libc locale or regex functions.

The BCL does not expose a locale's complete collating-element inventory. Single-scalar collating symbols and equivalence classes are supported. Multi-scalar collating elements produce the controlled `UnsupportedCollatingElement` diagnostic rather than falling back silently or throwing an unhandled exception. POSIX leaves multi-character bracket matching partly unspecified, so later platform providers can extend this boundary without changing command code.

## Differences from `System.Text.RegularExpressions`

This code is not a pattern translator layered over `System.Text.RegularExpressions.Regex`.

1. GNU BRE uses escaped grouping, alternation, plus, question-mark, and interval operators: `\(`, `\|`, `\+`, `\?`, and `\{...\}`.
2. POSIX/GNU selects the leftmost-longest whole match, while the Gnulib `re_match` interface also has observable register-selection behavior for equal endpoints and repeated subexpressions. The normal .NET backtracking engine does not provide that contract.
3. GNU contextual repetition and interval behavior is profile-driven, including the stricter default and Coreutils `expr`'s explicit Gnulib compatibility changes.
4. GNU word, whitespace, and whole-input operators are included directly.
5. POSIX named classes and range order are delegated to an injectable locale policy.
6. Match positions advance by Unicode scalar, while public offsets remain UTF-16 indices.
7. The engine returns structured compile and match diagnostics instead of `RegexParseException` or an unhandled resource failure.
8. Perl/.NET constructs are intentionally not part of GNU BRE: named and noncapturing groups, lookaround, atomic groups, balancing groups, lazy quantifiers, inline options, Unicode property escapes, conditionals, and replacement syntax are not recognized as such.
9. `RegexOptions.NonBacktracking` is not a substitute because GNU BRE back-references and Gnulib capture-register selection require semantics outside that mode's contract.

## Resource and cancellation policy

`MaximumNestingDepth` bounds parenthesized-subexpression recursion and nested adjacent repetition operators so parser and matcher structure cannot grow without control. `MaximumMatchStates` bounds the state-space explored by one search. Exceeding that bound returns `MatchResourceLimitExceeded`; it does not expose an internal exception. The default state limit is `int.MaxValue` so command implementations may choose a policy appropriate to their documented behavior.

Cancellation is checked while parsing, decoding search positions, evaluating nodes, expanding repetitions, and comparing back-references. Cancellation uses the normal `OperationCanceledException` contract rather than being converted into a regex diagnostic.

## Platform profile

The implementation is fully managed and has no P/Invoke path:

- `windows-latest`, `ubuntu-latest`, and `macos-latest` execute the same parser and matcher;
- BSD-family behavior is best effort and should be identical under a compatible .NET 10 runtime, subject to the selected culture data;
- TempleOS support is best effort and requires a compatible managed runtime/BCL port; the C-locale provider avoids a dependency on host globalization or native regex libraries.

No command should infer success when a requested regex feature cannot be represented. It should surface the compile or match diagnostic through its normal controlled-exit path.
