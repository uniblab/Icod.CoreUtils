# Icod.Patch source layout

The source directory contains the complete Wave A parsers and Wave B1 pure application engine:

- `Command.cs` owns public invocation, shared option parsing, diagnostics, compatibility wrappers, help, version, cancellation, and Wave B1 option validation.
- `PatchApplication.cs` selects the byte-oriented patch source, coordinates detection and parsing, and maps command policy into engine options. It deliberately performs no live path integration before P7.
- `PatchSource.cs` streams patch input into an owner-private temporary spool while retaining bounded line metadata and exact record terminators.
- `PatchScanner.cs` classifies structural records and finds count-aware unified, context, normal, and ed-script sections without splitting header-looking hunk or ed data.
- `PatchModels.cs` contains source locations, records, text regions, detected sections, scan limits, exceptions, and exit-status accumulation.
- `PatchSyntaxModels.cs` contains the immutable common file, range, hunk, operation, data-line, and exact raw-record models.
- `PatchParser.cs` materializes bounded source sections, tracks parser budgets, and dispatches to format parsers.
- `UnifiedContextPatchParser.cs` implements complete unified and context grammar normalization.
- `NormalEdPatchParser.cs` implements normal commands and the minimal GNU-compatible ed grammar, including internal single-dot unprotection.
- `PatchTargetContent.cs` owns in-memory or spill-backed byte-preserved target records, streams long records without whole-record buffering, and cleans temporary storage deterministically.
- `PatchEngineModels.cs` defines pure application policy, limits, direction decisions, virtual files, and immutable hunk/file results.
- `PatchApplicationEngine.cs` performs exact and heuristic virtual application, ed interpretation, offsets, fuzz, reversal, prerequisites, and merge output without selecting paths or mutating the filesystem.
- `PatchPrerequisite.cs` extracts and checks GNU-style `Prereq:` tokens.
- `PatchTemporaryFile.cs` creates exclusive owner-private temporary files shared by source, target, and result storage.
- `AssemblyInfo.cs` exposes internals only to the dedicated test assembly.

Later phases should preserve `Command` as the orchestration boundary, consume the shared E2 path model in P7, and keep committed mutation behind the P8/P9 transaction boundary.
