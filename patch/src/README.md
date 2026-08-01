# Icod.Patch source layout

The current source directory contains the P0-P2 front end and source model:

- `Command.cs` owns public invocation, shared option parsing, diagnostics, compatibility wrappers, help, version, and cancellation.
- `PatchApplication.cs` selects the byte-oriented patch source and coordinates the pure scan. It deliberately performs no target mutation in P0-P2.
- `PatchSource.cs` streams input into a private temporary spool while retaining bounded line metadata and exact record terminators.
- `PatchScanner.cs` classifies structural records and detects unified, context, normal, and ed-script candidates without parsing hunks prematurely.
- `PatchModels.cs` contains source locations, records, text regions, sections, limits, exceptions, and exit-status accumulation.
- `AssemblyInfo.cs` exposes internals only to the dedicated test assembly.

Later phases should preserve `Command` as the orchestration boundary and keep parsing and matching independent from filesystem mutation.
