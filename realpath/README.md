# realpath

`Icod.CoreUtils.RealPath` implements GNU-compatible lexical, physical, and no-link canonical pathname output over the neutral `Icod.Path` resolver.

The conformance baseline is GNU Coreutils 9.11. The command supports three missing-component policies, relative output, newline/NUL delimiters, quiet diagnostics, cancellation, and per-operand continuation. It never reports an unresolved input spelling as a successful canonical path.
