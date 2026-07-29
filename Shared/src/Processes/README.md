# Processes

The `Icod.CoreUtils.Shared.Processes` namespace supplies reusable child-process execution infrastructure.

## Responsibilities

- Configure executable paths, arguments, environment variables, and working directories.
- Redirect and capture standard output and standard error.
- Write optional standard input.
- Support cancellation and controlled termination.
- Return exit status and captured output as a structured result.

## Design notes

This infrastructure is for commands whose documented behavior requires executing another program. It must not be used to delegate implementation to the native equivalent of the command being implemented.
