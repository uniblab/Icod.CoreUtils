# Icod.Patch source layout

The source directory contains complete Wave A parsers, the Wave B1 pure application engine, and the Wave B2 path-planning layer:

- `Command.cs` owns public invocation, shared option parsing, diagnostics, compatibility wrappers, help, version, cancellation, and P1/P6/P7 option validation.
- `PatchApplication.cs` acquires the byte-oriented patch source, coordinates scanning and parsing, invokes the P7 planner, and deliberately stops before artifact creation or committed mutation.
- `PatchSource.cs` streams patch input into an owner-private temporary spool while retaining bounded line metadata and exact record terminators.
- `PatchScanner.cs` classifies structural records and finds count-aware unified, context, normal, and ed-script sections without splitting header-looking hunk or ed data.
- `PatchModels.cs` contains source locations, records, text regions, detected sections, scan limits, exceptions, and exit-status accumulation.
- `PatchSyntaxModels.cs` contains immutable common file, range, hunk, operation, data-line, and exact raw-record models.
- `PatchParser.cs` materializes bounded source sections, tracks parser budgets, and dispatches to format parsers.
- `UnifiedContextPatchParser.cs` implements complete unified and context grammar normalization.
- `NormalEdPatchParser.cs` implements normal commands and the minimal GNU-compatible ed grammar, including internal single-dot unprotection.
- `PatchTargetContent.cs` owns in-memory or spill-backed byte-preserved target records and deterministic temporary-storage cleanup.
- `PatchEngineModels.cs` defines pure application policy, limits, direction decisions, virtual files, and immutable hunk/file results.
- `PatchApplicationEngine.cs` performs exact and heuristic virtual application, ed interpretation, offsets, fuzz, reversal, prerequisites, and merge output without selecting paths or mutating the filesystem.
- `PatchPrerequisite.cs` extracts and checks GNU-style `Prereq:` tokens.
- `PatchPathSelection.cs` decodes quoted filename evidence, extracts `Index:` records, and applies platform-aware component stripping and candidate ranking inputs.
- `PatchPathModels.cs` defines candidate evidence, planned actions, multi-file plan ownership, the read-only path filesystem boundary, and revision-control retrieval policy/results.
- `PatchApplicationPlanner.cs` consumes `Icod.Path`, selects secure canonical targets, carries virtual state across sections, optionally retrieves missing content through an injected provider, invokes the pure engine, and aggregates the multi-file plan.
- `PatchTemporaryFile.cs` creates exclusive owner-private temporary files shared by source, target, and result storage.
- `AssemblyInfo.cs` exposes internals only to the dedicated test assembly.

P8 must consume the P7 plan rather than reimplement filename selection. It owns rejects, backups, output files, prompts, metadata/mode behavior, and final status presentation. P9/P11 own the injected safe mutation and replacement boundary.
