# Diagnostics

The `Icod.CoreUtils.Shared.Diagnostics` namespace defines the common execution context, exit statuses, command result, and diagnostic writer used by command implementations.

## Responsibilities

- Carry injected standard input, output, and error streams.
- Carry the program name and cancellation token.
- Format program-name-prefixed errors and warnings.
- Provide stable command exit-code constants and lightweight command results.

## Design notes

Commands must not dispose caller-owned streams. Expected usage, platform, and I/O failures should be converted into deterministic diagnostics and controlled exit statuses instead of escaping as unhandled exceptions.
