# Icod.CoreUtils.Test.Tests

This project verifies the Batch 37 GNU/POSIX `test` expression evaluator. It covers operand-count ambiguity rules, connectives and precedence, strings, arbitrary-precision integers, file metadata predicates, link identity, timestamps, ownership, access checks, terminal descriptors, diagnostics, cancellation, and basic host-filesystem integration.

The production command remains the sole condition-evaluator executable. No separate `[` project is created; `--help`, `--version`, and `[` are ordinary string operands when passed to `test`.
