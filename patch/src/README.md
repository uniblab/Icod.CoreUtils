# Icod.Patch source layout

The source directory contains complete Wave A parsers, the Wave B1 pure application engine, the Wave B2 path-planning layer, Phase P8 artifact policy, and the provisional Phase P9 transaction boundary:

- `Command.cs` owns public invocation, shared option parsing, environment policy, diagnostics, compatibility wrappers, help, version, cancellation, and GNU option validation.
- `PatchApplication.cs` acquires the byte-oriented patch source, coordinates scanning and parsing, invokes P7 planning and P8 artifact planning, handles dry runs and byte-oriented standard output, and commits through the injected P9 boundary.
- `PatchArtifacts.cs` derives explicit target, backup, reject, and output artifacts from final P7 virtual state; implements GNU backup/reject/output naming and metadata policy; quotes hostile pathnames; and consolidates repeated patches to one canonical target.
- `PatchFileSystem.cs` defines `IPatchFileSystem` and `IPatchTransaction`, consumes E2/E3/E4 path, metadata, and mutation providers, applies final-link policy to every artifact pathname, stages complete sibling temporary files, revalidates destination and validation-only input identity, applies mode/timestamp metadata, and provides provisional rollback, cancellation cleanup, and deterministic failure injection pending E6.
- `PatchInteraction.cs` supplies deterministic command-line answers for reversal, prerequisite, and version-control questions without allowing the patch source to compete for standard input.
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
- `PatchPathSelection.cs` decodes quoted filename evidence, extracts `Index:` records, and applies platform-aware component stripping and candidate-ranking inputs.
- `PatchPathModels.cs` defines candidate evidence, planned actions, multi-file plan ownership, the read-only path filesystem boundary, and revision-control retrieval policy/results.
- `PatchApplicationPlanner.cs` consumes `Icod.Path`, selects secure canonical targets, carries virtual state across sections, optionally retrieves missing content through an injected provider, invokes the pure engine, and aggregates the multi-file plan.
- `PatchTemporaryFile.cs` creates exclusive owner-private temporary files shared by source, target, and result storage.
- `AssemblyInfo.cs` exposes internals only to the dedicated test assembly.

P8 consumes the P7 plan and does not repeat filename selection or matching. The current P9 adapter is intentionally replaceable: it establishes artifact, staging, revalidation, failure, rollback, and cleanup contracts but does not claim final E6 transaction or atomicity semantics.
