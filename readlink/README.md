# READLINK(1)

## NAME

**readlink** — print symbolic link values or canonical file names

## SYNOPSIS

```text
readlink [OPTION]... FILE...
```

## PATHNAME GLOBBING

Command-line operands that actually contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition, and unmatched patterns are preserved as literal operands.

Non-pattern operands bypass pathname expansion and retain their original spelling so the neutral `Icod.Path` resolver can interpret the intended pathname dialect without host-separator rewriting. Globbing selects operands; it does not canonicalize them.

## DESCRIPTION

`Icod.CoreUtils.ReadLink` implements GNU-compatible direct symbolic-link inspection and canonicalization over the neutral `Icod.Path` resolver.

The conformance baseline is GNU Coreutils 9.11. The command supports strict, missing-final, and missing-suffix canonicalization, newline/NUL delimiters, quiet and verbose diagnostics, cancellation, and per-operand continuation. It never reports an unresolved input spelling as a successful canonical path.

## AUTHORS

GNU `readlink` was written by Dmitry V. Levin.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`readlink(1)`, `realpath(1)`
