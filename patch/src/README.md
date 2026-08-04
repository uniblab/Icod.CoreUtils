# Icod.Patch source layout

The source directory contains the closed P0–P12 implementation: complete Wave A parsers, the Wave B1 pure application engine, the Wave B2 path-planning layer, Phase P8 artifact policy, and the stabilized P11A/P11B adapter over shared E6:

- `Command.cs` owns public invocation, shared option parsing, environment policy, final 1.0 capability diagnostics, compatibility wrappers, help, version, cancellation, and GNU option validation.
- `PatchApplication.cs` acquires the byte-oriented patch source, coordinates scanning and parsing, invokes P7 planning and P8 artifact planning, handles dry runs and byte-oriented standard output, and commits through the injected E6-backed boundary.
- `PatchArtifacts.cs` derives explicit target, backup, reject, and output artifacts from final P7 virtual state; implements GNU backup/reject/output naming and metadata policy; assigns per-file recovery units; quotes hostile pathnames; and consolidates repeated patches to one canonical target.
- `PatchFileSystem.cs` defines `IPatchFileSystem` and `IPatchTransaction`, consumes E2/E3/E4 path, metadata, and mutation providers, enforces lexical and physical artifact containment, constructs the shared E6 provider, and forwards its stabilized atomicity and durability capability record without a Patch-local translation layer.
- `PatchE6Transaction.cs` adapts immutable Patch artifacts and per-file recovery units to `TransactionalFileReplacementTransaction`; it delegates secure sibling staging, flush, E3 revalidation, atomic publication, retained backups, rollback, metadata restoration, containment, cancellation, diagnostics, and cleanup to E6.
- `PatchE6Contract.cs` retains the frozen Patch requirement matrix used by P10 and P11 validation.
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

P8 consumes the P7 plan and does not repeat filename selection or matching. P11A keeps GNU-visible backup, reject, output, and multi-file partial-success policy in Patch while delegating transaction mechanics to shared E6. P11B removed the unreachable P9 implementation after `cp`, `mv`, and `install` independently validated the stabilized contract; no command-local replacement engine remains.

P12 leaves no provisional phase diagnostics in production. The source-defined
`ifdef`, `read-only`, and conditional GNU debugging options remain present in the
complete parser inventory but produce final capability diagnostics rather than
promising a later phase. The final behavior and residual-gap ledgers live under
`patch/upstream`.

