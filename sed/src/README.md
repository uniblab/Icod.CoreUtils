# Icod.LineEditor.Sed source layout

Phase LE1 decomposes the original single-file Sed implementation without changing its public API or its established behavior. `Icod.LineEditor.Sed.Command` remains one public partial class so every previously private implementation type remains private to the command boundary while source ownership becomes reviewable.

## Modules

| File | Responsibility |
|---|---|
| `Command.cs` | Public `Run` and `RunAsync` orchestration, stable exit-status boundary, and command constants. |
| `SedOptions.cs` | Command-line options, shared option-parser integration, help text, and version handling. |
| `SedScripting.cs` | Instruction kinds, program model, script parser, text/file arguments, and current script-file loading. |
| `SedAddresses.cs` | Single addresses, GNU range extensions, range state, negation, and selection evaluation. |
| `SedExecution.cs` | Pattern/hold-space command cycle, deferred output, execution state, debug presentation, and list formatting. |
| `SedRecords.cs` | Input sources, one-record lookahead, LF/NUL record reading, and current output-record serialization. |
| `SedRegularExpressions.cs` | Temporary BRE and POSIX-class translation into `System.Text.RegularExpressions`; LE3 replaces this module through the Shared regex provider. |
| `SedSubstitution.cs` | Substitution flags, replacement expansion, transliteration, and character-set expansion. |
| `SedProcesses.cs` | Shell execution through the shared process runner and text-writer stream adaptation. |
| `SedFiles.cs` | Current command-local in-place editing, backup naming, and symlink-path selection; LE10 later migrates replacement to E6. |

## LE1 invariants

- `Command.Run` and `Command.RunAsync` retain their signatures and caller-owned stream behavior.
- All implementation types remain non-public details of `Command`.
- Script fragments continue to be combined with `Environment.NewLine`; LE5 changes script-source representation deliberately.
- Record reading and writing retain the pre-LE1 decoded-line behavior; LE4 introduces byte-preserving records and explicit final termination.
- Regular expressions retain the private .NET translation behavior; LE3 performs the semantic migration.
- In-place editing retains the existing command-local replacement path; LE10 performs the E6 migration.

The characterization tests in `tests/Sed.Tests/src/SedCharacterizationTests.cs` record these temporary semantics so later phases can distinguish intentional semantic work from accidental refactoring regressions.
