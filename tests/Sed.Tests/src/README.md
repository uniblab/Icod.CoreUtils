# Icod.LineEditor.Sed tests

This directory contains the established command-level Sed suite, the LE1 decomposition coverage, and the LE3 Shared-regex migration suite.

| File | Purpose |
|---|---|
| `SedCommandTests.cs` | Existing command-level behavior and conformance coverage retained unchanged. |
| `SedCharacterizationTests.cs` | LE1 behavior freeze for option ordering, script-source ordering, diagnostics, implicit script mode, current record termination, sandbox denial, and in-place-edit startup. |
| `SedModuleBoundaryTests.cs` | Focused structural tests preserving the public `Command` signatures and private implementation boundary during decomposition and regex migration. |
| `SedRegularExpressionMigrationTests.cs` | GNU Sed 4.10 differential cases for BRE, ERE, captures, leftmost-longest selection, repeated zero-length matches, empty-expression reuse, modifiers, GNU escape preprocessing, strict-POSIX bracket policy, locale classes, and controlled diagnostics. |

Later semantic phases should update only the characterization assertions whose behavior is intentionally changed by the roadmap. They must not delete the command-level suite merely because equivalent lower-level coverage is introduced.

LE3 removes the private `System.Text.RegularExpressions` translation path. The migration tests deliberately exercise behavior where .NET leftmost-first selection and default Unicode character classes differ from GNU/POSIX Sed expectations.
