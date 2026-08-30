# REALPATH(1)

## NAME

**realpath** — print canonicalized absolute file names

## SYNOPSIS

```text
realpath [OPTION]... FILE...
```

## PATHNAME GLOBBING

Command-line operands that actually contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition, and unmatched patterns are preserved as literal operands.

Non-pattern operands bypass pathname expansion and retain their original spelling so the neutral `Icod.Path` resolver can interpret the intended pathname dialect without host-separator rewriting. `--relative-to=DIR` and `--relative-base=DIR` are literal control pathnames and are not glob-expanded.

## DESCRIPTION

`Icod.CoreUtils.RealPath` implements GNU-compatible lexical, physical, and no-link canonical pathname output over the neutral `Icod.Path` resolver.

The conformance baseline is GNU Coreutils 9.11. The command supports three missing-component policies, relative output, newline/NUL delimiters, quiet diagnostics, cancellation, and per-operand continuation. It never reports an unresolved input spelling as a successful canonical path.

## AUTHORS

GNU `realpath` was written by Pádraig Brady.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`realpath(1)`, `readlink(1)`
