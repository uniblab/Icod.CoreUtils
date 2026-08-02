# readlink

`Icod.CoreUtils.ReadLink` implements GNU-compatible direct symbolic-link inspection and canonicalization over the neutral `Icod.Path` resolver.

The conformance baseline is GNU Coreutils 9.11. The command supports strict, missing-final, and missing-suffix canonicalization, newline/NUL delimiters, quiet and verbose diagnostics, cancellation, and per-operand continuation. It never reports an unresolved input spelling as a successful canonical path.
