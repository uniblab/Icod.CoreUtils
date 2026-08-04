# Icod.LineEditor.Sed tests

This directory contains both the established command-level Sed suite and the Phase LE1 decomposition coverage.

| File | Purpose |
|---|---|
| `SedCommandTests.cs` | Existing command-level behavior and conformance coverage retained unchanged. |
| `SedCharacterizationTests.cs` | LE1 behavior freeze for option ordering, script-source ordering, diagnostics, implicit script mode, current record termination, sandbox denial, and in-place-edit startup. |
| `SedModuleBoundaryTests.cs` | Focused structural tests preserving the public `Command` signatures and private implementation boundary during decomposition. |

Later semantic phases should update only the characterization assertions whose behavior is intentionally changed by the roadmap. They must not delete the command-level suite merely because equivalent lower-level coverage is introduced.
