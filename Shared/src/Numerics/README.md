# Numerics

The `Icod.CoreUtils.Shared.Numerics` namespace provides culture-independent numeric parsing and exact arithmetic used by multiple commands.

## Responsibilities

- Parse signed and unsigned quantities with GNU-style suffix tables.
- Distinguish syntax, suffix, range, and overflow failures.
- Support configurable overflow behavior.
- Represent exact rational values with arbitrary-precision integers.
- Parse floating-point quantities with reusable suffix policies.

## Design notes

Parsing is deterministic and invariant-culture unless a command explicitly requires locale-sensitive behavior. Commands should use these result types instead of relying on exceptions for normal invalid-input diagnostics.
