# Icod.Patch.Tests source layout

- `CommandTests.cs` covers asynchronous and synchronous invocation, source selection, format forcing, diagnostics, cancellation, seed-format retirement, status accumulation, and the no-mutation Wave A boundary.
- `PatchScannerTests.cs` covers byte offsets, LF/CRLF/CR/incomplete records, count-aware unified/context/normal/ed detection, multiple sections, surrounding text, binary input, directive hardening, resource limits, cleanup, and deterministic fuzz input.
- `PatchParserTests.cs` covers complete unified/context/normal/ed parsing, immutable normalization, exact raw-record retention, creation/deletion forms, range and count validation, context-copy consistency, ed ordering and single-dot protection, binary records, interstitial text, and parser limits.
- `ProcessHostTests.cs` exercises the built executable through the `dotnet` process host.
