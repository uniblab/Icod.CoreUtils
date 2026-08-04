# Icod.LineEditor.Sed source layout

Phase LE1 decomposed the original single-file Sed implementation without changing its public API. Phase LE3 now migrates regular-expression execution to the Shared managed GNU BRE/ERE provider while preserving Sed-specific state and policy inside the command. `Icod.LineEditor.Sed.Command` remains one public partial class so every previously private implementation type remains private to the command boundary while source ownership becomes reviewable.

## Modules

| File | Responsibility |
|---|---|
| `Command.cs` | Public `Run` and `RunAsync` orchestration, stable exit-status boundary, and command constants. |
| `SedOptions.cs` | Command-line options, shared option-parser integration, help text, and version handling. |
| `SedScripting.cs` | Instruction kinds, program model, script parser, text/file arguments, and current script-file loading. |
| `SedAddresses.cs` | Single addresses, GNU range extensions, range state, negation, and selection evaluation. |
| `SedExecution.cs` | Pattern/hold-space command cycle, deferred output, execution state, debug presentation, and list formatting. |
| `SedRecords.cs` | Input sources, one-record lookahead, LF/NUL record reading, and current output-record serialization. |
| `SedRegularExpressions.cs` | `SedRegularExpressionCompiler`, GNU Sed escape preprocessing, Shared BRE/ERE provider selection, empty-expression reuse, GNU/POSIX policy, locale selection, controlled diagnostics, and GNU zero-length match iteration. |
| `SedSubstitution.cs` | Substitution flags, replacement expansion, transliteration, and character-set expansion. |
| `SedProcesses.cs` | Shell execution through the shared process runner and text-writer stream adaptation. |
| `SedFiles.cs` | Current command-local in-place editing, backup naming, and symlink-path selection; LE10 later migrates replacement to E6. |

## LE1 invariants

- `Command.Run` and `Command.RunAsync` retain their signatures and caller-owned stream behavior.
- All implementation types remain non-public details of `Command`.
- Script fragments continue to be combined with `Environment.NewLine`; LE5 changes script-source representation deliberately.
- Record reading and writing retain the pre-LE1 decoded-line behavior; LE4 introduces byte-preserving records and explicit final termination.
- Regular expressions compile through the Shared managed GNU provider; Sed continues to own empty-expression reuse, address/substitution modifiers, occurrence selection, zero-length iteration, replacement expansion, and diagnostics.
- In-place editing retains the existing command-local replacement path; LE10 performs the E6 migration.

The characterization tests in `tests/Sed.Tests/src/SedCharacterizationTests.cs` record these temporary semantics so later phases can distinguish intentional semantic work from accidental refactoring regressions.

## LE3 regular-expression boundary

- `SedRegularExpressionCompiler` selects Shared Basic or Extended syntax once per script parser.
- A nonempty address or substitution expression becomes the new shared "last regular expression"; an empty expression reuses the exact compiled object, including its original `I`/`M` policy.
- Address `I` and `M` modifiers and substitution `i`/`I` and `m`/`M` flags remain Sed syntax. New modifiers on an empty expression are rejected.
- GNU/POSIX mode is interpreted by the adapter rather than by weakening the Shared provider contract. GNU Sed control and numeric escapes are expanded before regex parsing; `--posix` suppresses that expansion only inside raw bracket expressions.
- Global substitution iteration follows GNU Sed's empty-match progression rule and consumes Shared leftmost-longest matches.
- Locale classification uses the process culture, with invariant culture mapped to the deterministic POSIX C-locale provider. Phase LE4 remains responsible for byte-preserving input and explicit encoding policy.
