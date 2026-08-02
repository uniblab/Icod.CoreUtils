# Icod.Patch.Tests source layout

- `CommandTests.cs` covers asynchronous and synchronous invocation, source selection, format forcing, diagnostics, cancellation, seed-format retirement, status accumulation, and the no-live-mutation boundary.
- `PatchScannerTests.cs` covers byte offsets, LF/CRLF/CR/incomplete records, count-aware format detection, multiple sections, surrounding text, binary input, directive hardening, resource limits, cleanup, and deterministic fuzz input.
- `PatchParserTests.cs` covers complete unified/context/normal/ed parsing, immutable normalization, exact raw-record retention, creation/deletion forms, count validation, context-copy consistency, ed ordering, binary records, interstitial text, and parser limits.
- `PatchTargetContentTests.cs` covers exact target round trips, memory/spill thresholds, streaming long records and prerequisite searches, resource limits, and deterministic private-spool cleanup.
- `PatchApplicationEngineTests.cs` covers exact and multi-hunk application, independent immutable ownership, ed operations, virtual creation/deletion, offset order, fuzz, whitespace canonicalization, reversal, direction policy, prerequisites, merge/diff3 output, candidate limits, randomized invariants, and cancellation.
- `PatchPathPlannerTests.cs` covers platform-aware `-p`, explicit operands, GNU/POSIX candidate ranking, `Index:` evidence, `-d`, quoted names, multiple sections, virtual creation/deletion, revision-control policy, roots, volumes, alternate separators, links, reparse points, containment escapes, and aggregate statuses over an injected filesystem.
- `WaveB1CommandTests.cs` covers P6 option validation and command-to-engine policy mapping.
- `WaveB2CommandTests.cs` covers P7 option/environment mapping, invalid numeric policies, and `-d` interpretation of a relative patch source.
- `GnuPatchDifferentialTests.cs` provides opt-in comparisons with an installed GNU patch 2.8 executable without making native patch a normal test dependency.
- `PatchTestSupport.cs` provides byte-oriented parser, virtual-file, and result helpers.
- `ProcessHostTests.cs` exercises the built executable through the `dotnet` process host.
