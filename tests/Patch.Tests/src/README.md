# Icod.Patch.Tests source layout

- `CommandTests.cs` covers asynchronous and synchronous invocation, source selection, diagnostics, cancellation, seed-format retirement, status accumulation, and the no-mutation P0-P2 boundary.
- `PatchScannerTests.cs` covers byte offsets, LF/CRLF/CR/incomplete records, unified/context/normal/ed detection, multiple sections, surrounding text, binary input, directive hardening, resource limits, cleanup, and deterministic fuzz input.
- `ProcessHostTests.cs` exercises the built executable through the `dotnet` process host.
