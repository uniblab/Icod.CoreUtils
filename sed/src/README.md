# Icod.LineEditor.Sed source layout

Phase LE1 decomposed the original single-file Sed implementation without changing its public API. Phase LE3 migrated regular-expression execution to the Shared managed GNU BRE/ERE provider, and Phase LE4 now supplies byte-preserving record framing, explicit termination, and locale-selected byte/text semantics while preserving Sed-specific state and policy inside the command. `Icod.LineEditor.Sed.Command` remains one public partial class so every previously private implementation type remains private to the command boundary while source ownership becomes reviewable.

## Modules

| File | Responsibility |
|---|---|
| `Command.cs` | Public text-stream `Run` and `RunAsync` compatibility orchestration, internal byte-stream core, stable exit-status boundary, and command constants. |
| `SedOptions.cs` | Command-line options, shared option-parser integration, help text, and version handling. |
| `SedScripting.cs` | Instruction kinds, program model, script parser, text/file arguments, and current script-file loading. |
| `SedAddresses.cs` | Single addresses, GNU range extensions, range state, negation, and selection evaluation. |
| `SedExecution.cs` | Pattern/hold-space command cycle, explicit termination propagation, deferred output, execution state, debug presentation, and list formatting. |
| `SedRecords.cs` | `SedInputRecord`, Shared LF/NUL byte framing, source and record identity, C/POSIX and UTF-8 codecs, invalid-byte preservation, one-record lookahead, explicit output serialization, and text-stream compatibility adapters. |
| `SedRegularExpressions.cs` | `SedRegularExpressionCompiler`, GNU Sed escape preprocessing, Shared BRE/ERE provider selection, empty-expression reuse, GNU/POSIX policy, locale selection, controlled diagnostics, and GNU zero-length match iteration. |
| `SedSubstitution.cs` | Substitution flags, replacement expansion, transliteration, and character-set expansion. |
| `SedProcesses.cs` | Shell execution through the shared process runner and text-writer stream adaptation. |
| `SedFiles.cs` | Current command-local in-place editing, backup naming, and symlink-path selection; LE10 later migrates replacement to E6. |

## LE1 invariants

- `Command.Run` and `Command.RunAsync` retain their signatures and caller-owned stream behavior.
- All implementation types remain non-public details of `Command`.
- Script fragments continue to be combined with `Environment.NewLine`; LE5 changes script-source representation deliberately.
- Record reading and writing now follow the LE4 byte-preserving record and explicit final-termination contract; script-source joining remains scheduled for LE5.
- Regular expressions compile through the Shared managed GNU provider; Sed continues to own empty-expression reuse, address/substitution modifiers, occurrence selection, zero-length iteration, replacement expansion, and diagnostics.
- In-place editing retains the existing command-local replacement path; LE10 performs the E6 migration.

The characterization tests in `tests/Sed.Tests/src/SedCharacterizationTests.cs` record these temporary semantics so later phases can distinguish intentional semantic work from accidental refactoring regressions.

## LE3 regular-expression boundary

- `SedRegularExpressionCompiler` selects Shared Basic or Extended syntax once per script parser.
- A nonempty address or substitution expression becomes the new shared "last regular expression"; an empty expression reuses the exact compiled object, including its original `I`/`M` policy.
- Address `I` and `M` modifiers and substitution `i`/`I` and `m`/`M` flags remain Sed syntax. New modifiers on an empty expression are rejected.
- GNU/POSIX mode is interpreted by the adapter rather than by weakening the Shared provider contract. GNU Sed control and numeric escapes are expanded before regex parsing; `--posix` suppresses that expansion only inside raw bracket expressions.
- Global substitution iteration follows GNU Sed's empty-match progression rule and consumes Shared leftmost-longest matches.
- Locale classification uses the LE4 Shared text-locale profile: C/POSIX selects byte classification, while every UTF-8 profile uses Unicode character classes with the process culture for collation, including invariant culture.

## LE4 record and text boundary

- Standard input and files are framed with Shared `ByteRecordReader`; only the configured LF or NUL separator is removed.
- `SedInputRecord` retains authoritative bytes, decoded text, source identity, aggregate and per-source numbers, separator kind, final termination, and representable byte coordinates.
- `TextLocaleEnvironment` selects a C/POSIX byte profile or UTF-8 profile. Malformed UTF-8 bytes map to reversible reserved UTF-16 code units rather than replacement characters.
- Data output uses Shared `DelimitedByteRecordWriter`; host newlines are presentation-only.
- The active record separator is also the internal pattern/hold-space separator used by `N`, `D`, `P`, `H`, `G`, and `W`. `l` renders internal NUL as `\000`.
- `SedRegularExpressionCompiler` passes LF or NUL to Shared `RegularExpressionOptions.LineSeparator`; `-z` explicitly enables NUL dot matching outside multiline mode, while `M` treats NUL as the line boundary.
- The executable uses raw standard streams. The public text-stream methods remain compatibility adapters and do not own caller streams.
- The current record and one lookahead record are retained. Pattern and hold spaces may grow without a fixed bound because that growth is required by Sed commands.

See `Icod.LineEditor-LE4-Record-and-Text-Semantics.md` for the complete contract and deferred LE5 boundary.
