# Command line

The `Icod.CoreUtils.Shared.CommandLine` namespace implements the reusable GNU/POSIX-style option parser used throughout the repository.

## Responsibilities

- Parse short options, clustered short options, long options, and long-option abbreviations.
- Represent option occurrences, operands, original argument positions, and deterministic parse errors.
- Support required, optional, and absent option values.
- Preserve encounter order and configurable operand ordering.
- Apply narrowly scoped compatibility rewrites before normal option parsing.

## Design notes

The parser handles command-line syntax only. Command-specific grammars, semantic validation, and execution remain in the command project. Parse results retain enough source information for stable GNU-style diagnostics and tests.
