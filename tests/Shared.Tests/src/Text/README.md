# Shared text tests

After the Completion Gate G3 text contraction, this directory contains only tests for Coreutils-owned text policy.

- `TabStopParserTests.cs` verifies GNU tab-list grammar, repeated specifications, `/N`, `+N`, compatibility edge cases, structured diagnostics, and overflow handling while consuming the tab-stop model from `Icod.CommandFramework.Text`.

Tests for byte-preserving text units, logical-line reading, locale resolution, Unicode display width, display-column state, and tab-stop model mechanics live with their owning implementation in `Icod.CommandFramework.Tests`.
