# Icod.Patch source layout

The current source directory contains the complete Wave A front end, source model, and syntax parsers:

- `Command.cs` owns public invocation, shared option parsing, diagnostics, compatibility wrappers, help, version, and cancellation.
- `PatchApplication.cs` selects the byte-oriented patch source and coordinates detection plus complete syntax parsing. It deliberately performs no target mutation in Wave A.
- `PatchSource.cs` streams input into a private temporary spool while retaining bounded line metadata and exact record terminators.
- `PatchScanner.cs` classifies structural records and finds count-aware unified, context, normal, and ed-script sections without splitting header-looking hunk or ed data.
- `PatchModels.cs` contains source locations, records, text regions, detected sections, scan limits, exceptions, and exit-status accumulation.
- `PatchSyntaxModels.cs` contains the immutable common file, range, hunk, operation, data-line, and exact raw-record models.
- `PatchParser.cs` materializes bounded source sections, tracks parser budgets, and dispatches to format parsers.
- `UnifiedContextPatchParser.cs` implements complete unified and context grammar normalization.
- `NormalEdPatchParser.cs` implements normal commands and the minimal GNU-compatible ed grammar, including internal single-dot unprotection.
- `AssemblyInfo.cs` exposes internals only to the dedicated test assembly.

Later phases should preserve `Command` as the orchestration boundary and keep parsing and matching independent from filesystem mutation.
