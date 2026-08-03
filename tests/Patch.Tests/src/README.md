# Icod.Patch.Tests source layout

- `CommandTests.cs` covers asynchronous and synchronous invocation, source selection, format forcing, diagnostics, cancellation, seed-format retirement, status accumulation, and committed target application.
- `PatchScannerTests.cs` covers byte offsets, LF/CRLF/CR/incomplete records, count-aware format detection, multiple sections, surrounding text, binary input, directive hardening, resource limits, cleanup, and deterministic fuzz input.
- `PatchParserTests.cs` covers complete unified/context/normal/ed parsing, immutable normalization, exact raw-record retention, creation/deletion forms, count validation, context-copy consistency, ed ordering, binary records, interstitial text, and parser limits.
- `PatchTargetContentTests.cs` covers exact target round trips, memory/spill thresholds, streaming long records and prerequisite searches, resource limits, and deterministic private-spool cleanup.
- `PatchApplicationEngineTests.cs` covers exact and multi-hunk application, independent immutable ownership, ed operations, virtual creation/deletion, offset order, fuzz, whitespace canonicalization, reversal, direction policy, prerequisites, merge/diff3 output, candidate limits, randomized invariants, and cancellation.
- `PatchPathPlannerTests.cs` covers platform-aware `-p`, explicit operands, GNU/POSIX candidate ranking, `Index:` evidence, `-d`, quoted names, multiple sections, virtual creation/deletion, revision-control policy, roots, volumes, alternate separators, links, reparse points, containment escapes, and aggregate statuses over an injected filesystem.
- `WaveB1CommandTests.cs` covers P6 option validation and command-to-engine policy mapping.
- `WaveB2CommandTests.cs` covers P7 option/environment mapping, invalid numeric policies, and `-d` interpretation of a relative patch source.
- `WaveCCommandTests.cs` covers P8 backup naming and environment precedence, version-control abbreviations, output-file and standard-output modes, output-link policy, rejects, dry runs, prompts, quoting, current and post-2038 timestamps, Unix mode preservation, unsafe artifact names, broken pipes, and GNU-visible statuses.
- `WaveCTransactionTests.cs` covers the initial P9 staging, destination and validation-only input revalidation, injected commit and metadata failures, multi-artifact rollback, cancellation cleanup, and temporary-file removal.
- `GnuPatchDifferentialTests.cs` provides opt-in comparisons with an installed GNU patch 2.8 executable without making native patch a normal test dependency.
- `PatchTestSupport.cs` provides byte-oriented parser, virtual-file, and result helpers.
- `ProcessHostTests.cs` exercises the built executable through the `dotnet` process host.
