# CHGRP(1)

## NAME

**chgrp** — change file group ownership

## SYNOPSIS

```text
chgrp [OPTION]... GROUP FILE...
chgrp [OPTION]... --reference=RFILE FILE...
```

## PATHNAME GLOBBING

Target `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The GROUP operand, `--reference=RFILE`, and `--from=CURRENT` remain literal control values. A `**` pattern selects initial targets only; recursive ownership changes remain controlled by `-R` and the command's link-traversal options.

## DESCRIPTION

`Icod.CoreUtils.ChGrp` is a managed .NET implementation of GNU Coreutils `chgrp(1)`, modeled on GNU Coreutils 9.11.

The command changes the group ownership of each selected filesystem entry. Group names and numeric identities are resolved through the shared identity provider, while metadata observation, recursive traversal, and mutation use the shared filesystem contracts.

With `--reference`, the group is taken from `RFILE`. The implementation also supports the shared `--from=CURRENT` ownership filter, allowing a requested change to be conditional on the currently observed owner/group.

## OPTIONS

```text
-c, --changes
    Report only files whose ownership actually changes.

-f, --silent, --quiet
    Suppress most error diagnostics.

-v, --verbose
    Report every file processed.

--dereference
    Affect the referent of each symbolic link.

-h, --no-dereference
    Affect a symbolic link itself rather than its referent when supported.

--from=CURRENT
    Apply a change only when the current owner/group matches CURRENT.

--no-preserve-root
    Do not treat `/` specially during recursive processing.

--preserve-root
    Refuse to operate recursively on `/`.

--reference=RFILE
    Copy the relevant ownership values from RFILE.

-R, --recursive
    Operate recursively.

-H
    With -R, traverse command-line directory symbolic links.

-L
    With -R, traverse every directory symbolic link encountered.

-P
    With -R, do not traverse directory symbolic links. This is the default.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
0    Every requested ownership operation completed or was intentionally skipped.
1    Usage, identity resolution, traversal, metadata, or mutation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

POSIX ownership mutation is capability-dependent. If the active filesystem mutation provider cannot set ownership, `chgrp` reports that ownership mutation is unsupported rather than simulating success.

Recursive operations use race-aware traversal observations and stable filesystem identities where available. No-follow ownership changes on symbolic links are applied only when the platform provider exposes that capability.

## AUTHORS

GNU `chgrp` was written by David MacKenzie and Jim Meyering.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`chgrp(1)`, `chown(1)`, `chmod(1)`, `id(1)`
